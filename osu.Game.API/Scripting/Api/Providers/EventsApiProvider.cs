// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides a simple event bus: <c>osu.on</c>, <c>osu.off</c> and <c>osu.emit</c>.
    /// The game emits a handful of built-in events (e.g. <c>"screen"</c>).
    /// </summary>
    public class EventsApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            context.AddFunction(context.Api, "on", (_, args) =>
            {
                if (args.Length >= 2 && args[0].IsString() && args[1].IsCallable())
                    context.Environment.Events.Subscribe(context.Instance, args[0].AsString(), args[1]);

                return JsValue.Undefined;
            });

            context.AddFunction(context.Api, "off", (_, args) =>
            {
                if (args.Length >= 2 && args[0].IsString() && args[1].IsCallable())
                    context.Environment.Events.Unsubscribe(context.Instance, args[0].AsString(), args[1]);

                return JsValue.Undefined;
            });

            context.AddFunction(context.Api, "emit", (_, args) =>
            {
                if (args.Length >= 1 && args[0].IsString())
                {
                    string name = args[0].AsString();
                    object?[] payload = args.Skip(1).Cast<object?>().ToArray();
                    context.Runner.Emit(name, payload);
                }

                return JsValue.Undefined;
            });
        }
    }
}
