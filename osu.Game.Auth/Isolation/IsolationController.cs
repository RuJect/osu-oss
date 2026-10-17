// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// Selects and starts the platform isolation backend. Failures are logged and swallowed so that
    /// signing continues unguarded rather than breaking.
    /// </summary>
    internal static class IsolationController
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        public static IDisposable? Start(IsolationOptions options)
        {
            if (!options.Enabled)
            {
                logger.Add("Auth blob isolation disabled (AUTHISOLATE=0).");
                return null;
            }

            IIsolationBackend backend = create(options);

            if (!backend.IsSupported)
            {
                logger.Add("Auth blob isolation is not available on this platform.");
                backend.Dispose();
                return null;
            }

            try
            {
                backend.Start();
                return backend;
            }
            catch (Exception e)
            {
                logger.Add($"Auth blob isolation setup failed ({e.Message}); continuing without it.");
                backend.Dispose();
                return null;
            }
        }

        private static IIsolationBackend create(IsolationOptions options)
        {
            if (OperatingSystem.IsLinux())
                return new LinuxSeccompBackend(options);

            if (OperatingSystem.IsWindows())
                return new WindowsIatBackend(options);

            return new MacShimBackend(options);
        }
    }
}
