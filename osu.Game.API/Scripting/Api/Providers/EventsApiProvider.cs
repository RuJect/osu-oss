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
    public class EventsApiProvider : ScriptApiProvider
    {
        [ScriptApi]
        public void On(string? eventName, JsValue handler)
        {
            if (eventName != null && handler.IsCallable())
                Context.Services.Events.Subscribe(Context.Instance, eventName, handler);
        }

        [ScriptApi]
        public void Off(string? eventName, JsValue handler)
        {
            if (eventName != null && handler.IsCallable())
                Context.Services.Events.Unsubscribe(Context.Instance, eventName, handler);
        }

        [ScriptApi]
        public void Once(string? eventName, JsValue handler)
        {
            if (eventName != null && handler.IsCallable())
                Context.Services.Events.SubscribeOnce(Context.Instance, eventName, handler);
        }

        [ScriptApi]
        public void Emit(JsValue[] args)
        {
            if (args.Length >= 1 && args[0].IsString())
            {
                string name = args[0].AsString();
                object?[] payload = args.Skip(1).Cast<object?>().ToArray();
                Context.Runner.Emit(name, payload);
            }
        }
    }
}
