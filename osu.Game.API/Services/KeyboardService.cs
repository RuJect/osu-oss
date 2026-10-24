// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Input.States;
using osuTK.Input;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Synthetic keyboard input and key listeners.
    /// </summary>
    public class KeyboardService
    {
        private const double default_press_duration = 50;

        private readonly ApiServices services;
        private readonly SchedulerService scheduler;
        private readonly List<Action<Key>> pressHandlers = new List<Action<Key>>();
        private readonly List<Action<Key>> releaseHandlers = new List<Action<Key>>();
        private readonly HashSet<Key> heldKeys = new HashSet<Key>();
        private readonly HashSet<Key> ownedKeys = new HashSet<Key>();
        private readonly List<PendingRelease> pendingReleases = new List<PendingRelease>();

        private ButtonStates<Key>? previousKeys;
        private int tickTimer = -1;

        public KeyboardService(ApiServices services, SchedulerService scheduler)
        {
            this.services = services;
            this.scheduler = scheduler;
        }

        /// <summary>Whether every key in <paramref name="keys"/> is currently pressed.</summary>
        public bool IsDown(Key[] keys)
        {
            InputService? input = currentInput();

            if (input == null)
                return false;

            foreach (Key key in keys)
            {
                if (!input.IsKeyPressed(key))
                    return false;
            }

            return true;
        }

        /// <summary>Holds the given key combination down until <see cref="Up"/>.</summary>
        public void Down(Key[] keys) => setPressed(keys, true);

        /// <summary>Releases the given key combination.</summary>
        public void Up(Key[] keys) => setPressed(keys, false);

        /// <summary>
        /// Presses and releases the given keys after <paramref name="duration"/> milliseconds (50ms by default).
        /// Awaitable; completes when the keys are released, including by an early <see cref="Up"/>.
        /// </summary>
        public Task Press(Key[] keys, double? duration)
        {
            InputService? input = currentInput();

            if (input == null)
                return Task.CompletedTask;

            press(input, keys);

            double delay = duration ?? default_press_duration;

            if (delay <= 0)
            {
                release(input, keys);
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>();
            pendingReleases.Add(new PendingRelease(keys, scheduler.Elapsed + delay, completion));
            ensureTimer();
            return completion.Task;
        }

        /// <summary>Registers a callback invoked with the key when a key is pressed.</summary>
        public void OnPress(Action<Key> handler)
        {
            if (currentInput() == null)
                return;

            pressHandlers.Add(handler);
            ensureTimer();
        }

        /// <summary>Registers a callback invoked with the key when a key is released.</summary>
        public void OnRelease(Action<Key> handler)
        {
            if (currentInput() == null)
                return;

            releaseHandlers.Add(handler);
            ensureTimer();
        }

        /// <summary>Releases any keys still held by this instance. Called on teardown.</summary>
        public void ReleaseOwned()
        {
            // shortcut: input cannot be changed off the update thread; only affects shutdown, where it does not matter.
            InputService? input = currentInput();

            if (input == null)
                return;

            foreach (Key key in heldKeys.ToArray())
            {
                if (ownedKeys.Contains(key))
                    input.SetKeyPressed(key, false);
            }

            heldKeys.Clear();
            ownedKeys.Clear();
            pendingReleases.Clear();
        }

        private void setPressed(Key[] keys, bool pressed)
        {
            InputService? input = currentInput();

            if (input == null)
                return;

            if (pressed)
                press(input, keys);
            else
            {
                release(input, keys);
                completePending(keys);
            }
        }

        private void press(InputService input, Key[] keys)
        {
            foreach (Key key in keys)
            {
                if (!input.IsKeyPressed(key))
                    ownedKeys.Add(key);

                input.SetKeyPressed(key, true);
                heldKeys.Add(key);
            }
        }

        private void release(InputService input, Key[] keys)
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

            tickTimer = scheduler.AddTimer(tickState, 0, true);
        }

        private void cancelTimer()
        {
            if (tickTimer < 0)
                return;

            scheduler.CancelTimer(tickTimer);
            tickTimer = -1;
            previousKeys = null;
        }

        private void tickState()
        {
            InputService? input = currentInput();

            if (input == null)
            {
                cancelTimer();
                return;
            }

            if (pressHandlers.Count > 0 || releaseHandlers.Count > 0)
                poll(input);

            double now = scheduler.Elapsed;

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

        private void poll(InputService input)
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

        private static void invoke(List<Action<Key>> handlers, Key key)
        {
            foreach (Action<Key> handler in handlers.ToArray())
                handler(key);
        }

        private InputService? currentInput() => InputService.FromServices(services);

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
