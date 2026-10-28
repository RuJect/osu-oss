// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.IO;

namespace osu.Game.Auth.Provisioning
{
    /// <summary>
    /// Manages pristine official copies of the assemblies measured by the native auth
    /// module. Instead of shipping the proprietary blobs, they are fetched at runtime
    /// from the matching release: on Linux from <c>osu.AppImage</c> (a squashfs image
    /// that its own bundled runtime can extract without FUSE or external tooling), on
    /// macOS from the architecture-matching <c>osu.app.*.zip</c> app bundle, and on
    /// Windows from the Squirrel full package.
    /// </summary>
    internal sealed class OfficialFileCache
    {
        /// <summary>Environment variable consumed by the measurement redirect shim.</summary>
        public const string DIR_ENV = "AUTH_OFFICIAL_DIR";

        private const string marker_file_name = ".version";

        private const string release_base_url = "https://github.com/ppy/osu/releases/download/";
        private const string appimage_asset_name = "osu.AppImage";

        /// <summary>Path (within the AppImage) of the directory holding the app assemblies.</summary>
        private const string appimage_source_dir = "usr/bin";

        /// <summary>Where the requested path lands after extraction, relative to the working directory.</summary>
        private const string appimage_output_dir = "squashfs-root/usr/bin";

        /// <summary>Release assets bundling the official macOS app, per architecture.</summary>
        private const string macos_asset_apple_silicon = "osu.app.Apple.Silicon.zip";
        private const string macos_asset_intel = "osu.app.Intel.zip";

        /// <summary>Path (within the macOS archive) of the directory holding the app assemblies.</summary>
        private const string macos_source_dir = "osu!.app/Contents/MacOS";

        /// <summary>Path (within the Windows Squirrel package) of the directory holding the app assemblies.</summary>
        private const string windows_source_dir = "lib/app";

        /// <summary>The managed auth module, by its original file name in official release archives.</summary>
        public const string AUTH_MODULE_FILE_NAME = "osu.Game.Auth.dll";

        /// <summary>
        /// The auth module's file name in the cache. Renamed to avoid clashing with this project's
        /// identically-named assembly when loaded into its isolated load context.
        /// </summary>
        public const string AUTH_MODULE_CACHE_NAME = "osu.Game.Auth.transplant.dll";

        /// <summary>The assemblies measured by the native module, by original file name.</summary>
        private static readonly string[] measured_file_names =
        {
            "osu!.dll",
            "osu.Framework.dll",
            "osu.Game.dll",
            "osu.Game.Rulesets.Catch.dll",
            "osu.Game.Rulesets.Mania.dll",
            "osu.Game.Rulesets.Osu.dll",
            "osu.Game.Rulesets.Taiko.dll",
        };

        /// <summary>Every file required before the cache is considered ready.</summary>
        private static readonly string[] cached_file_names = measured_file_names.Append(AUTH_MODULE_CACHE_NAME).ToArray();

        private readonly Storage directory;

        /// <summary>
        /// Resolves the osu! user storage directory which owns the auth cache.
        /// </summary>
        internal static Storage ResolveStorage(GameHost host)
        {
            foreach (string path in host.UserStoragePaths)
            {
                Storage storage = host.GetStorage(path);

                if (storage.ExistsDirectory("osu"))
                    return storage.GetStorageForDirectory("osu");
            }

            foreach (string path in host.UserStoragePaths)
            {
                try
                {
                    return host.GetStorage(path).GetStorageForDirectory("osu");
                }
                catch
                {
                    // try next path.
                }
            }

            throw new InvalidOperationException("No valid user storage path could be resolved.");
        }

        public OfficialFileCache(Storage rootStorage, string gameVersion, bool isTachyon)
        {
            directory = SpecialStorage.GetAuthBlobs(rootStorage);
            Directory = SpecialStorage.GetRoot(rootStorage).GetFullPath(SpecialStorage.AUTH_BLOBS_FOLDER, true);
            ReleaseTag = $"{normalizeVersion(gameVersion)}-{(isTachyon ? "tachyon" : "lazer")}";
        }

        /// <summary>Absolute path of the cache directory (valid as <see cref="DIR_ENV"/>).</summary>
        public string Directory { get; }

        /// <summary>The GitHub release tag the blobs are sourced from (e.g. <c>2026.1005.0-lazer</c>).</summary>
        public string ReleaseTag { get; }

        public string AppImageUrl => release_base_url + ReleaseTag + "/" + appimage_asset_name;

