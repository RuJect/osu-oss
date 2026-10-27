// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.API.Scripting;
using osu.Game.API.Services;
using osu.Game.Configuration;
using osu.Game.Events;
using osu.Game.IO;
using osu.Game.Online.Server;
using osu.Game.Overlays;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Composition root of the plugin subsystem. Constructed from <c>osu.Desktop/Program.cs</c> around
    /// <c>host.Run(game)</c>; once the game has loaded, resolves the game services, registers the built-in
    /// "system" plugin (which the scripting subsystem runs against) and discovers plugins from
    /// <c>osu-oss/plugins</c> in the background for activation on the update thread.
    /// </summary>
    /// <remarks>
    /// Does not need to be constructed before <c>ScriptingBootstrap</c>; scripting consumers observe
    /// <see cref="SystemPluginLoaded"/> and <see cref="Initialised"/> and also check <see cref="IsInitialised"/>
    /// when attaching late.
    /// </remarks>
    public class PluginBootstrap : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("plugins");

        private readonly OsuGame game;
        private readonly GameHost host;
        private readonly OsuEventBus events;

        private readonly ConcurrentQueue<PluginLoadResult> pendingLoads = new ConcurrentQueue<PluginLoadResult>();

        private InputManagerProvider? input;
        private Storage? storageRoot;
        private ScriptingSettings? scriptingSettings;
        private SpecialConfigManager? specialConfig;
        private Bindable<bool>? scriptingEnabledBindable;
        private Bindable<bool>? allowClrAccessBindable;
        private CancellationTokenSource? loadCancellation;
        private volatile bool discoveryCompleted;
        private bool initialised;
        private bool disposed;
        private bool shutdown;
        private bool pluginsLoadedRaised;

        /// <summary>The plugin host, or <c>null</c> if initialisation failed.</summary>
        public PluginHost? Host { get; private set; }

        /// <summary>The resolved game services, or <c>null</c> if initialisation failed.</summary>
        public GameServices? Services { get; private set; }

        /// <summary>The built-in system plugin, or <c>null</c> if it could not be loaded.</summary>
        public LoadedPlugin? SystemPlugin { get; private set; }

        /// <summary>Whether the initialisation attempt has completed.</summary>
        public bool IsInitialised { get; private set; }

        /// <summary>Raised on the update thread immediately after the system plugin has been activated.</summary>
        public event Action<LoadedPlugin>? SystemPluginLoaded;

        /// <summary>Raised once after the initialisation attempt, regardless of success or failure.</summary>
        public event Action? Initialised;

        /// <summary>Raised once on the update thread when background discovery has finished and every discovered plugin has been activated.</summary>
        public event Action? PluginsLoaded;

        public PluginBootstrap(OsuGame game, GameHost host, OsuEventBus events)
        {
            this.game = game;
            this.host = host;
            this.events = events;

            if (game.IsLoaded)
                initialise();
            else
                game.OnLoadComplete += _ => initialise();
        }

        private void initialise()
        {
            if (initialised || disposed || shutdown)
                return;

            initialised = true;

            try
            {
                Storage storage = game.Dependencies.Get<Storage>();
                storageRoot = storage;
                input = new InputManagerProvider();

                Services = new GameServices(
                    game, host, new OutputLog(),
                    storage,
                    SpecialStorage.GetScripts(storage),
                    game.Dependencies.Get<MusicController>(),
                    game.Dependencies.Get<OsuConfigManager>(),
                    game.Dependencies.Get<INotificationOverlay>(),
                    game.Dependencies.Get<IServerRegistry>(),
                    game.Dependencies.Get<IServerManager>(),
                    () => input.Value);

                specialConfig = game.Dependencies.Get<SpecialConfigManager>();
                scriptingSettings = new ScriptingSettings();

                scriptingEnabledBindable = specialConfig.GetBindable<bool>(SpecialSetting.ScriptingEnabled);
                allowClrAccessBindable = specialConfig.GetBindable<bool>(SpecialSetting.AllowClrAccess);

                scriptingSettings.Update(scriptingEnabledBindable.Value, allowClrAccessBindable.Value);

                scriptingEnabledBindable.BindValueChanged(_ => updateScriptingSettings());
                allowClrAccessBindable.BindValueChanged(_ => updateScriptingSettings());

                Host = new PluginHost(events, Services.CreatePluginServices, Services.CreatePluginLogger, createScriptHost, Services.CreatePluginConfigStorage);

                game.Add(new PluginRuntimeComponent(Host, input, activateNextPlugin));

                loadSystemPlugin(storage);
                startDiscovery(storage);
            }
            catch (Exception e)
            {
                logger.Add($"failed to initialise plugins: {e.Message}", LogLevel.Error);
            }
            finally
            {
                IsInitialised = true;
                Initialised?.Invoke();
            }
        }

        private void updateScriptingSettings()
        {
            if (scriptingSettings == null || scriptingEnabledBindable == null || allowClrAccessBindable == null)
                return;

            scriptingSettings.Update(scriptingEnabledBindable.Value, allowClrAccessBindable.Value);
            Host?.ApplyScriptingSettings(scriptingSettings);
        }

        private PluginScriptHost? createScriptHost(PluginManifest manifest, string? jsEntryPoint, PluginContext context)
        {
            bool global = manifest.Id == PluginManifest.SYSTEM_ID;

            if (!global && string.IsNullOrEmpty(jsEntryPoint))
                return null;

            if (!manifest.Permissions.CanUseJs)
            {
                context.Logger.Warn("javascript entry ignored: missing js permission");
                return null;
            }

            if (storageRoot == null || scriptingSettings == null)
                return null;

            Storage storage = global ? SpecialStorage.GetScripts(storageRoot) : SpecialStorage.GetPluginScripts(storageRoot, manifest.Id);
            string? entryPoint = global ? null : Path.GetFileName(jsEntryPoint);

            return new PluginScriptHost(context, new ScriptStorage(storage), game.ScreenStack, global, entryPoint, scriptingSettings);
        }

        private void loadSystemPlugin(Storage storage)
        {
            if (Host == null)
                return;

            var manifest = new PluginManifest
            {
                Id = PluginManifest.SYSTEM_ID,
                Version = PluginApi.VERSION,
                ApiVersion = PluginApi.VERSION,
                Permissions = new PluginManifest.PluginPermissions { Trusted = true },
                DirectoryPath = SpecialStorage.GetPluginRoot(storage, PluginManifest.SYSTEM_ID).GetFullPath(string.Empty),
            };

            LoadedPlugin entry;

            try
            {
                entry = Host.LoadSystemPlugin(manifest);
            }
            catch (Exception e)
            {
                logger.Add($"failed to load system plugin: {e.Message}", LogLevel.Error);
                return;
            }

            SystemPlugin = entry;
            SystemPluginLoaded?.Invoke(entry);
        }

        private void startDiscovery(Storage storage)
        {
            GameServices services = Services!;

            loadCancellation = new CancellationTokenSource();
            CancellationToken token = loadCancellation.Token;

            Task.Run(() =>
            {
                try
                {
                    var repository = new PluginRepository(SpecialStorage.GetPlugins(storage), services.CreateLogFallback());
                    var loader = new PluginLoader(services.Output, services.CreatePluginLogger);

                    foreach (PluginLoadResult result in loader.LoadFrom(repository))
                    {
                        if (token.IsCancellationRequested)
                            break;

                        pendingLoads.Enqueue(result);
                    }
                }
                catch (Exception e)
                {
                    logger.Add($"failed to discover plugins: {e.Message}", LogLevel.Error);
                }
                finally
                {
                    if (!token.IsCancellationRequested)
                        discoveryCompleted = true;
                }
            }, token);
        }

        private void activateNextPlugin()
        {
            if (shutdown || disposed)
                return;

            if (pendingLoads.TryDequeue(out PluginLoadResult? result))
                Host?.Load(result);

            if (discoveryCompleted && pendingLoads.IsEmpty && !pluginsLoadedRaised)
            {
                pluginsLoadedRaised = true;
                PluginsLoaded?.Invoke();
            }
        }

        /// <summary>
        /// Stops discovery and unloads all plugins. Must be called on the update thread; subsequent
        /// calls are no-ops.
        /// </summary>
        public void Shutdown()
        {
            if (shutdown)
                return;

            shutdown = true;

            try
            {
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
            }
            catch (Exception e)
            {
                logger.Add($"failed to cancel plugin discovery: {e.Message}", LogLevel.Error);
            }
            finally
            {
                loadCancellation = null;
            }

            while (pendingLoads.TryDequeue(out _))
            {
            }

            Host?.Shutdown();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            if (!shutdown)
            {
                logger.Add("shutdown hook did not run; unloading plugins from Dispose (best effort)", LogLevel.Important);
                Shutdown();
            }
        }
    }
}
