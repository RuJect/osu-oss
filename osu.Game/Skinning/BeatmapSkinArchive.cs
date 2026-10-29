// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Storyboards;
using osu.Game.Utils;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    internal static class BeatmapSkinArchive
    {
        public const string SKIN_CONFIG_FILENAME = @"skin.ini";

        private static readonly string[] known_visual_skin_prefixes =
        [
            @"hitcircle", @"approachcircle", @"reversearrow", @"sliderstartcircle", @"sliderendcircle", @"sliderfollowcircle",
            @"sliderb", @"sliderscorepoint", @"cursor", @"cursortrail", @"cursormiddle", @"followpoint",
            @"hit0", @"hit50", @"hit100", @"hit300", @"lighting", @"particle", @"star",
            @"default-", @"score-", @"combo-", @"scorebar-", @"spinner-", @"comboburst", @"selection-",
            @"section-", @"menu-", @"pause-", @"fail-", @"play-", @"ranking-", @"inputoverlay-",
            @"scoreentry-", @"taiko", @"fruit", @"banana", @"mania-", @"stage-", @"key", @"note",
        ];

        public static bool HasCustomSkin(WorkingBeatmap beatmap)
        {
            IEnumerable<string>? availableResources = getAvailableResources(beatmap);
            return availableResources != null && HasCustomSkin(beatmap.BeatmapInfo.BeatmapSet, beatmap.Storyboard, availableResources);
        }

        public static bool HasCustomSkin(IBeatmapSetInfo? beatmapSet, Storyboard storyboard, IEnumerable<string> availableResources)
        {
            HashSet<string> excludedFiles = getExcludedFiles(beatmapSet, storyboard);
            return availableResources.Any(file => isSkinMarker(file, excludedFiles));
        }

        public static MemoryStream? CreateArchive(WorkingBeatmap beatmap, out string filename)
            => createArchive(beatmap.BeatmapInfo.BeatmapSet, beatmap.Storyboard, beatmap.GetStream, beatmap.Skin as LegacySkin, out filename);

        public static MemoryStream? CreateArchive(IBeatmapSetInfo? beatmapSet, Storyboard storyboard, Func<string, Stream?> getStream, out string filename)
            => createArchive(beatmapSet, storyboard, getStream, null, out filename);

        private static MemoryStream? createArchive(IBeatmapSetInfo? beatmapSet, Storyboard storyboard, Func<string, Stream?> getStream, LegacySkin? legacySkin, out string filename)
        {
            filename = string.Empty;

            if (beatmapSet == null)
                return null;

            HashSet<string> excludedFiles = getExcludedFiles(beatmapSet, storyboard);

            if (!beatmapSet.Files.Any(f => isSkinMarker(f.Filename, excludedFiles)))
                return null;

            var files = beatmapSet.Files.Where(f => shouldIncludeInArchive(f.Filename, excludedFiles)).ToArray();

            if (files.Length == 0)
                return null;

            var archiveStream = new MemoryStream();
            bool skinIniWritten = false;

            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in files)
                {
                    bool isSkinIni = isSkinConfiguration(file.Filename);

                    if (isSkinIni && skinIniWritten)
                        continue;

                    using (Stream? source = getStream(file.File.GetStoragePath()))
                    {
                        if (source == null)
                        {
                            Logger.Log($"File {file.Filename} is missing and will not be included in the beatmap skin archive", LoggingTarget.Database);
                            continue;
                        }

                        string entryFilename = isSkinIni ? SKIN_CONFIG_FILENAME : file.Filename;
                        ZipArchiveEntry entry = archive.CreateEntry(entryFilename, CompressionLevel.Optimal);

                        using (Stream entryStream = entry.Open())
                            source.CopyTo(entryStream);

                        skinIniWritten |= isSkinIni;
                    }
                }

                if (!skinIniWritten)
                    writeGeneratedSkinIni(archive, beatmapSet, legacySkin);
            }

            filename = $"{beatmapSet.Metadata.GetDisplayString()} [beatmap skin]".GetValidFilename() + @".osk";
            archiveStream.Position = 0;
            return archiveStream;
        }

        private static IEnumerable<string>? getAvailableResources(WorkingBeatmap beatmap)
        {
            var resources = (beatmap.Skin as LegacyBeatmapSkin)?.BeatmapSetResources;

            if (resources != null)
                return resources.GetAvailableResources();

            return beatmap.BeatmapInfo.BeatmapSet?.Files.Select(f => f.Filename);
        }

        private static HashSet<string> getExcludedFiles(IBeatmapSetInfo? beatmapSet, Storyboard storyboard)
        {
            HashSet<string> excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (beatmapSet != null)
            {
                foreach (IBeatmapInfo difficulty in beatmapSet.Beatmaps)
                {
                    addIfNotEmpty(excludedFiles, difficulty.Metadata.AudioFile);
                    addIfNotEmpty(excludedFiles, difficulty.Metadata.BackgroundFile);
                }
            }

            addStoryboardResources(storyboard, excludedFiles);

            return excludedFiles;
        }

        private static bool isSkinMarker(string filename, HashSet<string> excludedFiles)
        {
            string path = filename.ToStandardisedPath();

            if (isSkinConfiguration(path))
                return true;

            if (excludedFiles.Contains(path))
                return false;

            return isKnownVisualSkinAsset(path);
        }

        private static bool shouldIncludeInArchive(string filename, HashSet<string> excludedFiles)
        {
            string path = filename.ToStandardisedPath();

            if (isSkinMarker(path, excludedFiles))
                return true;

            if (excludedFiles.Contains(path))
                return false;

            string extension = Path.GetExtension(path).ToLowerInvariant();

            if (extension == @".osu" || extension == @".osb" || SupportedExtensions.VIDEO_EXTENSIONS.Contains(extension))
                return false;

            return SupportedExtensions.AUDIO_EXTENSIONS.Contains(extension);
        }

        private static bool isKnownVisualSkinAsset(string filename)
        {
            string extension = Path.GetExtension(filename).ToLowerInvariant();

            if (!SupportedExtensions.IMAGE_EXTENSIONS.Contains(extension))
                return false;

            string name = Path.GetFileNameWithoutExtension(filename).ToLowerInvariant();

            if (name.EndsWith(@"@2x", StringComparison.Ordinal))
                name = name[..^3];

            return known_visual_skin_prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static bool isSkinConfiguration(string filename)
            => string.Equals(Path.GetFileName(filename.ToStandardisedPath()), SKIN_CONFIG_FILENAME, StringComparison.OrdinalIgnoreCase);

        private static void addStoryboardResources(Storyboard storyboard, HashSet<string> excludedFiles)
        {
            if (storyboard == null)
                return;

            foreach (IStoryboardElement element in storyboard.Layers.SelectMany(l => l.Elements))
            {
                addIfNotEmpty(excludedFiles, element.Path);

                if (element is StoryboardAnimation animation && !string.IsNullOrEmpty(animation.Path))
                {
                    string path = animation.Path.ToStandardisedPath();
                    string directory = Path.GetDirectoryName(path) ?? string.Empty;
                    string name = Path.GetFileNameWithoutExtension(path);
                    string extension = Path.GetExtension(path);

                    for (int i = 0; i < animation.FrameCount; i++)
                        addIfNotEmpty(excludedFiles, Path.Combine(directory, $"{name}{i}{extension}").ToStandardisedPath());
                }
            }
        }

        private static void addIfNotEmpty(HashSet<string> files, string? filename)
        {
            if (!string.IsNullOrEmpty(filename))
                files.Add(filename.ToStandardisedPath());
        }

        private static void writeGeneratedSkinIni(ZipArchive archive, IBeatmapSetInfo beatmapSet, LegacySkin? legacySkin)
        {
            ZipArchiveEntry entry = archive.CreateEntry(SKIN_CONFIG_FILENAME, CompressionLevel.Optimal);

            using (StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.WriteLine(@"[General]");
                writer.WriteLine($@"Name: {beatmapSet.Metadata.GetDisplayString()} (Beatmap Skin)");
                writer.WriteLine($@"Author: {beatmapSet.Metadata.Author.Username}");

                IReadOnlyList<Color4>? comboColours = legacySkin?.Configuration.ComboColours;

                if (comboColours == null || comboColours.Count == 0)
                    return;

                writer.WriteLine();
                writer.WriteLine(@"[Colours]");

                for (int i = 0; i < comboColours.Count; i++)
                {
                    Color4 colour = comboColours[i];
                    writer.WriteLine(FormattableString.Invariant($@"Combo{i + 1}: {colour.R * 255:0},{colour.G * 255:0},{colour.B * 255:0}"));
                }
            }
        }
    }
}
