// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Development;
using osu.Framework.Threading;

namespace osu.Game.API.Events
{
    /// <summary>
    /// A strongly-typed event bus for game-wide events. Independent from the scripting subsystem.
    /// </summary>
    /// <remarks>
    /// Handlers are always invoked on the update thread; publishing from another thread is marshalled via the
    /// supplied <see cref="Scheduler"/>.
    /// </remarks>
    public class OsuEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();
        private readonly List<Action<OsuApiEvent>> globalHandlers = new List<Action<OsuApiEvent>>();

        /// <summary>
        /// The scheduler used to marshal <see cref="Publish{TEvent}"/> calls made from a non-update thread.
        /// Assigned once the game is loaded; publishing is done directly before then (during construction no
        /// subscribers can exist yet).
        /// </summary>
        public Scheduler? Scheduler { get; set; }

        /// <summary>
        /// Subscribes to events of type <typeparamref name="TEvent"/>.
        /// Dispose the returned token to unsubscribe.
        /// </summary>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : OsuApiEvent
        {
            var type = typeof(TEvent);

            if (!handlers.TryGetValue(type, out List<Delegate>? list))
                handlers[type] = list = new List<Delegate>();

            list.Add(handler);

            return new Subscription(() => list.Remove(handler));
        }

        /// <summary>
        /// Subscribes to every event published on this bus, regardless of type.
        /// Dispose the returned token to unsubscribe.
        /// </summary>
        public IDisposable SubscribeAll(Action<OsuApiEvent> handler)
        {
            globalHandlers.Add(handler);
            return new Subscription(() => globalHandlers.Remove(handler));
        }

        /// <summary>
        /// Publishes <paramref name="event"/> to all matching subscribers. Safe to call from any thread.
        /// </summary>
        public void Publish<TEvent>(TEvent @event)
            where TEvent : OsuApiEvent
        {
            if (!ThreadSafety.IsUpdateThread && Scheduler != null)
            {
                Scheduler.Add(() => publish(@event));
                return;
            }

            publish(@event);
        }

        private void publish<TEvent>(TEvent @event)
            where TEvent : OsuApiEvent
        {
            if (handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list))
            {
                foreach (Delegate handler in list.ToArray())
                    ((Action<TEvent>)handler)(@event);
            }

            foreach (Action<OsuApiEvent> handler in globalHandlers.ToArray())
                handler(@event);
        }

        private class Subscription : IDisposable
        {
            private Action? dispose;

            public Subscription(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose() => dispose?.Invoke();
        }
    }
}
