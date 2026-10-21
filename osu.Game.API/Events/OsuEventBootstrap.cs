// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.Events;

namespace osu.Game.API.Events
{
    /// <summary>
    /// Composition root for the game event subsystem. Constructed from <c>osu.Desktop/Program.cs</c> around
    /// <c>host.Run(game)</c>, it owns the <see cref="OsuEventBus"/> and wires the bridge which forwards
    /// <see cref="GameEventHub"/> signals into it.
    /// </summary>
    /// <remarks>
    /// This subsystem is intentionally independent from the scripting module.
    /// </remarks>
    public class OsuEventBootstrap : IDisposable
    {
        private readonly GameHost host;
        private readonly OsuGame game;

        private GameEventBridge? bridge;

        private bool initialised;
        private bool disposed;

        /// <summary>
        /// The event bus that plugins and other consumers can subscribe to.
        /// </summary>
        public OsuEventBus Bus { get; } = new OsuEventBus();

        public OsuEventBootstrap(GameHost host, OsuGame game)
        {
            this.host = host;
            this.game = game;

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

            GameEventHub? hub = game.Dependencies.Get<GameEventHub>();

            if (hub != null)
                bridge = new GameEventBridge(hub, Bus);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            bridge?.Dispose();
            bridge = null;
        }
    }
}
