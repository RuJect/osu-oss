// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;
using osu.Framework.Input.States;
using osuTK.Input;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides synthetic keyboard input and key listeners via <c>osu.keyboard</c>.
    /// </summary>
    public class KeyboardApiProvider : ScriptApiProvider
    {
        private const double default_press_duration = 50;

        private readonly List<JsValue> pressHandlers = new List<JsValue>();
        private readonly List<JsValue> releaseHandlers = new List<JsValue>();
        private readonly HashSet<Key> heldKeys = new HashSet<Key>();
        private readonly HashSet<Key> ownedKeys = new HashSet<Key>();
        private readonly List<PendingRelease> pendingReleases = new List<PendingRelease>();

        private ButtonStates<Key>? previousKeys;
        private int tickTimer = -1;

        /// <summary>Whether every key in <paramref name="keys"/> is currently pressed.</summary>
        [ScriptApi(Target = "keyboard")]
        public bool IsDown(JsValue keys)
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
                return false;

            foreach (Key key in ScriptKeyParser.Parse(keys))
            {
                if (!input.IsKeyPressed(key))
                    return false;
            }

            return true;
        }

        /// <summary>Holds the given key or key combination down until <c>osu.keyboard.up</c>.</summary>
        [ScriptApi(Target = "keyboard")]
        public void Down(JsValue keys) => setPressed(keys, true);

        /// <summary>Releases the given key or key combination.</summary>
        [ScriptApi(Target = "keyboard")]
        public void Up(JsValue keys) => setPressed(keys, false);

        /// <summary>
        /// Presses and releases the given key or combination after <paramref name="duration"/> milliseconds
        /// (50ms by default). Awaitable; completes when the key is released, including by an early
        /// <c>osu.keyboard.up</c>.
        /// </summary>
        [ScriptApi(Target = "keyboard")]
        public Task Press(JsValue keys, JsValue duration)
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
                return Task.CompletedTask;

            Key[] parsed = ScriptKeyParser.Parse(keys);
            press(input, parsed);

            double delay = duration.IsNumber() ? Math.Max(0, duration.AsNumber()) : default_press_duration;

            if (delay <= 0)
            {
                release(input, parsed);
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>();
            pendingReleases.Add(new PendingRelease(parsed, Context.Instance.Scheduler.Elapsed + delay, completion));
            ensureTimer();
            return completion.Task;
        }

        /// <summary>Registers a callback invoked with the key name when a key is pressed.</summary>
        [ScriptApi(Target = "keyboard")]
        public void OnPress(JsValue handler)
        {
            if (!handler.IsCallable() || ScriptInput.FromServices(Context.Services) == null)
                return;

            pressHandlers.Add(handler);
            ensureTimer();
        }

        /// <summary>Registers a callback invoked with the key name when a key is released.</summary>
        [ScriptApi(Target = "keyboard")]
        public void OnRelease(JsValue handler)
        {
            if (!handler.IsCallable() || ScriptInput.FromServices(Context.Services) == null)
                return;

            releaseHandlers.Add(handler);
            ensureTimer();
        }

        protected override void OnRegistered(ScriptApiContext context)
        {
            context.Instance.OnTearDown(() =>
            {
                // shortcut: input cannot be changed off the update thread; only affects shutdown, where it does not matter.
                ScriptInput? input = ScriptInput.FromServices(context.Services);

                if (input != null)
                    releaseOwned(input);
            });
        }

        private void setPressed(JsValue keys, bool pressed)
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
                return;

            Key[] parsed = ScriptKeyParser.Parse(keys);

            if (pressed)
                press(input, parsed);
            else
            {
                release(input, parsed);
                completePending(parsed);
            }
        }

        private void press(ScriptInput input, Key[] keys)
        {
            foreach (Key key in keys)
            {
                if (!input.IsKeyPressed(key))
                    ownedKeys.Add(key);

                input.SetKeyPressed(key, true);
                heldKeys.Add(key);
            }
        }

        private void release(ScriptInput input, Key[] keys)
        {
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                Key key = keys[i];

                if (!heldKeys.Remove(key))
                    continue;

                if (ownedKeys.Remove(key))
                    input.SetKeyPressed(key, false);
            }
        }

        private void releaseOwned(ScriptInput input)
        {
            foreach (Key key in heldKeys.ToArray())
            {
                if (ownedKeys.Contains(key))
                    input.SetKeyPressed(key, false);
            }

            heldKeys.Clear();
            ownedKeys.Clear();
            pendingReleases.Clear();
        }

        private void completePending(Key[] keys)
        {
            for (int i = pendingReleases.Count - 1; i >= 0; i--)
            {
                PendingRelease pending = pendingReleases[i];

                if (!pending.Keys.Any(keys.Contains))
                    continue;

                pendingReleases.RemoveAt(i);
                pending.Completion.TrySetResult(true);
            }

            if (pressHandlers.Count == 0 && releaseHandlers.Count == 0 && pendingReleases.Count == 0)
                cancelTimer();
        }

        private void ensureTimer()
        {
            if (tickTimer >= 0)
                return;

            JsValue tick = new ClrFunction(Context.Engine, "keyboardTick", (_, _) =>
            {
                tickState();
                return JsValue.Undefined;
            });

            tickTimer = Context.Instance.Scheduler.AddTimer(tick, 0, true);
        }

        private void cancelTimer()
        {
            if (tickTimer < 0)
                return;

            Context.Instance.Scheduler.CancelTimer(tickTimer);
            tickTimer = -1;
            previousKeys = null;
        }

        private void tickState()
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
            {
                cancelTimer();
                return;
            }

            if (pressHandlers.Count > 0 || releaseHandlers.Count > 0)
                poll(input);

            double now = Context.Instance.Scheduler.Elapsed;

            for (int i = pendingReleases.Count - 1; i >= 0; i--)
            {
                PendingRelease pending = pendingReleases[i];

                if (pending.At > now)
                    continue;

                pendingReleases.RemoveAt(i);
                release(input, pending.Keys);
                pending.Completion.TrySetResult(true);
            }

            if (pressHandlers.Count == 0 && releaseHandlers.Count == 0 && pendingReleases.Count == 0)
                cancelTimer();
        }

        private void poll(ScriptInput input)
        {
            if (previousKeys == null)
            {
                previousKeys = input.Keys.Clone();
                return;
            }

            var difference = input.Keys.EnumerateDifference(previousKeys);
            previousKeys.Set(input.Keys);

            foreach (Key key in difference.Pressed)
                invoke(pressHandlers, key);

            foreach (Key key in difference.Released)
                invoke(releaseHandlers, key);
        }

        private void invoke(List<JsValue> handlers, Key key)
        {
            foreach (JsValue handler in handlers.ToArray())
                Context.Engine.Invoke(handler, key.ToString());
        }

        private readonly struct PendingRelease
        {
            public readonly Key[] Keys;
            public readonly double At;
            public readonly TaskCompletionSource<bool> Completion;

            public PendingRelease(Key[] keys, double at, TaskCompletionSource<bool> completion)
            {
                Keys = keys;
                At = at;
                Completion = completion;
            }
        }
    }
}
