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
    public class ConsoleApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "console", Global = true)]
        public void Log(JsValue[] args) => write(ScriptOutputLevel.Log, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Info(JsValue[] args) => write(ScriptOutputLevel.Info, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Warn(JsValue[] args) => write(ScriptOutputLevel.Warn, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Error(JsValue[] args) => write(ScriptOutputLevel.Error, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Debug(JsValue[] args) => write(ScriptOutputLevel.Log, args);

        protected override void OnRegistered(ScriptApiContext context)
        {
            // osu.log is an alias for console.log.
            context.Api.Set("log", context.GetObject("console", global: true).Get("log"));
        }

        private void write(ScriptOutputLevel level, JsValue[] args)
            => Context.Services.Output.Log(level, Context.Instance.Name, string.Join(" ", args.Select(format)));

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