        /// <summary>The macOS release archive bundling the official app for the current architecture.</summary>
        public string MacosArchiveUrl => release_base_url + ReleaseTag + "/"
                                         + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                                             ? macos_asset_apple_silicon
                                             : macos_asset_intel);

        /// <summary>The Windows Squirrel package bundling the official app.</summary>
        public string WindowsPackageUrl => release_base_url + ReleaseTag + "/osulazer-" + ReleaseTag + "-full.nupkg";

        /// <summary>Whether a complete, version-matching set of official files is cached.</summary>
        public bool IsReady
        {
            get
            {
                try
                {
                    if (!directory.Exists(marker_file_name))
                        return false;

                    using (var reader = new StreamReader(directory.GetStream(marker_file_name, FileAccess.Read, FileMode.Open)))
                    {
                        if (reader.ReadToEnd().Trim() != ReleaseTag)
                            return false;
                    }

                    return cached_file_names.All(directory.Exists);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to validate official auth files: {e}", LoggingTarget.Network);
                    return false;
                }
            }
        }

        /// <summary>
        /// Downloads the platform release archive and extracts the measured assemblies into the cache.
        /// <paramref name="progress"/> is reported in the range [0, 1].
        /// </summary>
        public async Task DownloadAsync(IProgress<double>? progress, CancellationToken token)
        {
            if (OperatingSystem.IsLinux())
            {
                await downloadLinuxAsync(progress, token).ConfigureAwait(false);
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                await downloadMacosAsync(progress, token).ConfigureAwait(false);
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                await downloadWindowsAsync(progress, token).ConfigureAwait(false);
                return;
            }

            throw new PlatformNotSupportedException("Official auth file retrieval is only implemented for Linux, macOS and Windows.");
        }

