// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.IO;

namespace osu.Game.Theming
{
    /// <summary>
    /// Loads built-in and user theme definitions and exposes the active <see cref="ThemeSnapshot"/>.
    /// </summary>
    /// <remarks>
    /// The active theme is resolved once during startup. Theme values are consumed while drawables are
    /// constructed, so changing the selection requires a restart. The settings UI still reads
    /// <see cref="AvailableThemes"/> to offer a live preview of every loaded theme.
    /// </remarks>
    public static class ThemeManager
    {
        public const string DEFAULT_THEME = "midnight-violet";
        public const string THEMES_FOLDER = SpecialStorage.THEMES_FOLDER;

        private const string builtin_resource_marker = ".Theming.BuiltIn.";

        public static ThemeSnapshot Current { get; private set; } = createVanillaTheme();

        /// <summary>
        /// Whether the user has allowed themes to use glass effects for this session.
        /// </summary>
        public static bool GlassEffectsAllowed { get; private set; } = true;

        /// <summary>
        /// Whether the active theme and user settings currently enable glass surfaces.
        /// </summary>
        public static bool GlassActive => GlassEffectsAllowed && Current.GlassEnabled;

        /// <summary>
        /// Whether all theme colours should have their hue replaced by the theme's main hue.
        /// </summary>
        public static bool AggressiveTinting { get; private set; }

        public static IReadOnlyList<ThemeSnapshot> AvailableThemes => available_themes;

        public static IReadOnlyList<string> LoadErrors => load_errors;

        private static readonly List<ThemeSnapshot> available_themes = new List<ThemeSnapshot>();
        private static readonly List<string> load_errors = new List<string>();

        private static Storage? themeStorage;

        public static string? ThemesPath => themeStorage?.GetFullPath(string.Empty);

        public static void Initialise(Storage osuStorage, string? themeId, bool glassEffectsAllowed = true, bool aggressiveTinting = true)
        {
            themeStorage = SpecialStorage.GetRoot(osuStorage).GetStorageForDirectory(THEMES_FOLDER);
            GlassEffectsAllowed = glassEffectsAllowed;
            AggressiveTinting = aggressiveTinting;
            Reload(themeId);
        }

        public static void Reload(string? themeId = null)
        {
            themeId ??= Current.Id;

            var definitions = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            var userThemeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            available_themes.Clear();
            load_errors.Clear();

            foreach (var (id, json) in loadBuiltInDefinitions())
                definitions[id] = json;

            foreach (var (id, json) in loadUserDefinitions())
            {
                definitions[id] = json;
                userThemeIds.Add(id);
            }

            var resolutionStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string? id in definitions.Keys.ToArray())
            {
                try
                {
                    JObject merged = mergeDefinition(id, definitions, resolutionStack);
                    available_themes.Add(new ThemeSnapshot(id, merged, !userThemeIds.Contains(id)));
                }
                catch (Exception e)
                {
                    string message = $"Failed to load theme '{id}': {e.Message}";
                    load_errors.Add(message);
                    Logger.Error(e, message, LoggingTarget.Runtime);
                }
            }

            available_themes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            ThemeSnapshot? requested = available_themes.FirstOrDefault(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));

            if (requested == null)
            {
                requested = available_themes.FirstOrDefault(t => string.Equals(t.Id, DEFAULT_THEME, StringComparison.OrdinalIgnoreCase));

                if (requested != null)
                    Logger.Log($"Theme '{themeId}' was not found, falling back to '{requested.Id}'.", LoggingTarget.Runtime, LogLevel.Important);
            }

            if (requested == null)
            {
                Current = createVanillaTheme();
                Logger.Log("No themes were loaded; using vanilla colours.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            Current = requested;
            Logger.Log($"Using theme '{Current.Name}' ({Current.Id}).", LoggingTarget.Runtime, LogLevel.Verbose);
            Logger.Log($"Glass effects {(GlassActive ? "enabled" : "disabled")} (user setting: {GlassEffectsAllowed}, theme: {Current.GlassEnabled}). Aggressive tinting: {AggressiveTinting}.", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private static IEnumerable<(string, JObject)> loadBuiltInDefinitions()
        {
            Assembly assembly = typeof(ThemeManager).Assembly;

            foreach (string resource in assembly.GetManifestResourceNames())
            {
                int markerIndex = resource.IndexOf(builtin_resource_marker, StringComparison.Ordinal);

                if (markerIndex < 0 || !resource.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                string id = resource.Substring(markerIndex + builtin_resource_marker.Length);
                id = id.Substring(0, id.Length - ".json".Length).ToLowerInvariant();

                using var stream = assembly.GetManifestResourceStream(resource);

                if (stream == null)
                    continue;

                using var reader = new StreamReader(stream);

                JObject? json = parseTheme(id, reader.ReadToEnd());

                if (json != null)
                    yield return (id, json);
            }
        }

        private static IEnumerable<(string, JObject)> loadUserDefinitions()
        {
            var result = new List<(string, JObject)>();

            if (themeStorage == null)
                return result;

            foreach (string file in themeStorage.GetFiles(string.Empty, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

                try
                {
                    string content = File.ReadAllText(themeStorage.GetFullPath(file));
                    JObject? json = parseTheme(id, content);

                    if (json != null)
                        result.Add((id, json));
                }
                catch (Exception e)
                {
                    string message = $"Failed to read theme file '{file}': {e.Message}";
                    load_errors.Add(message);
                    Logger.Error(e, message, LoggingTarget.Runtime);
                }
            }

            return result;
        }

        private static JObject? parseTheme(string id, string content)
        {
            try
            {
                return JObject.Parse(content);
            }
            catch (JsonException e)
            {
                string message = $"Theme '{id}' contains invalid JSON: {e.Message}";
                load_errors.Add(message);
                Logger.Error(e, message, LoggingTarget.Runtime);
                return null;
            }
        }

        private static JObject mergeDefinition(string id, IReadOnlyDictionary<string, JObject> definitions, HashSet<string> resolutionStack)
        {
            if (!definitions.TryGetValue(id, out JObject? json))
                throw new ThemeLoadException($"Base theme '{id}' was not found.");

            if (!resolutionStack.Add(id))
                throw new ThemeLoadException($"Circular theme inheritance involving '{id}'.");

            try
            {
                string? parentId = json.Value<string>("inherits");

                if (string.IsNullOrWhiteSpace(parentId))
                    return (JObject)json.DeepClone();

                JObject parent = mergeDefinition(parentId, definitions, resolutionStack);
                return deepMerge(parent, json);
            }
            finally
            {
                resolutionStack.Remove(id);
            }
        }

        private static JObject deepMerge(JObject target, JObject overrides)
        {
            var result = (JObject)target.DeepClone();

            foreach (var property in overrides.Properties())
            {
                if (property.Name.Equals("inherits", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (result[property.Name] is JObject existing && property.Value is JObject incoming)
                    result[property.Name] = deepMerge(existing, incoming);
                else
                    result[property.Name] = property.Value.DeepClone();
            }

            return result;
        }

        internal static void UseVanillaForTesting() => Current = createVanillaTheme();

        private static ThemeSnapshot createVanillaTheme() => new ThemeSnapshot("vanilla", new JObject
        {
            ["name"] = "Vanilla",
            ["author"] = "ppy",
            ["description"] = "osu!lazer's default colour palette.",
        }, true);
    }
}
