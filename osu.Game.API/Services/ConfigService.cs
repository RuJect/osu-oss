// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Game.Configuration;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Non-generic access to <see cref="OsuConfigManager"/> values, since the public config API is generic
    /// per-value-type.
    /// </summary>
    public class ConfigService
    {
        // shortcut: reaches into ConfigManager's protected ConfigStore via reflection; revisit if a public
        // non-generic lookup is added.
        private static readonly FieldInfo config_store_field =
            typeof(ConfigManager<OsuSetting>).GetField("ConfigStore", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly OsuConfigManager config;

        public ConfigService(OsuConfigManager config)
        {
            this.config = config;
        }

        public object? GetValue(OsuSetting setting)
        {
            IBindable? bindable = getBindable(setting);
            return bindable == null ? null : valueProperty(bindable).GetValue(bindable);
        }

        public void SetValue(OsuSetting setting, object? value)
        {
            IBindable? bindable = getBindable(setting);

            if (bindable == null)
                return;

            PropertyInfo property = valueProperty(bindable);
            property.SetValue(bindable, convert(value, property.PropertyType));
        }

        public string[] Keys() => Enum.GetNames<OsuSetting>();

        private IBindable? getBindable(OsuSetting setting)
        {
            var store = (Dictionary<OsuSetting, IBindable>)config_store_field.GetValue(config)!;
            return store.TryGetValue(setting, out IBindable? bindable) ? bindable : null;
        }

        private static PropertyInfo valueProperty(IBindable bindable) => bindable.GetType().GetProperty("Value")!;

        private static object? convert(object? value, Type targetType)
        {
            if (targetType == typeof(string))
                return value?.ToString();

            if (targetType == typeof(bool))
                return Convert.ToBoolean(value);

            if (targetType == typeof(double))
                return Convert.ToDouble(value);

            if (targetType == typeof(float))
                return Convert.ToSingle(value);

            if (targetType == typeof(int))
                return Convert.ToInt32(value);

            if (targetType.IsEnum)
            {
                if (value is string s)
                    return Enum.Parse(targetType, s, ignoreCase: true);

                if (value is null)
                    return Enum.ToObject(targetType, 0);

                return Enum.ToObject(targetType, Convert.ToInt32(value));
            }

            return value;
        }
    }
}
