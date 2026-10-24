// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Holds the frame callbacks and timers registered by a single script or plugin.
    /// Executed by the owning component on the game's update thread.
    /// </summary>
    public class SchedulerService
    {
        private readonly List<Action> updateHandlers = new List<Action>();
        private readonly List<Timer> timers = new List<Timer>();

        private int nextTimerId;
        private double timeBase;
        private bool timeBaseSet;
        private double elapsed;

        /// <summary>Time elapsed since the scheduler's first tick, in milliseconds.</summary>
        public double Elapsed => elapsed;

        public void AddUpdateHandler(Action handler) => updateHandlers.Add(handler);

        /// <summary>Registers a timer and returns its id, used to cancel it via <see cref="CancelTimer"/>.</summary>
        public int AddTimer(Action handler, double interval, bool repeat)
        {
            if (interval < 0)
                interval = 0;

            int id = ++nextTimerId;
            timers.Add(new Timer(id, handler, interval, repeat, elapsed + interval));
            return id;
        }

        /// <summary>Cancels the timer with the given id. Returns <see langword="false"/> if no such timer exists.</summary>
        public bool CancelTimer(int id)
        {
            int index = timers.FindIndex(t => t.Id == id);

            if (index < 0)
                return false;

            timers.RemoveAt(index);
            return true;
        }

        /// <summary>Whether any frame callbacks or timers are still registered.</summary>
        public bool HasPendingCallbacks => updateHandlers.Count > 0 || timers.Count > 0;

        public void Tick(double currentTime, Action<Exception> onError)
        {
            // Anchor the scheduler's clock to its first tick so timer countdowns are relative to the script's
            // lifetime rather than the game's absolute clock (which would fire them immediately).
            if (!timeBaseSet)
            {
                timeBase = currentTime;
                timeBaseSet = true;
            }

            elapsed = currentTime - timeBase;

            foreach (Action handler in updateHandlers.ToArray())
                invoke(handler, onError);

            for (int i = timers.Count - 1; i >= 0; i--)
            {
                Timer timer = timers[i];

                if (elapsed < timer.NextRun)
                    continue;

                if (timer.Repeat)
                    timer.NextRun = elapsed + timer.Interval;
                else
                    timers.RemoveAt(i);

                invoke(timer.Handler, onError);
            }
        }

        private static void invoke(Action handler, Action<Exception> onError)
        {
            try
            {
                handler();
            }
            catch (Exception e)
            {
                onError(e);
            }
        }

        public void Clear()
        {
            updateHandlers.Clear();
            timers.Clear();
        }

        private class Timer
        {
            public readonly int Id;
            public readonly Action Handler;
            public readonly double Interval;
            public readonly bool Repeat;

            public double NextRun;

            public Timer(int id, Action handler, double interval, bool repeat, double nextRun)
            {
                Id = id;
                Handler = handler;
                Interval = interval;
                Repeat = repeat;
                NextRun = nextRun;
            }
        }
    }
}
