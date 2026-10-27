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
    /// Handler exceptions are isolated, logged and reported as <see cref="PluginErrorEvent"/>.
    /// </summary>
    public class PluginEventService : IDisposable
    {
        private readonly OsuEventBus bus;
        private readonly PluginManifest manifest;
        private readonly PluginLogger logger;
        private readonly Action<Exception>? onHandlerError;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public PluginEventService(OsuEventBus bus, PluginManifest manifest)
            : this(bus, manifest, new PluginLogger(manifest.Id))
        {
        }

        public PluginEventService(OsuEventBus bus, PluginManifest manifest, PluginLogger logger, Action<Exception>? onHandlerError = null)
        {
            this.bus = bus;
            this.manifest = manifest;
            this.logger = logger;
            this.onHandlerError = onHandlerError;
        }

        /// <summary>Subscribes to events of type <typeparamref name="TEvent"/>. Dispose the token to detach.</summary>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : OsuApiEvent
        {
            if (!manifest.Permissions.CanUseBus)
                return new DetachedSubscription();

            IDisposable token = bus.Subscribe<TEvent>(@event => invoke(handler, @event));
            subscriptions.Add(token);
            return token;
        }

        /// <summary>Subscribes to every event published on the bus. Dispose the token to detach.</summary>
        public IDisposable SubscribeAll(Action<OsuApiEvent> handler)
        {
            if (!manifest.Permissions.CanUseBus)
                return new DetachedSubscription();

            IDisposable token = bus.SubscribeAll(@event => invoke(handler, @event));
            subscriptions.Add(token);
            return token;
        }

        /// <summary>Publishes an event on the shared bus. A no-op without the "bus" permission.</summary>
        public void Publish<TEvent>(TEvent @event)
            where TEvent : OsuApiEvent
        {
            if (!manifest.Permissions.CanUseBus)
                return;

            bus.Publish(@event);
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
        }

        private void invoke<TEvent>(Action<TEvent> handler, TEvent @event)
            where TEvent : OsuApiEvent
        {
            try
            {
                handler(@event);
            }
            catch (Exception e)
            {
                handleException(e, @event is PluginErrorEvent);
            }
        }

        private void handleException(Exception exception, bool isPluginErrorEvent)
        {
            string message = $"event handler failed: {exception.Message}";

            logger.Error(message);
            onHandlerError?.Invoke(exception);

            if (!isPluginErrorEvent)
                bus.Publish(new PluginErrorEvent(manifest, message));
        }

        private class DetachedSubscription : IDisposable
        {
            public void Dispose() { }
        }
    }
}
