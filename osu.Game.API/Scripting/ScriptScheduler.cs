// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Holds the frame callbacks and timers registered by a single script instance.
    /// Executed by <see cref="ScriptRunner.Tick"/> on the game's update thread.
    /// </summary>
    internal class ScriptScheduler
    {
        private readonly List<JsValue> updateHandlers = new List<JsValue>();
        private readonly List<JsTimer> timers = new List<JsTimer>();

        public void AddUpdateHandler(JsValue handler) => updateHandlers.Add(handler);

        public void AddTimer(JsValue handler, double interval, bool repeat)
        {
            if (interval < 0)
                interval = 0;

            timers.Add(new JsTimer(handler, interval, repeat));
        }

        /// <summary>Whether any frame callbacks or timers are still registered.</summary>
        public bool HasPendingCallbacks => updateHandlers.Count > 0 || timers.Count > 0;

        public void Tick(Engine engine, double currentTime, Action<Exception> onError)
        {
            foreach (JsValue handler in updateHandlers.ToArray())
                invoke(engine, handler, onError);

            for (int i = timers.Count - 1; i >= 0; i--)
            {
                JsTimer timer = timers[i];

                if (currentTime < timer.NextRun)
                    continue;

                if (timer.Repeat)
                    timer.NextRun = currentTime + timer.Interval;
                else
                    timers.RemoveAt(i);

                invoke(engine, timer.Handler, onError);
            }
        }

        private static void invoke(Engine engine, JsValue handler, Action<Exception> onError)
        {
            try
            {
                engine.Invoke(handler);
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

        private class JsTimer
        {
            public readonly JsValue Handler;
            public readonly double Interval;
            public readonly bool Repeat;

            public double NextRun;

            public JsTimer(JsValue handler, double interval, bool repeat)
            {
                Handler = handler;
                Interval = interval;
                Repeat = repeat;
            }
        }
    }
}
