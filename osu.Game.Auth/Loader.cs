// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Auth.Provisioning;
using osu.Game.Auth.Redirect;
using osu.Game.Auth.Transplant;

namespace osu.Game.Auth
{
    /// <summary>
    /// Entry point of the auth module. Loads the proprietary managed auth module from the official
    /// file cache (<see cref="TransplantSigner"/>) and installs request signing.
    /// </summary>
    /// <remarks>
    /// Pristine official copies of the measured assemblies and the managed auth module are fetched at
    /// runtime from the matching release (<see cref="OfficialFileCache"/>, see
    /// <see cref="OfficialFilesDownloader"/>). Until they are present, signing is disabled entirely.
    /// </remarks>
    public class Loader : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        private readonly IDisposable? implementation;
        private bool disposed;

        public Loader(GameHost host, OsuGameBase game)
        {
            implementation = createImplementation(host, game);
        }

        private static IDisposable? createImplementation(GameHost host, OsuGameBase game)
        {
            OfficialFileCache files;

            try
            {
                files = new OfficialFileCache(OfficialFileCache.ResolveStorage(host), game.Version, isTachyon(game));
            }
            catch (Exception e)
            {
                logger.Add($"Could not resolve auth storage ({e.Message}); signing disabled.");
                return null;
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
                    logger.Add("Auth measurement redirect shim is not active; signing disabled.");
                    return null;
                }

                var transplant = new TransplantSigner(host, game, files.Directory);
                logger.Add("Using transplanted (proprietary) auth signing.");
                return transplant;
            }
            catch (Exception e)
            {
                logger.Add($"Managed auth transplant failed ({e.Message}); signing disabled.");
                return null;
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
