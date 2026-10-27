// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Jint.Native;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides read-only access to a plugin's bundled resources via <c>osu.res</c>.
    /// </summary>
    public class ResourceApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "res")]
        public string? Read(string? path)
        {
            ResourceStore? resources = Context.Plugin.Resources;
            return resources != null && path != null && resources.Exists(path) ? resources.Read(path) : null;
        }

        [ScriptApi(Target = "res")]
        public bool Exists(string? path)
        {
            ResourceStore? resources = Context.Plugin.Resources;
            return resources != null && path != null && resources.Exists(path);
        }

        [ScriptApi(Target = "res")]
        public string[] List(string? path)
        {
            ResourceStore? resources = Context.Plugin.Resources;

            if (resources == null)
                return Array.Empty<string>();

            return resources.List(path ?? string.Empty).ToArray();
        }

        [ScriptApi(Target = "res")]
        public JsValue ReadBytes(string? path)
        {
            ResourceStore? resources = Context.Plugin.Resources;

            if (resources == null || path == null || !resources.Exists(path))
                return JsValue.Undefined;

            byte[]? bytes = resources.ReadBytes(path);
            return bytes == null ? JsValue.Undefined : ScriptBinary.ToJs(Context.Engine, bytes);
        }
    }
}
