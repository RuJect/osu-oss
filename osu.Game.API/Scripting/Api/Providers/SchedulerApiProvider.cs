// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides frame/interval scheduling callbacks: <c>osu.onUpdate</c>, <c>osu.every</c> and <c>osu.after</c>.
    /// </summary>
    public class SchedulerApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            context.AddFunction(context.Api, "onUpdate", (_, args) =>
            {
                if (args.Length >= 1 && args[0].IsCallable())
                    context.Instance.Scheduler.AddUpdateHandler(args[0]);

                return JsValue.Undefined;
            });

            context.AddFunction(context.Api, "every", (_, args) =>
            {
                registerTimer(context, args, true);
                return JsValue.Undefined;
            });

            context.AddFunction(context.Api, "after", (_, args) =>
            {
                registerTimer(context, args, false);
                return JsValue.Undefined;
            });
        }

        private static void registerTimer(ScriptApiContext context, JsValue[] args, bool repeat)
        {
            if (args.Length < 2)
                return;

            JsValue handler;
            double interval;

            if (args[0].IsCallable())
            {
                handler = args[0];
                interval = args[1].IsNumber() ? args[1].AsNumber() : 0;
            }
            else if (args[1].IsCallable())
            {
                interval = args[0].IsNumber() ? args[0].AsNumber() : 0;
                handler = args[1];
            }
            else
            {
                return;
            }

            context.Instance.Scheduler.AddTimer(handler, interval, repeat);
        }
    }
}