        [SupportedOSPlatform("linux")]
        private async Task downloadLinuxAsync(IProgress<double>? progress, CancellationToken token)
        {
            string staging = Path.Combine(Path.GetTempPath(), "osu-auth-" + Guid.NewGuid().ToString("N"));

            try
            {
                System.IO.Directory.CreateDirectory(staging);

                string appImage = Path.Combine(staging, "osu.AppImage");
                await downloadArchiveAsync(appImage, AppImageUrl, progress, 0.9, token).ConfigureAwait(false);

                File.SetUnixFileMode(appImage, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                               | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                               | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

                string extractRoot = Path.Combine(staging, "extract");
                System.IO.Directory.CreateDirectory(extractRoot);

                for (int i = 0; i < cached_file_names.Length; i++)
                {
                    await extractAppImageAsync(appImage, extractRoot, sourceFileName(cached_file_names[i]), token).ConfigureAwait(false);
                    progress?.Report(0.9 + 0.1 * (i + 1) / cached_file_names.Length);
                }

                string sourceDirectory = Path.Combine(extractRoot, appimage_output_dir);
                string authModulePath = Path.Combine(sourceDirectory, AUTH_MODULE_FILE_NAME);

                if (!File.Exists(authModulePath))
                    throw new FileNotFoundException($"Official auth module is missing from the release archive: {authModulePath}", authModulePath);

                File.Move(authModulePath, Path.Combine(sourceDirectory, AUTH_MODULE_CACHE_NAME), true);
                publish(sourceDirectory);
            }
            finally
            {
                try { System.IO.Directory.Delete(staging, true); }
                catch { }
            }
        }

        [SupportedOSPlatform("macos")]
        private async Task downloadMacosAsync(IProgress<double>? progress, CancellationToken token)
        {
            string staging = Path.Combine(Path.GetTempPath(), "osu-auth-" + Guid.NewGuid().ToString("N"));

            try
            {
                System.IO.Directory.CreateDirectory(staging);

                string archive = Path.Combine(staging, "osu.app.zip");
                await downloadArchiveAsync(archive, MacosArchiveUrl, progress, 0.9, token).ConfigureAwait(false);

                string extractRoot = Path.Combine(staging, "extract");
                System.IO.Directory.CreateDirectory(extractRoot);

                using (ZipArchive zip = ZipFile.OpenRead(archive))
                {
                    for (int i = 0; i < cached_file_names.Length; i++)
                    {
                        // The archive also contains AppleDouble (__MACOSX) entries; the exact
                        // bundle-relative path below selects the real assembly.
                        string entryName = macos_source_dir + "/" + sourceFileName(cached_file_names[i]);

                        ZipArchiveEntry entry = zip.GetEntry(entryName)
                                                ?? throw new FileNotFoundException($"Official file is missing from the release archive: {entryName}", entryName);

                        entry.ExtractToFile(Path.Combine(extractRoot, cached_file_names[i]), true);
                        token.ThrowIfCancellationRequested();
                        progress?.Report(0.9 + 0.1 * (i + 1) / cached_file_names.Length);
                    }
                }

                publish(extractRoot);
            }
            finally
            {
                try { System.IO.Directory.Delete(staging, true); }
                catch { }
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task downloadWindowsAsync(IProgress<double>? progress, CancellationToken token)
        {
            string staging = Path.Combine(Path.GetTempPath(), "osu-auth-" + Guid.NewGuid().ToString("N"));

            try
            {
                System.IO.Directory.CreateDirectory(staging);

                string package = Path.Combine(staging, "osulazer.nupkg");
                await downloadArchiveAsync(package, WindowsPackageUrl, progress, 0.9, token).ConfigureAwait(false);

                string extractRoot = Path.Combine(staging, "extract");
                System.IO.Directory.CreateDirectory(extractRoot);

                using (ZipArchive zip = ZipFile.OpenRead(package))
                {
                    for (int i = 0; i < cached_file_names.Length; i++)
                    {
                        string entryName = windows_source_dir + "/" + sourceFileName(cached_file_names[i]);

                        ZipArchiveEntry entry = zip.GetEntry(entryName)
                                                ?? throw new FileNotFoundException($"Official file is missing from the release package: {entryName}", entryName);

                        entry.ExtractToFile(Path.Combine(extractRoot, cached_file_names[i]), true);
                        token.ThrowIfCancellationRequested();
                        progress?.Report(0.9 + 0.1 * (i + 1) / cached_file_names.Length);
                    }
                }

                publish(extractRoot);
            }
            finally
            {
                try { System.IO.Directory.Delete(staging, true); }
                catch { }
            }
        }

        private async Task downloadArchiveAsync(string destination, string url, IProgress<double>? progress, double scale, CancellationToken token)
        {
            Logger.Log($"Downloading official auth files ({url})...", LoggingTarget.Network);

            var request = new FileWebRequest(destination, url);

            request.DownloadProgress += (read, total) =>
            {
                if (total > 0)
                    progress?.Report(Math.Clamp((double)read / total, 0, 1) * scale);
            };

            await request.PerformAsync(token).ConfigureAwait(false);

            if (request.Aborted)
                throw new OperationCanceledException("Official auth file download aborted.");
        }

        private static async Task extractAppImageAsync(string appImage, string workingDirectory, string fileName, CancellationToken token)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = appImage,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            startInfo.ArgumentList.Add("--appimage-extract");
            startInfo.ArgumentList.Add($"{appimage_source_dir}/{fileName}");

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the AppImage extractor.");

            await process.WaitForExitAsync(token).ConfigureAwait(false);

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"AppImage extraction of {fileName} failed (exit code {process.ExitCode}).");
        }

        private void publish(string sourceDirectory)
        {
            // Remove the marker first so an interrupted publish leaves the cache invalid.
            if (directory.Exists(marker_file_name))
                directory.Delete(marker_file_name);

            foreach (string name in cached_file_names)
            {
                string source = Path.Combine(sourceDirectory, name);

                if (!File.Exists(source))
                    throw new FileNotFoundException($"Extracted official file is missing: {source}", source);

                using var sourceStream = File.OpenRead(source);
                using var destinationStream = directory.GetStream(name, FileAccess.Write, FileMode.Create);
                sourceStream.CopyTo(destinationStream);
            }

            using var marker = new StreamWriter(directory.GetStream(marker_file_name, FileAccess.Write, FileMode.Create));
            marker.Write(ReleaseTag);
        }

        private static string sourceFileName(string cachedFileName)
            => cachedFileName == AUTH_MODULE_CACHE_NAME ? AUTH_MODULE_FILE_NAME : cachedFileName;

        private static string normalizeVersion(string gameVersion)
        {
            // "2026.1005.0.0+sha" / "2026.1005.0-lazer+sha" / "2026.1005.0" -> "2026.1005.0"
            string withoutMetadata = gameVersion.Split('+').First();
            string[] numeric = withoutMetadata.Split('-').First().Split('.');
            return string.Join('.', numeric.Take(3));
        }
    }
}
