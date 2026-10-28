// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.Versioning;
using osu.Desktop.LegacyIpc;
using osu.Desktop.Recording;
using osu.Desktop.Windows;
using osu.Framework;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.API.Commands;
using osu.Game.API.Events;
using osu.Game.API.Plugins;
using osu.Game.API.UI.Commands;
using osu.Game.API.UI.Scripting;
using osu.Game.Auth;
using osu.Game.IPC;
using osu.Game.Recording;
using osu.Game.Tournament;
using SDL;
using Velopack;

namespace osu.Desktop
{
    public static class Program
    {
        private const string base_game_name = @"osu-oss";

        private static LegacyTcpIpcProvider? legacyIpc;

        private static bool isFirstRun;

        [STAThread]
        public static void Main(string[] args)
        {
            // IMPORTANT DON'T IGNORE: For general sanity, velopack's setup needs to run before anything else.
            // This has bitten us in the rear before (bricked updater), and although the underlying issue from
            // last time has been fixed, let's not tempt fate.
            setupVelopack(args);

            // Make the auth measurement redirect shim transparent on Linux/macOS by
            // re-executing the process with it preloaded (no manual LD_PRELOAD).
            // Returns immediately on Windows or when no re-exec is required.
            RedirectBootstrap.EnsurePreloaded();

            if (OperatingSystem.IsWindows())
            {
                var windowsVersion = Environment.OSVersion.Version;

                // While .NET 8 only supports Windows 10 and above, running on Windows 7/8.1 may still work. We are limited by realm currently, as they choose to only support 8.1 and higher.
                // See https://www.mongodb.com/docs/realm/sdk/dotnet/compatibility/
                if (windowsVersion.Major < 6 || (windowsVersion.Major == 6 && windowsVersion.Minor <= 2))
                {
                    unsafe
                    {
                        // If users running in compatibility mode becomes more of a common thing, we may want to provide better guidance or even consider
                        // disabling it ourselves.
                        // We could also better detect compatibility mode if required:
                        // https://stackoverflow.com/questions/10744651/how-i-can-detect-if-my-application-is-running-under-compatibility-mode#comment58183249_10744730
                        SDL3.SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR,
                            "Your operating system is too old to run osu!"u8,
                            "This version of osu! requires at least Windows 8.1 to run.\n"u8
                            + "Please upgrade your operating system or consider using an older version of osu!.\n\n"u8
                            + "If you are running a newer version of windows, please check you don't have \"Compatibility mode\" turned on for osu!"u8, null);
                        return;
                    }
                }
            }

            // NVIDIA profiles are based on the executable name of a process.
            // Lazer and stable share the same executable name.
            // Stable sets this setting to "Off", which may not be what we want, so let's force it back to the default "Auto" on startup.
            if (OperatingSystem.IsWindows())
                NVAPI.ThreadedOptimisations = NvThreadControlSetting.OGL_THREAD_CONTROL_DEFAULT;

            // This is a safe default. Localised usages should specify lower values as required.
            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(1000));

            // Back up the cwd before DesktopGameHost changes it
            string cwd = Environment.CurrentDirectory;

            string gameName = base_game_name;
            string? dataPath = null;
            bool tournamentClient = false;
            bool noNetwork = false;
            bool sharedData = false;
            RecordingOptions? recordingOptions = null;

            foreach (string arg in args)
            {
                string[] split = arg.Split('=');

                string key = split[0];
                string val = split.Length > 1 ? split[1] : string.Empty;

                switch (key)
                {
                    case "--tournament":
                        tournamentClient = true;
                        break;

                    case "--no-network":
                        noNetwork = true;
                        break;

                    case "--shared":
                        sharedData = true;
                        break;

                    case "--data-path":
                        if (string.IsNullOrWhiteSpace(val))
                            throw new ArgumentException("Provided data path must not be empty.");

                        dataPath = Path.GetFullPath(val, cwd);
                        break;

                    case "--debug-client-id":
                        if (!DebugUtils.IsDebugBuild)
                            throw new InvalidOperationException("Cannot use this argument in a non-debug build.");

                        if (!int.TryParse(val, out int clientID))
                            throw new ArgumentException("Provided client ID must be an integer.");

                        gameName = $"{base_game_name}-{clientID}";
                        break;

                    case "--recording":
                    case "--recording-output":
                    case "--recording-duration":
                    case "--recording-dry-run":
                    case "--recording-codec":
                    case "--recording-preset":
                        // These will be parsed by RecordingOptions.Parse
                        break;
                }
            }

            if (sharedData && dataPath == null)
            {
                dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu");
                Logger.Log($@"Using shared osu! data path ""{dataPath}"".", LoggingTarget.Runtime, LogLevel.Important);
            }

