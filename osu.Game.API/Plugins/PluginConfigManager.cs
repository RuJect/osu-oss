// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Per-plugin configuration stored in <c>config.ini</c> under the plugin root. Settings are enum members;
    /// each member may be assigned a section with <see cref="PluginConfigSectionAttribute"/>.
    /// </summary>
    public class PluginConfigManager<TLookup> : ConfigManager<TLookup>, IPluginConfig
        where TLookup : struct, Enum
    {
        private const string config_file = "config.ini";
        private const string default_section = "General";

        private static readonly TLookup[] lookups = typeof(TLookup)
                                                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                                                    .Select(f => (TLookup)f.GetValue(null)!)
                                                    .ToArray();

        private static readonly Dictionary<TLookup, string> sections_by_lookup = lookups.ToDictionary(lookup => lookup, getSectionName);
        private static readonly string[] sections = lookups.Select(lookup => sections_by_lookup[lookup]).Distinct().ToArray();
        private static readonly IReadOnlyList<string> section_list = Array.AsReadOnly(sections);

        private readonly Storage storage;
        private readonly PluginLogger logger;

        /// <inheritdoc />
        public Type SettingType => typeof(TLookup);

        /// <inheritdoc />
        public IReadOnlyList<string> Sections => section_list;

        /// <summary>
        /// Creates a configuration manager and loads <c>config.ini</c> after <paramref name="configureDefaults"/>
        /// has registered the plugin's defaults.
        /// </summary>
        public PluginConfigManager(Storage storage, PluginLogger logger, Action<PluginConfigManager<TLookup>>? configureDefaults = null)
        {
            this.storage = storage;
            this.logger = logger;

            configureDefaults?.Invoke(this);

            Load();
        }

        /// <summary>Registers a default value for <paramref name="lookup"/>.</summary>
        public new Bindable<T> SetDefault<T>(TLookup lookup, T value) => base.SetDefault(lookup, value);

        /// <summary>Registers a default boolean value for <paramref name="lookup"/>.</summary>
        public new BindableBool SetDefault(TLookup lookup, bool value) => base.SetDefault(lookup, value);

        /// <summary>Registers a default integer value for <paramref name="lookup"/> with optional bounds.</summary>
        public new BindableInt SetDefault(TLookup lookup, int value, int? min = null, int? max = null)
            => base.SetDefault(lookup, value, min, max);

        /// <summary>Registers a default float value for <paramref name="lookup"/> with optional bounds and precision.</summary>
        public new BindableFloat SetDefault(TLookup lookup, float value, float? min = null, float? max = null, float? precision = null)
            => base.SetDefault(lookup, value, min, max, precision);

        /// <summary>Registers a default double value for <paramref name="lookup"/> with optional bounds and precision.</summary>
        public new BindableDouble SetDefault(TLookup lookup, double value, double? min = null, double? max = null, double? precision = null)
            => base.SetDefault(lookup, value, min, max, precision);

        /// <inheritdoc />
        public IReadOnlyList<string> Keys(string? section = null)
        {
            IEnumerable<TLookup> values = section == null
                ? lookups
                : lookups.Where(lookup => string.Equals(sections_by_lookup[lookup], section, StringComparison.Ordinal));

            return values.Select(lookup => lookup.ToString()).ToArray();
        }

        /// <inheritdoc />
        public object? GetValue(string key)
        {
            if (!tryGetLookup(key, out TLookup lookup))
            {
                logger.Warn($"unknown config key '{key}'");
                return null;
            }

            if (!ConfigStore.TryGetValue(lookup, out IBindable? bindable))
                return null;

            return valueProperty(bindable).GetValue(bindable);
        }

        /// <inheritdoc />
        public void SetValue(string key, object? value)
        {
            if (value == null)
            {
                Reset(key);
                return;
            }

            if (!tryGetBindable(key, out IBindable? bindable))
                return;

            if (bindable is not IParseable parseable)
            {
                logger.Warn($"config key '{key}' cannot be parsed");
                return;
            }

            try
            {
                parseable.Parse(formatForParse(value), CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                logger.Warn($"invalid value for config key '{key}': {e.Message}");
            }
        }

        /// <inheritdoc />
        public void Reset(string key)
        {
            if (!tryGetBindable(key, out IBindable? bindable))
                return;

            try
            {
                bindable.GetType().GetMethod("SetDefault", Type.EmptyTypes)?.Invoke(bindable, null);
            }
            catch (Exception e)
            {
                logger.Warn($"failed to reset config key '{key}': {e.Message}");
            }
        }

        protected override void PerformLoad()
        {
            if (!storage.Exists(config_file))
                return;

            try
            {
                using Stream stream = storage.GetStream(config_file, FileAccess.Read, FileMode.Open);
                using var reader = new StreamReader(stream);

                string? line;

                while ((line = reader.ReadLine()) != null)
                    parseLine(line);
            }
            catch (Exception e)
            {
                logger.Error($"failed to load plugin config: {e.Message}");
            }
        }

        protected override bool PerformSave()
        {
            try
            {
                using Stream stream = storage.CreateFileSafely(config_file);
                using var writer = new StreamWriter(stream) { NewLine = "\n" };

                foreach (string section in sections)
                {
                    TLookup[] sectionLookups = lookups
                                               .Where(lookup => string.Equals(sections_by_lookup[lookup], section, StringComparison.Ordinal)
                                                                && ConfigStore.ContainsKey(lookup))
                                               .ToArray();

                    if (sectionLookups.Length == 0)
                        continue;

                    writer.WriteLine($"[{section}]");

                    foreach (TLookup lookup in sectionLookups)
                    {
                        IBindable bindable = ConfigStore[lookup];
                        object? value = valueProperty(bindable).GetValue(bindable);
                        writer.WriteLine($"{lookup} = {formatForSave(value)}");
                    }

                    writer.WriteLine();
                }

                return true;
            }
            catch (Exception e)
            {
                logger.Error($"failed to save plugin config: {e.Message}");
                return false;
            }
        }

        private void parseLine(string line)
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                return;

            if (trimmed.StartsWith('['))
            {
                if (!trimmed.EndsWith(']'))
                    logger.Warn($"invalid config section line '{trimmed}'");

                return;
            }

            int separator = trimmed.IndexOf('=');

            if (separator <= 0)
            {
                logger.Warn($"invalid config line '{trimmed}'");
                return;
            }

            string key = trimmed[..separator].Trim();
            string rawValue = trimmed[(separator + 1)..].Trim();

            if (!tryGetLookup(key, out TLookup lookup))
            {
                logger.Warn($"unknown config key '{key}'");
                return;
            }

            if (!ConfigStore.TryGetValue(lookup, out IBindable? bindable))
            {
                logger.Warn($"no config entry for '{key}'");
                return;
            }

            if (bindable is not IParseable parseable)
            {
                logger.Warn($"config key '{key}' cannot be parsed");
                return;
            }

            try
            {
                parseable.Parse(rawValue, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                logger.Warn($"invalid value for config key '{key}': {e.Message}");
            }
        }

        private bool tryGetBindable(string key, out IBindable bindable)
        {
            bindable = null!;

            if (!tryGetLookup(key, out TLookup lookup))
            {
                logger.Warn($"unknown config key '{key}'");
                return false;
            }

            if (!ConfigStore.TryGetValue(lookup, out IBindable? found))
            {
                logger.Warn($"no config entry for '{key}'");
                return false;
            }

            bindable = found;
            return true;
        }

        private static bool tryGetLookup(string key, out TLookup lookup)
            => Enum.TryParse(key, true, out lookup) && Enum.IsDefined(lookup);

        private static string getSectionName(TLookup lookup)
            => typeof(TLookup).GetField(lookup.ToString())?.GetCustomAttribute<PluginConfigSectionAttribute>()?.Name ?? default_section;

        private static PropertyInfo valueProperty(IBindable bindable) => bindable.GetType().GetProperty("Value")!;

        private static string formatForParse(object value)
        {
            return value switch
            {
                string text => text,
                bool boolean => boolean.ToString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }

        private static string formatForSave(object? value)
        {
            string text = value switch
            {
                null => string.Empty,
                string raw => raw,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };

            return text.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
