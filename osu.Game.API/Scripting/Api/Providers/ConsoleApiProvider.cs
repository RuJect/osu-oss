// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides the <c>console</c> object (and <c>osu.log</c>) which forward output to the scripting console.
    /// </summary>
    public class ConsoleApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            JsObject console = context.CreateObject();

            context.AddFunction(console, "log", (_, args) => write(context, ScriptOutputLevel.Log, args));
            context.AddFunction(console, "info", (_, args) => write(context, ScriptOutputLevel.Info, args));
            context.AddFunction(console, "warn", (_, args) => write(context, ScriptOutputLevel.Warn, args));
            context.AddFunction(console, "error", (_, args) => write(context, ScriptOutputLevel.Error, args));
            context.AddFunction(console, "debug", (_, args) => write(context, ScriptOutputLevel.Log, args));

            context.Engine.SetValue("console", console);

            // osu.log is an alias for console.log.
            context.Api.Set("log", console.Get("log"));
        }

        private static JsValue write(ScriptApiContext context, ScriptOutputLevel level, JsValue[] args)
        {
            context.Services.Output.Log(level, context.Instance.Name, string.Join(" ", args.Select(format)));
            return JsValue.Undefined;
        }

        private static string format(JsValue value)
        {
            if (value.IsNull())
                return "null";

            if (value.IsUndefined())
                return "undefined";

            return value.ToString();
        }
    }
}
