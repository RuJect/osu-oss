// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using Jint;
using Jint.Native;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Game.Configuration;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Non-generic access to <see cref="OsuConfigManager"/> values, since the public config API is generic
    /// per-value-type. Reaches the protected <c>ConfigStore</c> and reflects over the concrete bindable's
    /// <c>Value</c> property.
    /// </summary>
    public static class ScriptConfigHelper
    {
        private static readonly FieldInfo config_store_field =
            typeof(ConfigManager<OsuSetting>).GetField("ConfigStore", BindingFlags.NonPublic | BindingFlags.Instance)!;

        public static object? GetValue(OsuConfigManager config, OsuSetting setting)
        {
            IBindable? bindable = getBindable(config, setting);
            return bindable == null ? null : valueProperty(bindable).GetValue(bindable);
        }

        public static void SetValue(OsuConfigManager config, OsuSetting setting, JsValue value)
        {
            IBindable? bindable = getBindable(config, setting);

            if (bindable == null)
                return;

            PropertyInfo property = valueProperty(bindable);
            property.SetValue(bindable, convert(value, property.PropertyType));
        }

        private static IBindable? getBindable(OsuConfigManager config, OsuSetting setting)
        {
            var store = (Dictionary<OsuSetting, IBindable>)config_store_field.GetValue(config)!;
            return store.TryGetValue(setting, out IBindable? bindable) ? bindable : null;
        }

        private static PropertyInfo valueProperty(IBindable bindable) => bindable.GetType().GetProperty("Value")!;

        private static object? convert(JsValue value, Type targetType)
        {
            if (targetType == typeof(string))
                return value.IsString() ? value.AsString() : value.ToString();

            if (targetType == typeof(bool))
                return value.AsBoolean();

            if (targetType == typeof(double))
                return value.IsNumber() ? value.AsNumber() : 0d;

            if (targetType == typeof(float))
                return (float)(value.IsNumber() ? value.AsNumber() : 0d);

            if (targetType == typeof(int))
                return (int)(value.IsNumber() ? value.AsNumber() : 0d);

            if (targetType.IsEnum)
            {
                if (value.IsString())
                    return Enum.Parse(targetType, value.AsString(), ignoreCase: true);

                if (value.IsNumber())
                    return Enum.ToObject(targetType, (int)value.AsNumber());

                return Enum.ToObject(targetType, 0);
            }

            return value.ToObject();
        }
    }
}
