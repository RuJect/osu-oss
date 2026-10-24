// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Jint;
using Jint.Native;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides the <c>console</c> object (and <c>osu.log</c>) which forward output to the scripting console.
    /// </summary>
    public class ConsoleApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "console", Global = true)]
        public void Log(JsValue[] args) => write(OutputLevel.Log, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Info(JsValue[] args) => write(OutputLevel.Info, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Warn(JsValue[] args) => write(OutputLevel.Warn, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Error(JsValue[] args) => write(OutputLevel.Error, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Debug(JsValue[] args) => write(OutputLevel.Log, args);

        [ScriptApi(Target = "console", Global = true)]
        public void Table(JsValue value) => write(OutputLevel.Log, new[] { stringify(value) });

        protected override void OnRegistered(ScriptApiContext context)
        {
            // osu.log is an alias for console.log.
            context.Api.Set("log", context.GetObject("console", global: true).Get("log"));
        }

        private void write(OutputLevel level, JsValue[] args)
            => Context.Services.Output.Log(level, Context.Instance.Name, string.Join(" ", args.Select(format)));

        private JsValue stringify(JsValue value)
        {
            JsValue json = Context.Engine.GetValue("JSON");
            return Context.Engine.Invoke(json.Get("stringify"), new object[] { value, JsValue.Null, 2 });
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
