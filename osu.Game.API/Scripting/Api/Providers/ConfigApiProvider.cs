// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Jint.Native;
using osu.Game.Configuration;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides read/write access to osu! settings via <c>osu.config</c>. Keys are <see cref="OsuSetting"/>
    /// member names (case-insensitive).
    /// </summary>
    public class ConfigApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "config")]
        public JsValue Get(string? key)
        {
            OsuConfigManager? config = Context.Services.Config;
            OsuSetting? setting = parse(key);

            if (config == null || setting == null)
                return JsValue.Undefined;

            object? value = ScriptConfigHelper.GetValue(config, setting.Value);
            return value == null ? JsValue.Undefined : JsValue.FromObject(Context.Engine, value);
        }

        [ScriptApi(Target = "config")]
        public void Set(string? key, JsValue value)
        {
            OsuConfigManager? config = Context.Services.Config;
            OsuSetting? setting = parse(key);

            if (config == null || setting == null)
                return;

            ScriptConfigHelper.SetValue(config, setting.Value, value);
        }

        [ScriptApi(Target = "config")]
        public string[] Keys() => Enum.GetNames<OsuSetting>();

        private static OsuSetting? parse(string? key)
            => key == null || !Enum.TryParse<OsuSetting>(key, true, out OsuSetting setting) ? null : setting;
    }
}
