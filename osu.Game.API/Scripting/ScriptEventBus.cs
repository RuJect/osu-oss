// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using osu.Game.API.Events;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Adapter that exposes the shared <see cref="OsuEventBus"/> to scripts as string-named subscriptions.
    /// Well-known names map to typed events published on the bus; unknown names resolve to custom
    /// <see cref="ScriptEvent"/>s emitted via <c>osu.emit</c>.
    /// </summary>
    public class ScriptEventBus
    {
        private static readonly Dictionary<string, EventMapping> mappings = new Dictionary<string, EventMapping>
        {
            ["screen"] = new EventMapping(typeof(ScreenChangedEvent), e => new object?[] { ((ScreenChangedEvent)e).Name }),
            ["scriptStarted"] = new EventMapping(typeof(ScriptStartedEvent), e => new object?[] { ((ScriptStartedEvent)e).Name }),
            ["scriptStopped"] = new EventMapping(typeof(ScriptStoppedEvent), e => new object?[] { ((ScriptStoppedEvent)e).Name }),
            ["scriptError"] = new EventMapping(typeof(ScriptErrorEvent), e => new object?[] { ((ScriptErrorEvent)e).Name, ((ScriptErrorEvent)e).Message }),
            ["beatmapSelected"] = new EventMapping(typeof(BeatmapSelectedEvent), e => new object?[] { ((BeatmapSelectedEvent)e).Beatmap }),
            ["selectedModsChanged"] = new EventMapping(typeof(SelectedModsChangedEvent), e => new object?[] { ((SelectedModsChangedEvent)e).Mods }),
            ["rulesetChanged"] = new EventMapping(typeof(RulesetChangedEvent), e => new object?[] { ((RulesetChangedEvent)e).Ruleset }),
            ["beatmapDownloaded"] = new EventMapping(typeof(BeatmapDownloadedEvent), e => new object?[] { ((BeatmapDownloadedEvent)e).BeatmapSet }),
            ["beatmapDeleted"] = new EventMapping(typeof(BeatmapDeletedEvent), e => new object?[] { ((BeatmapDeletedEvent)e).BeatmapSet }),
            ["gameplayLoading"] = new EventMapping(typeof(GameplayLoadingEvent), e => new object?[] { ((GameplayLoadingEvent)e).Player }),
            ["gameplayLoaded"] = new EventMapping(typeof(GameplayLoadedEvent), e => new object?[] { ((GameplayLoadedEvent)e).Player }),
            ["gameplayPaused"] = new EventMapping(typeof(GameplayPausedEvent), e => new object?[] { ((GameplayPausedEvent)e).Player }),
            ["gameplayResumed"] = new EventMapping(typeof(GameplayResumedEvent), e => new object?[] { ((GameplayResumedEvent)e).Player }),
            ["gameplayExited"] = new EventMapping(typeof(GameplayExitedEvent), e => new object?[] { ((GameplayExitedEvent)e).Player }),
            ["gameplayRestarted"] = new EventMapping(typeof(GameplayRestartedEvent), e => new object?[] { ((GameplayRestartedEvent)e).Player }),
            ["gameplayPassed"] = new EventMapping(typeof(GameplayPassedEvent), e => new object?[] { ((GameplayPassedEvent)e).Player, ((GameplayPassedEvent)e).Score }),
            ["gameplayFailed"] = new EventMapping(typeof(GameplayFailedEvent), e => new object?[] { ((GameplayFailedEvent)e).Player, ((GameplayFailedEvent)e).Score }),
        };

        private readonly OsuEventBus bus;
        private readonly Dictionary<ScriptInstance, List<Subscription>> subscriptions = new Dictionary<ScriptInstance, List<Subscription>>();

        /// <summary>Invoked when a script handler throws; set by <see cref="ScriptRunner"/>.</summary>
        public Action<ScriptInstance, Exception>? OnHandlerError { get; set; }

        public ScriptEventBus(OsuEventBus bus)
        {
            this.bus = bus;
        }

        public void Subscribe(ScriptInstance instance, string eventName, JsValue handler) => subscribe(instance, eventName, handler, false);

        /// <summary>Subscribes <paramref name="handler"/> for a single invocation, after which it is removed.</summary>
        public void SubscribeOnce(ScriptInstance instance, string eventName, JsValue handler) => subscribe(instance, eventName, handler, true);

        private void subscribe(ScriptInstance instance, string eventName, JsValue handler, bool once)
        {
            if (mappings.TryGetValue(eventName, out EventMapping? mapping))
                track(instance, handler, bus.SubscribeAll(e => dispatch(instance, handler, e.GetType() == mapping.Type ? mapping.Project(e) : null, once)));
            else
                track(instance, handler, bus.Subscribe<ScriptEvent>(e => dispatch(instance, handler, e.Name == eventName ? e.Args : null, once)));
        }

        private void dispatch(ScriptInstance instance, JsValue handler, object?[]? args, bool once)
        {
            if (args == null || instance.State != ScriptExecutionState.Running)
                return;

            try
            {
                instance.Engine.Invoke(handler, args);
                instance.Engine.Advanced.ProcessTasks();
            }
            catch (Exception e)
            {
                OnHandlerError?.Invoke(instance, e);
            }

            if (once)
                Unsubscribe(instance, handler);
        }

        public void Unsubscribe(ScriptInstance instance, string eventName, JsValue handler) => Unsubscribe(instance, handler);

        private void Unsubscribe(ScriptInstance instance, JsValue handler)
        {
            if (!subscriptions.TryGetValue(instance, out List<Subscription>? list))
                return;

            foreach (Subscription subscription in list.Where(s => s.Handler == handler).ToArray())
            {
                subscription.Token.Dispose();
                list.Remove(subscription);
            }

            if (list.Count == 0)
                subscriptions.Remove(instance);
        }

        /// <summary>Removes every subscription belonging to <paramref name="instance"/>.</summary>
        public void RemoveFor(ScriptInstance instance)
        {
            if (!subscriptions.TryGetValue(instance, out List<Subscription>? list))
                return;

            foreach (Subscription subscription in list)
                subscription.Token.Dispose();

            subscriptions.Remove(instance);
        }

        /// <summary>Whether <paramref name="instance"/> currently has any event subscriptions.</summary>
        public bool HasSubscriptions(ScriptInstance instance)
            => subscriptions.TryGetValue(instance, out List<Subscription>? list) && list.Count > 0;

        /// <summary>Publishes a custom script event (from <c>osu.emit</c>).</summary>
        public void Emit(string eventName, object?[] args) => bus.Publish(new ScriptEvent(eventName, args));

        public void PublishScreen(string name) => bus.Publish(new ScreenChangedEvent(name));

        public void PublishScriptStarted(string name) => bus.Publish(new ScriptStartedEvent(name));

        public void PublishScriptStopped(string name) => bus.Publish(new ScriptStoppedEvent(name));

        public void PublishScriptError(string name, string message) => bus.Publish(new ScriptErrorEvent(name, message));

        private void track(ScriptInstance instance, JsValue handler, IDisposable token)
        {
            if (!subscriptions.TryGetValue(instance, out List<Subscription>? list))
                subscriptions[instance] = list = new List<Subscription>();

            list.Add(new Subscription(handler, token));
        }

        private sealed class EventMapping
        {
            public readonly Type Type;
            public readonly Func<OsuApiEvent, object?[]> Project;

            public EventMapping(Type type, Func<OsuApiEvent, object?[]> project)
            {
                Type = type;
                Project = project;
            }
        }

        private readonly struct Subscription
        {
            public readonly JsValue Handler;
            public readonly IDisposable Token;

            public Subscription(JsValue handler, IDisposable token)
            {
                Handler = handler;
                Token = token;
            }
        }
    }
}
