// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.API.Events;
using osu.Game.Events;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Per-plugin gate over the shared <see cref="OsuEventBus"/>. Bus access is granted by the
    /// plugin manifest ("bus" permission, or <see cref="PluginManifest.PluginPermissions.Trusted"/>).
    /// Tracks every subscription it creates so surviving handlers can be detached on unload.
    /// </summary>
    public class PluginEventService : IDisposable
    {
        private readonly OsuEventBus bus;
        private readonly PluginManifest manifest;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public PluginEventService(OsuEventBus bus, PluginManifest manifest)
        {
            this.bus = bus;
            this.manifest = manifest;
        }

        /// <summary>Signals the load lifecycle around <see cref="PluginBase.Load"/>.</summary>
        public void LoadPlugin(PluginBase plugin, PluginContext context)
        {
            bus.Publish(new PluginLoadingEvent(manifest));

            plugin.Load(context);

            bus.Publish(new PluginLoadedEvent(manifest));
        }

        /// <summary>
        /// Signals the unload lifecycle around <see cref="PluginBase.Unload"/>, then detaches every
        /// subscription created through this service so unloaded plugins go silent.
        /// </summary>
        public void UnloadPlugin(PluginBase plugin)
        {
            bus.Publish(new PluginUnloadingEvent(manifest));

            try
            {
                plugin.Unload();
            }
            finally
            {
                foreach (IDisposable subscription in subscriptions)
                    subscription.Dispose();

                subscriptions.Clear();

                bus.Publish(new PluginUnloadedEvent(manifest));
            }
        }

        /// <summary>Subscribes to events of type <typeparamref name="TEvent"/>. Dispose the token to detach.</summary>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : OsuApiEvent
        {
            if (!manifest.Permissions.HasPermission("bus"))
                return new DetachedSubscription();

            IDisposable token = bus.Subscribe(handler);
            subscriptions.Add(token);
            return token;
        }

        /// <summary>Subscribes to every event published on the bus. Dispose the token to detach.</summary>
        public IDisposable SubscribeAll(Action<OsuApiEvent> handler)
        {
            if (!manifest.Permissions.HasPermission("bus"))
                return new DetachedSubscription();

            IDisposable token = bus.SubscribeAll(handler);
            subscriptions.Add(token);
            return token;
        }

        /// <summary>Publishes an event on the shared bus. A no-op without the "bus" permission.</summary>
        public void Publish<TEvent>(TEvent @event)
            where TEvent : OsuApiEvent
        {
            if (!manifest.Permissions.HasPermission("bus"))
                return;

            bus.Publish(@event);
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
        }

        private class DetachedSubscription : IDisposable
        {
            public void Dispose() { }
        }
    }
}
