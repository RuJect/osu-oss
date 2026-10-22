// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides the core scripting surface: <c>osu.version</c>, <c>osu.run</c> and <c>osu.scripts</c>.
    /// </summary>
    public class CoreApiProvider : ScriptApiProvider
    {
        [ScriptApi]
        public string Version => Context.Services.Version;

        [ScriptApi]
        public JsValue Run(string? name)
        {
            if (name == null)
                return JsBoolean.False;

            ScriptInstance? instance = Context.Runner.RunByName(name);
            return instance != null ? JsValue.FromObject(Context.Engine, instance) : JsValue.Null;
        }

        [ScriptApi]
        public string[] Scripts() => Context.Services.Repository.GetNames();
    }
}
