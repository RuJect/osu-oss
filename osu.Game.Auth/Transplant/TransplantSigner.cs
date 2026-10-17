// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Auth.Isolation;

namespace osu.Game.Auth
{
    /// <summary>
    /// Signing method which transplants the proprietary managed
    /// <c>osu.Game.Auth.dll</c> (shipped next to the executable) in an isolated
    /// load context and delegates to its <see cref="Loader"/>, which installs the
    /// real <c>APIRequest.Sign</c> / <c>HubClientConnector.Sign</c> delegates.
    /// </summary>
    /// <remarks>
    /// The proprietary native module measures the game assemblies, so the process must
    /// run with the measurement redirect active. On Linux/macOS this is arranged
    /// transparently by <see cref="RedirectBootstrap"/> (which re-executes the process
    /// with the shim preloaded); on Windows <see cref="RedirectShim"/> loads it in-process.
    /// See <c>osu.Game.Auth/Redirect/native/authredirect.c</c>.
    /// </remarks>
    internal sealed class TransplantSigner : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        /// <summary>Environment variable overriding the proprietary assembly path.</summary>
        private const string path_env = "OSU_AUTH_TRANSPLANT";

        private const string default_file_name = "osu.Game.Auth.transplant.dll";

        private readonly AssemblyLoadContext context;
        private readonly IDisposable loader;
        private readonly IDisposable? isolation;

        public TransplantSigner(GameHost host, OsuGameBase game)
        {
            // Establish the syscall isolation before the proprietary module can execute anything.
            IDisposable? startedIsolation = IsolationController.Start(IsolationOptions.FromEnvironment());

            try
            {
                string path = ResolvePath();

                context = new TransplantLoadContext(path);
                Assembly assembly = context.LoadFromAssemblyPath(path);

                // Resolved by name (not a compile-time reference) to avoid clashing with
                // this project's identically-named assembly.
                Type? loaderType = assembly.GetType("osu.Game.Auth.Loader", throwOnError: true);

                loader = (IDisposable)Activator.CreateInstance(loaderType!, host, game)!;
                logger.Add($"Transplanted managed auth module from {path}.");
            }
            catch
            {
                startedIsolation?.Dispose();
                throw;
            }

            isolation = startedIsolation;
        }

        private static string ResolvePath()
        {
            string? overridePath = Environment.GetEnvironmentVariable(path_env);

            if (!string.IsNullOrEmpty(overridePath))
                return Path.GetFullPath(overridePath);

            string path = Path.Combine(AppContext.BaseDirectory, default_file_name);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Transplant auth module not found at {path}.", path);

            return path;
        }

        public void Dispose()
        {
            loader.Dispose();
            isolation?.Dispose();
        }

        /// <summary>
        /// Load context for the proprietary assembly. <see cref="Load"/> returns
        /// <c>null</c> so every dependency resolves against the already-loaded
        /// default context, preserving type identity for <c>osu.Game</c> and
        /// <c>osu.Framework</c>.
        /// </summary>
        private sealed class TransplantLoadContext : AssemblyLoadContext
        {
            public TransplantLoadContext(string mainAssemblyPath)
                : base($"osu.Game.Auth.transplant:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}")
            {
            }

            protected override Assembly? Load(AssemblyName assemblyName) => null;
        }
    }
}
