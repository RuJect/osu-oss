// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides frame/interval scheduling callbacks: <c>osu.onUpdate</c>, <c>osu.every</c>, <c>osu.after</c> and <c>osu.cancel</c>.
    /// </summary>
    public class SchedulerApiProvider : ScriptApiProvider
    {
        [ScriptApi]
        public void OnUpdate(JsValue handler)
        {
            if (handler.IsCallable())
                Context.Instance.Scheduler.AddUpdateHandler(handler);
        }

        [ScriptApi]
        public double Every(JsValue first, JsValue second) => registerTimer(first, second, true);

        [ScriptApi]
        public double After(JsValue first, JsValue second) => registerTimer(first, second, false);

        [ScriptApi]
        public void Cancel(double id) => Context.Instance.Scheduler.CancelTimer((int)id);

        private double registerTimer(JsValue first, JsValue second, bool repeat)
        {
            JsValue handler;
            double interval;

            if (first.IsCallable())
            {
                handler = first;
                interval = second.IsNumber() ? second.AsNumber() : 0;
            }
            else if (second.IsCallable())
            {
                interval = first.IsNumber() ? first.AsNumber() : 0;
                handler = second;
            }
            else
            {
                return -1;
            }

            return Context.Instance.Scheduler.AddTimer(handler, interval, repeat);
        }
    }
}
