// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides the core scripting surface: <c>osu.version</c>, <c>osu.run</c> and <c>osu.scripts</c>.
    /// </summary>
    public class CoreApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            context.AddProperty(context.Api, "version", context.Services.Version);

            context.AddFunction(context.Api, "run", (_, args) =>
            {
                if (args.Length >= 1 && args[0].IsString())
                    return JsValue.FromObject(context.Engine, context.Runner.RunByName(args[0].AsString()));

                return JsBoolean.False;
            });

            context.AddFunction(context.Api, "scripts", (_, _) =>
            {
                string[] names = context.Services.Repository.GetNames();
                return new JsArray(context.Engine, names.Select(n => (JsValue)n).ToArray());
            });
        }
    }
}
