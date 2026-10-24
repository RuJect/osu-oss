// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.API.Events;
using osu.Game.API.Services;
using osu.Game.IO;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Composition root of the plugin subsystem. Constructed from <c>osu.Desktop/Program.cs</c> around
    /// <c>host.Run(game)</c>; loads plugins from the <c>osu-oss/plugins</c> directory once the game has
    /// loaded and drives them on the update thread.
    /// </summary>
    public class PluginBootstrap : IDisposable
    {
        private readonly OsuGame game;
        private readonly ApiServices services;
        private readonly PluginHost pluginHost;

        private bool initialised;
        private bool disposed;

        public PluginHost Host => pluginHost;

        public PluginBootstrap(OsuGame game, ApiServices services, OsuEventBus events)
        {
            this.game = game;
            this.services = services;

            pluginHost = new PluginHost(services, events);

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

            try
            {
                Storage storage = game.Dependencies.Get<Storage>();
                var repository = new PluginRepository(SpecialStorage.GetPlugins(storage));
                var loader = new PluginLoader(services.Output);

                foreach (IPlugin plugin in loader.LoadFrom(repository))
                    pluginHost.Load(plugin);
            }
            catch (Exception e)
            {
                services.Output.Log(OutputLevel.Error, "plugins", $"failed to initialise plugins: {e.Message}");
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
