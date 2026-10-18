// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Stores the event subscriptions registered by scripts. Invocation and lifecycle checks are performed
    /// by <see cref="ScriptRunner"/>, which owns the running instances.
    /// </summary>
    public class ScriptEventBus
    {
        private readonly Dictionary<string, List<EventSubscription>> subscriptions = new Dictionary<string, List<EventSubscription>>();

        public void Subscribe(ScriptInstance instance, string eventName, JsValue handler)
        {
            if (!subscriptions.TryGetValue(eventName, out List<EventSubscription>? list))
                subscriptions[eventName] = list = new List<EventSubscription>();

            list.Add(new EventSubscription(instance, handler));
        }

        public void Unsubscribe(ScriptInstance instance, string eventName, JsValue handler)
        {
            if (subscriptions.TryGetValue(eventName, out List<EventSubscription>? list))
            {
                list.RemoveAll(s => s.Instance == instance && s.Handler == handler);

                if (list.Count == 0)
                    subscriptions.Remove(eventName);
            }
        }

        /// <summary>Removes every subscription belonging to <paramref name="instance"/>.</summary>
        public void RemoveFor(ScriptInstance instance)
        {
            foreach (List<EventSubscription> list in subscriptions.Values)
                list.RemoveAll(s => s.Instance == instance);
        }

        /// <summary>Whether <paramref name="instance"/> currently has any event subscriptions.</summary>
        public bool HasSubscriptions(ScriptInstance instance)
        {
            foreach (List<EventSubscription> list in subscriptions.Values)
            {
                if (list.Any(s => s.Instance == instance))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a snapshot of the handlers registered for <paramref name="eventName"/>, safe to
        /// iterate while the script set is being mutated.
        /// </summary>
        internal EventSubscription[] HandlersFor(string eventName)
            => subscriptions.TryGetValue(eventName, out List<EventSubscription>? list) ? list.ToArray() : Array.Empty<EventSubscription>();

        internal readonly struct EventSubscription
        {
            public readonly ScriptInstance Instance;
            public readonly JsValue Handler;

            public EventSubscription(ScriptInstance instance, JsValue handler)
            {
                Instance = instance;
                Handler = handler;
            }
        }
    }
}
