// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides the core scripting surface: <c>osu.version</c>, <c>osu.run</c>, <c>osu.stop</c>, <c>osu.stopAll</c> and <c>osu.scripts</c>.
    /// </summary>
    public class CoreApiProvider : ScriptApiProvider
    {
        [ScriptApi]
        public string Version => Context.Plugin.Services.Version;

        [ScriptApi]
        public JsValue Run(string? name)
        {
            if (name == null)
                return JsBoolean.False;

            ScriptInstance? instance = Context.Runner.RunByName(name);
            return instance != null ? createHandle(instance) : JsValue.Null;
        }

        [ScriptApi]
        public void Stop(string? name)
        {
            if (name == null)
                return;

            foreach (ScriptInstance instance in Context.Runner.Running.Where(i => i.Name == name).ToArray())
                Context.Runner.Stop(instance);
        }

        [ScriptApi]
        public void StopAll() => Context.Runner.StopAll();

        [ScriptApi]
        public string[] Scripts() => Context.Repository.GetNames();

        private JsObject createHandle(ScriptInstance instance)
        {
            JsObject handle = Context.CreateObject();

            handle.Set("name", JsValue.FromObject(Context.Engine, instance.Name));
            handle.Set("state", JsValue.FromObject(Context.Engine, instance.State.ToString()));

            Context.AddFunction(handle, "stop", (_, _) =>
            {
                Context.Runner.Stop(instance);
                return JsValue.Undefined;
            });

            return handle;
        }
    }
}
