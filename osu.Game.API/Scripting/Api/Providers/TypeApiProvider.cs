// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides <c>osu.type</c>, which resolves a CLR type by name across all loaded assemblies.
    /// </summary>
    public class TypeApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            context.AddFunction(context.Api, "type", (_, args) =>
            {
                if (args.Length >= 1 && args[0].IsString())
                {
                    Type? type = resolve(args[0].AsString());

                    if (type != null)
                        return JsValue.FromObject(context.Engine, type);
                }

                return JsValue.Undefined;
            });
        }

        private static Type? resolve(string name)
        {
            Type? type = Type.GetType(name);

            if (type != null)
                return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(name);

                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
