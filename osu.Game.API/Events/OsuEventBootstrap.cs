// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Events;
using osu.Game.Online.Server;
using osu.Game.Overlays;
using osu.Game.API.Services;

namespace osu.Game.API.Events
{
    /// <summary>
    /// Composition root for the game event and shared service subsystem. Constructed from
    /// <c>osu.Desktop/Program.cs</c> around <c>host.Run(game)</c>, it owns the <see cref="OsuEventBus"/>
    /// and the <see cref="ApiServices"/> bundle consumed by plugins and scripts.
    /// </summary>
    public class OsuEventBootstrap : IDisposable
    {
        private readonly GameHost host;
        private readonly OsuGame game;

        private bool initialised;
        private bool disposed;

        /// <summary>
        /// The event bus that plugins and other consumers can subscribe to.
        /// </summary>
        public OsuEventBus Bus { get; } = new OsuEventBus();

        /// <summary>The shared services handed to plugins and scripts.</summary>
        public ApiServices Services { get; }

        public OsuEventBootstrap(GameHost host, OsuGame game)
        {
            this.host = host;
            this.game = game;

            // Created eagerly: consumers such as the plugin bootstrap receive this instance before the
            // game (and therefore OnLoadComplete) exists.
            Services = new ApiServices(game, host, new OutputLog());

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

            // The update thread (and its scheduler) only exists once the host has started running.
            Bus.Scheduler = host.UpdateThread?.Scheduler;

            // Resolve the shared game services here (before any plugin loads) so plugin contexts expose
            // them right away.
            Services.Music = new MusicService(game.Dependencies.Get<MusicController>(), host);
            Services.Config = new ConfigService(game.Dependencies.Get<OsuConfigManager>());
            Services.Notifications = new NotificationService(game.Dependencies.Get<INotificationOverlay>());
            Services.Servers = game.Dependencies.Get<IServerRegistry>();
            Services.ServerManager = game.Dependencies.Get<IServerManager>();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
        }
    }
}