            // Parse recording options
            try
            {
                recordingOptions = RecordingOptions.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Logger.Log($"Invalid recording arguments: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            // If recording mode is enabled, run recording and exit
            if (recordingOptions?.Enabled == true)
            {
                // Use provided data path or default
                string recordingDataPath = dataPath ?? getDefaultDataPath();

                try
                {
                    Directory.CreateDirectory(recordingDataPath);
                }
                catch (Exception e)
                {
                    Logger.Log($@"Could not use data path ""{recordingDataPath}"": {e.Message}", LoggingTarget.Runtime, LogLevel.Error);
                    return;
                }

                Logger.Log($"Starting recording: {recordingOptions}", LoggingTarget.Runtime, LogLevel.Important);

                using (var recordingHost = new RecordingHost(recordingOptions, recordingDataPath))
                    Environment.ExitCode = recordingHost.RunAndRecord();

                return;
            }

            if (dataPath != null)
            {
                try
                {
                    Directory.CreateDirectory(dataPath);
                }
                catch (Exception e)
                {
                    Logger.Log($@"Could not use provided data path ""{dataPath}"": {e.Message}", LoggingTarget.Runtime, LogLevel.Error);
                    return;
                }
            }

            var hostOptions = new HostOptions
            {
                IPCPipeName = tournamentClient ? null : sharedData ? $"{OsuGame.IPC_PIPE_NAME}-shared" : OsuGame.IPC_PIPE_NAME,
                FriendlyGameName = OsuGameBase.GAME_NAME,
            };

            using (DesktopGameHost host = Host.GetSuitableDesktopHost(gameName, hostOptions))
            {
                if (!host.IsPrimaryInstance)
                {
                    if (trySendIPCMessage(host, cwd, args))
                        return;

                    Logger.Log(@"osu! does not support multiple running instances.", LoggingTarget.Runtime, LogLevel.Error);
                    return;
                }

                if (host.IsPrimaryInstance)
                {
                    try
                    {
                        Logger.Log("Starting legacy IPC provider...");
                        legacyIpc = new LegacyTcpIpcProvider();
                        legacyIpc.Bind();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to start legacy IPC provider");
                    }
                }

                if (tournamentClient)
                    host.Run(new TournamentGame { DataPath = dataPath });
                else
                {
                    var game = new OsuGameDesktop(args)
                    {
                        IsFirstRun = isFirstRun,
                        EnableWebSocketServer = Environment.GetEnvironmentVariable("OSU_WEBSOCKET_SERVER") == "1",
                        NoNetwork = noNetwork,
                        DataPath = dataPath,
                    };

                    // Proprietary auth transplant: initialises APIRequest.Sign /
                    // HubClientConnector.Sign (token signing). Requires the native
                    // auth module + file-measurement redirect (see Auth/).
                    using (noNetwork ? null : new Loader(host, game))
                    // Game event API: shared typed event bus consumed by the scripting subsystem and plugins.
                    using (var events = new OsuEventBootstrap(host, game))
                    // C# plugin subsystem: loads plugins from osu-oss/plugins and hosts the built-in "system" plugin.
                    using (var plugins = new PluginBootstrap(game, host, events.Bus))
                    // JavaScript scripting subsystem: runs in the system plugin context and hosts the Ctrl+F4 console overlay.
                    using (var scripting = new ScriptingBootstrap(game, plugins))
                    // Command API + Ctrl+Space command palette.
                    using (var commands = new CommandBootstrap(game))
                    using (new CommandPaletteBootstrap(game, commands))
                    {
                        game.ShutdownRequested += plugins.Shutdown;

                        try
                        {
                            host.Run(game);
                        }
                        finally
                        {
                            game.ShutdownRequested -= plugins.Shutdown;
                        }
                    }
                }
            }
        }

        private static bool trySendIPCMessage(IIpcHost host, string cwd, string[] args)
        {
            if (args.Length == 1 && args[0].StartsWith(OsuGameBase.OSU_PROTOCOL, StringComparison.Ordinal))
            {
                var osuSchemeLinkHandler = new OsuSchemeLinkIPCChannel(host);
                if (!osuSchemeLinkHandler.HandleLinkAsync(args[0]).Wait(3000))
                    throw new IPCTimeoutException(osuSchemeLinkHandler.GetType());

                return true;
            }

            if (args.Length > 0 && args[0].Contains('.')) // easy way to check for a file import in args
            {
                var importer = new ArchiveImportIPCChannel(host);

                foreach (string file in args)
                {
                    Console.WriteLine(@"Importing {0}", file);
                    if (!importer.ImportAsync(Path.GetFullPath(file, cwd)).Wait(3000))
                        throw new IPCTimeoutException(importer.GetType());
                }

                return true;
            }

            return false;
        }

        private static string getDefaultDataPath()
        {
            // Use the same logic as OsuGameBase for default data path
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(basePath, "osu-oss");
        }

        private static void setupVelopack(string[] args)
        {
            // Arguments being present indicate the user is either starting the game in a special (aka tournament) mode,
            // or is running with pending imports via file association or otherwise.
            //
            // In both these scenarios, we'd hope the game does not attempt to update.
            //
            // Special consideration for velopack startup arguments, which must be handled during update.
            // See https://docs.velopack.io/integrating/hooks#command-line-hooks.
            if (args.Length > 0 && !args[0].StartsWith("--velo", StringComparison.Ordinal))
            {
                Logger.Log("Handling arguments, skipping velopack setup.");
                return;
            }

            if (OsuGameDesktop.IsPackageManaged)
            {
                Logger.Log("Updates are being managed by an external provider. Skipping Velopack setup.");
                return;
            }

            var app = VelopackApp.Build();

            app.OnFirstRun(_ => isFirstRun = true);

            if (OperatingSystem.IsWindows())
                configureWindows(app);

            app.Run();
        }

        [SupportedOSPlatform("windows")]
        private static void configureWindows(VelopackApp app)
        {
            app.OnFirstRun(_ =>
            {
                WindowsAssociationManager.InstallAssociations();
                SharedShortcutManager.Install();
            });

            app.OnAfterUpdateFastCallback(_ =>
            {
                WindowsAssociationManager.UpdateAssociations();
                SharedShortcutManager.Install();
            });

            app.OnBeforeUninstallFastCallback(_ =>
            {
                WindowsAssociationManager.UninstallAssociations();
                SharedShortcutManager.Uninstall();
            });
        }
    }
}
