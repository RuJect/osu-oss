// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;
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
    /// "system" plugin (which the scripting subsystem runs against) and loads plugins from <c>osu-oss/plugins</c>.
    /// </summary>
    /// <remarks>
    /// Must be constructed before <c>ScriptingBootstrap</c>: both hook the
    /// game's load complete and handlers fire in registration order, so the system plugin is in place by
    /// the time scripting initialises.
    /// </remarks>
    public class PluginBootstrap : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("plugins");

        private readonly OsuGame game;
        private readonly GameHost host;
        private readonly OsuEventBus events;

        private InputManagerProvider? input;
        private bool initialised;
        private bool disposed;

        public PluginHost? Host { get; private set; }

        public GameServices? Services { get; private set; }

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
            if (initialised || disposed)
                return;

            initialised = true;

            try
            {
                Storage storage = game.Dependencies.Get<Storage>();
                input = new InputManagerProvider();

                Services = new GameServices(
                    game, host, new OutputLog(),
                    storage,
                    SpecialStorage.GetScripts(storage),
                    SpecialStorage.GetScriptData(storage),
                    game.Dependencies.Get<MusicController>(),
                    game.Dependencies.Get<OsuConfigManager>(),
                    game.Dependencies.Get<INotificationOverlay>(),
                    game.Dependencies.Get<IServerRegistry>(),
                    game.Dependencies.Get<IServerManager>(),
                    () => input.Value);

                Host = new PluginHost(events, Services.CreatePluginServices);

                game.Add(new PluginRuntimeComponent(Host, input));

                loadSystemPlugin(storage);

                var repository = new PluginRepository(SpecialStorage.GetPlugins(storage));
                var loader = new PluginLoader(Services.Output);

                foreach (PluginLoadResult result in loader.LoadFrom(repository))
                    Host.Load(result);
            }
            catch (Exception e)
            {
                logger.Add($"failed to initialise plugins: {e.Message}", LogLevel.Error);
            }
        }

        private void loadSystemPlugin(Storage storage)
        {
            var manifest = new PluginManifest
            {
                Id = PluginManifest.SYSTEM_ID,
                Permissions = new PluginManifest.PluginPermissions { Trusted = true },
                DirectoryPath = SpecialStorage.GetScripts(storage).GetFullPath(string.Empty),
            };

            Host!.LoadSystemPlugin(manifest);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Host?.UnloadAll();
        }
    }
}
