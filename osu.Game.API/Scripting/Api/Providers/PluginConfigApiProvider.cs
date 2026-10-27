// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Jint;
using Jint.Native;
using osu.Game.API.Plugins;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides access to the plugin's own configuration via <c>osu.pluginConfig</c>.
    /// </summary>
    public class PluginConfigApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "pluginConfig")]
        public JsValue Get(string? key)
        {
            IPluginConfig? config = Context.Plugin.Config;

            if (config == null || key == null)
                return JsValue.Undefined;

            object? value = config.GetValue(key);
            return value == null ? JsValue.Undefined : JsValue.FromObject(Context.Engine, value);
        }

        [ScriptApi(Target = "pluginConfig")]
        public void Set(string? key, JsValue value)
        {
            IPluginConfig? config = Context.Plugin.Config;

            if (config == null || key == null)
                return;

            config.SetValue(key, value.IsUndefined() ? null : value.ToObject());
        }

        [ScriptApi(Target = "pluginConfig")]
        public void Reset(string? key)
        {
            IPluginConfig? config = Context.Plugin.Config;

            if (config != null && key != null)
                config.Reset(key);
        }

        [ScriptApi(Target = "pluginConfig")]
        public string[] Keys(string? section) => Context.Plugin.Config?.Keys(section).ToArray() ?? Array.Empty<string>();

        [ScriptApi(Target = "pluginConfig")]
        public string[] Sections() => Context.Plugin.Config?.Sections.ToArray() ?? Array.Empty<string>();
    }
}
