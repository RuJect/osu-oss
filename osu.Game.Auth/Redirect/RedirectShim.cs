// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using osu.Framework.Logging;

namespace osu.Game.Auth.Redirect
{
    /// <summary>
    /// Locates and (on Windows) loads the measurement redirect shim (<c>authredirect</c>)
    /// for the current platform.
    /// </summary>
    /// <remarks>
    /// On Linux/macOS the shim must be injected by the dynamic loader at process start
    /// (<c>LD_PRELOAD</c> / <c>DYLD_INSERT_LIBRARIES</c>), because a runtime <c>dlopen</c>
    /// cannot replace already-bound libc symbols. <see cref="RedirectBootstrap"/> arranges
    /// this transparently by re-executing the process. On Windows there is no such
    /// mechanism, so the shim is loaded here; its <c>DllMain</c> registers a DLL-load
    /// notification that redirects the native module's own imports when it is loaded later.
    /// </remarks>
    internal static class RedirectShim
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        /// <summary>Environment variable overriding the shim path.</summary>
        private const string path_env = "OSU_AUTHREDIRECT";

        /// <summary>
        /// Marker set in the environment of a process that was bootstrapped with the shim
        /// preloaded (see <see cref="RedirectBootstrap"/>).
        /// </summary>
        internal const string ACTIVE_ENV = "OSU_AUTHREDIRECT_ACTIVE";

        /// <summary>Common file name stem of the shim on every platform.</summary>
        internal const string SHIM_NAME = "authredirect";

        private static bool? active;

        /// <summary>
        /// Resolves the shim path for the current platform, honouring <c>OSU_AUTHREDIRECT</c>.
        /// </summary>
        public static string ResolvePath()
        {
            string fileName = OperatingSystem.IsWindows() ? "authredirect.dll"
                : OperatingSystem.IsMacOS() ? "authredirect.dylib"
                : "authredirect.so";

            string? overridePath = Environment.GetEnvironmentVariable(path_env);

            return !string.IsNullOrEmpty(overridePath)
                ? Path.GetFullPath(overridePath)
                : Path.Combine(AppContext.BaseDirectory, fileName);
        }

        /// <summary>
        /// Ensures the shim is active for this process and reports whether it is.
        /// On Windows this attempts to load the DLL; on Linux/macOS it only verifies that
        /// the process was started with the shim preloaded.
        /// </summary>
        public static bool EnsureActive()
        {
            if (active.HasValue)
                return active.Value;

            active = OperatingSystem.IsWindows() ? loadWindowsShim() : isPosixActive();

            return active.Value;
        }

        private static bool isPosixActive()
        {
            if (Environment.GetEnvironmentVariable(ACTIVE_ENV) == "1")
                return true;

            // The shim may also have been preloaded by hand (documented escape hatch).
            string loaderVar = OperatingSystem.IsMacOS() ? "DYLD_INSERT_LIBRARIES" : "LD_PRELOAD";
            string? loader = Environment.GetEnvironmentVariable(loaderVar);

            return loader != null && loader.Contains(SHIM_NAME, StringComparison.Ordinal);
        }

        private static bool loadWindowsShim()
        {
            string path = ResolvePath();

            if (!File.Exists(path))
            {
                logger.Add($"Auth redirect shim not found at {path}; signing will not be redirected.");
                return false;
            }

            try
            {
                NativeLibrary.Load(path);
                logger.Add($"Loaded auth redirect shim from {path}.");
                return true;
            }
            catch (Exception e)
            {
                logger.Add($"Failed to load auth redirect shim ({e.Message}).");
                return false;
            }
        }
    }
}
