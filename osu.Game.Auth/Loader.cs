// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;

namespace osu.Game.Auth
{
    /// <summary>
    /// Entry point of the auth module. Installs request signing and starts the native module.
    /// Mirrors the proprietary loader surface (same type name/namespace for drop-in use).
    /// </summary>
    /// <remarks>
    /// Two signing methods are available, selected via the <c>OSU_AUTH_METHOD</c> environment
    /// variable (see <see cref="AuthMethod"/>): <c>transplant</c> (default) delegates to the
    /// proprietary managed module (<see cref="TransplantSigner"/>), while <c>native</c> uses the
    /// open reimplementation (<see cref="NativeAuthSigner"/>).
    /// <para>
    /// The transplant method requires pristine official copies of the measured assemblies, which
    /// are fetched at runtime from the matching release (<see cref="OfficialFileCache"/>, see
    /// <see cref="OfficialFilesDownloader"/>). Until those are present, signing is disabled entirely.
    /// </para>
    /// </remarks>
    public class Loader : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        private readonly IDisposable? implementation;
        private bool disposed;

        public Loader(GameHost host, OsuGameBase game)
        {
            implementation = CreateImplementation(host, game);
        }

        private static IDisposable CreateImplementation(GameHost host, OsuGameBase game)
        {
            if (!AuthMethod.IsTransplant())
            {
                logger.Add("Using native (reimplemented) auth signing.");
                return new NativeAuthSigner(host, game);
            }

            return createTransplant(host, game);
        }

        private static IDisposable createTransplant(GameHost host, OsuGameBase game)
        {
            OfficialFileCache files;

            try
            {
                files = new OfficialFileCache(NativeModule.ResolveStorage(host), game.Version, isTachyon(game));
            }
            catch (Exception e)
            {
                logger.Add($"Could not resolve auth storage ({e.Message}); falling back to native signing.");
                return new NativeAuthSigner(host, game);
            }

            if (!files.IsReady)
            {
                logger.Add($"Official auth files for {files.ReleaseTag} are missing; signing is disabled until they are downloaded. Fetching in the background...");
                return OfficialFilesDownloader.Start(host, game, files);
            }

            try
            {
                NativeEnvironment.Set(OfficialFileCache.DIR_ENV, files.Directory);

                if (!RedirectShim.EnsureActive())
                {
                    logger.Add("Auth measurement redirect shim is not active; falling back to native signing.");
                    return new NativeAuthSigner(host, game);
                }

                var transplant = new TransplantSigner(host, game);
                logger.Add("Using transplanted (proprietary) auth signing.");
                return transplant;
            }
            catch (Exception e)
            {
                logger.Add($"Managed auth transplant failed ({e.Message}); falling back to native signing.");
                return new NativeAuthSigner(host, game);
            }
        }

        private static bool isTachyon(OsuGameBase game)
            => game.Version.Contains("tachyon", StringComparison.Ordinal);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            implementation?.Dispose();
        }
    }
}
