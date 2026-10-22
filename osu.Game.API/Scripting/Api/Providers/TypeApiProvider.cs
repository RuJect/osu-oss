// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides <c>osu.type</c>, which resolves a CLR type by name across all loaded assemblies.
    /// </summary>
    public class TypeApiProvider : ScriptApiProvider
    {
        [ScriptApi(Name = "type")]
        public JsValue ResolveType(string? name)
        {
            if (name == null)
                return JsValue.Undefined;

            Type? type = resolve(name);
            return type != null ? JsValue.FromObject(Context.Engine, type) : JsValue.Undefined;
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
