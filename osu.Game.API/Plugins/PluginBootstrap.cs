// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.Events;
using osu.Game.API.Services;
using osu.Game.IO;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Composition root of the plugin subsystem. Constructed from <c>osu.Desktop/Program.cs</c> around
    /// <c>host.Run(game)</c>; once the game has loaded, registers the built-in "system" plugin (which the
    /// scripting subsystem runs against) and loads plugins from <c>osu-oss/plugins</c>.
    /// </summary>
    /// <remarks>
    /// Must be constructed before <see cref="osu.Game.API.Scripting.ScriptingBootstrap"/>: both hook the
    /// game's load complete and handlers fire in registration order, so the system plugin is in place by
    /// the time scripting initialises.
    /// </remarks>
    public class PluginBootstrap : IDisposable
    {
        private readonly OsuGame game;
        private readonly ApiServices services;
        private readonly OsuEventBus events;
        private readonly PluginHost pluginHost;

        private bool initialised;
        private bool disposed;

        public PluginHost Host => pluginHost;

        public PluginBootstrap(OsuGame game, ApiServices services, OsuEventBus events)
        {
            this.game = game;
            this.services = services;
            this.events = events;

            pluginHost = new PluginHost(game, services, events);

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

            game.Add(new PluginRuntimeComponent(pluginHost));

            loadSystemPlugin();

            try
            {
                Storage storage = game.Dependencies.Get<Storage>();

                var repository = new PluginRepository(SpecialStorage.GetPlugins(storage));
                var loader = new PluginLoader(services.Output);

                foreach (PluginLoadResult result in loader.LoadFrom(repository))
                    pluginHost.Load(result);
            }
            catch (Exception e)
            {
                services.Output.Log(OutputLevel.Error, "plugins", $"failed to initialise plugins: {e.Message}");
            }
        }

        private void loadSystemPlugin()
        {
            try
            {
                Storage storage = game.Dependencies.Get<Storage>();

                var manifest = new PluginManifest
                {
                    Id = "system",
                    About = "Built-in plugin hosting the global JavaScript scripting environment.",
                    Permissions = new PluginManifest.PluginPermissions { Trusted = true },
                    DirectoryPath = SpecialStorage.GetScripts(storage).GetFullPath(string.Empty),
                };

                // The system plugin shares the scripts data directory: user scripts persist their files under
                // osu-oss/scripts/data, and the scripting console operates on the same storage.
                var systemStorage = new StorageService(new ScopedStorage(SpecialStorage.GetScriptData(storage)));

                var context = new PluginContext(
                    services,
                    new PluginEventService(events, manifest),
                    new SchedulerService(),
                    systemStorage,
                    new PluginLogger("system", systemStorage),
                    manifest);

                pluginHost.LoadSystemPlugin(manifest, context);
            }
            catch (Exception e)
            {
                services.Output.Log(OutputLevel.Error, "system", $"failed to load system plugin: {e.Message}");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            pluginHost.UnloadAll();
        }
    }
}
