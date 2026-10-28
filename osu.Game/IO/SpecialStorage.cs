// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using osu.Framework.Platform;

namespace osu.Game.IO
{
    /// <summary>
    /// Resolves the scoped <c>osu-oss</c> storage directory which holds all files specific to
    /// this fork (configuration, downloaded blobs, caches, ...). The directory lives inside a
    /// storage root, e.g. <c>&lt;user storage&gt;/osu-oss/osu-oss</c> for fork data or
    /// <c>&lt;user storage&gt;/osu/osu-oss</c> for official-server auth blobs.
    /// </summary>
    public static class SpecialStorage
    {
        private static readonly Regex plugin_id_regex = new Regex(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>Name of the scoped directory inside the osu! storage.</summary>
        public const string ROOT_FOLDER = "osu-oss";

        /// <summary>Where proprietary authentication blobs are cached.</summary>
        public const string AUTH_BLOBS_FOLDER = "auth-blobs";

        /// <summary>Where user scripts live.</summary>
        public const string SCRIPTS_FOLDER = "scripts";

        /// <summary>Where user plugins (C# assemblies) live.</summary>
        public const string PLUGINS_FOLDER = "plugins";

        /// <summary>Where plugin-side persistent data lives (inside a plugin's scoped directory).</summary>
        public const string DATA_FOLDER = "data";

        /// <summary>Where plugin-side read-only resources live (inside a plugin's scoped directory).</summary>
        public const string RESOURCES_FOLDER = "res";

        /// <summary>Where logs live.</summary>
        public const string LOGS_FOLDER = "logs";

        /// <summary>Config file name (relative to <see cref="ROOT_FOLDER"/>).</summary>
        public const string CONFIG_FILENAME = "config.ini";

        /// <summary>Returns the storage scoped to the <c>osu-oss</c> directory, creating it if required.</summary>
        public static Storage GetRoot(Storage osuStorage) => osuStorage.GetStorageForDirectory(ROOT_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/auth-blobs</c> directory, creating it if required.</summary>
        public static Storage GetAuthBlobs(Storage osuStorage) => GetRoot(osuStorage).GetStorageForDirectory(AUTH_BLOBS_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/scripts</c> directory, creating it if required.</summary>
        public static Storage GetScripts(Storage osuStorage) => GetRoot(osuStorage).GetStorageForDirectory(SCRIPTS_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/plugins</c> directory, creating it if required.</summary>
        public static Storage GetPlugins(Storage osuStorage) => GetRoot(osuStorage).GetStorageForDirectory(PLUGINS_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/plugins/&lt;pluginId&gt;</c> directory, creating it if required.</summary>
        /// <exception cref="ArgumentException">The plugin id is empty or not a safe directory name.</exception>
        public static Storage GetPluginRoot(Storage osuStorage, string pluginId)
        {
            validatePluginId(pluginId);
            return GetPlugins(osuStorage).GetStorageForDirectory(pluginId);
        }

        /// <summary>Returns the storage scoped to the <c>osu-oss/plugins/&lt;pluginId&gt;/data</c> directory, creating it if required.</summary>
        /// <exception cref="ArgumentException">The plugin id is empty or not a safe directory name.</exception>
        public static Storage GetPluginData(Storage osuStorage, string pluginId)
            => GetPluginRoot(osuStorage, pluginId).GetStorageForDirectory(DATA_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/plugins/&lt;pluginId&gt;/res</c> directory, creating it if required.</summary>
        /// <exception cref="ArgumentException">The plugin id is empty or not a safe directory name.</exception>
        public static Storage GetPluginResources(Storage osuStorage, string pluginId)
            => GetPluginRoot(osuStorage, pluginId).GetStorageForDirectory(RESOURCES_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/plugins/&lt;pluginId&gt;/scripts</c> directory, creating it if required.</summary>
        /// <exception cref="ArgumentException">The plugin id is empty or not a safe directory name.</exception>
        public static Storage GetPluginScripts(Storage osuStorage, string pluginId)
            => GetPluginRoot(osuStorage, pluginId).GetStorageForDirectory(SCRIPTS_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/logs</c> directory, creating it if required.</summary>
        public static Storage GetLogs(Storage osuStorage) => GetRoot(osuStorage).GetStorageForDirectory(LOGS_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/plugins/&lt;pluginId&gt;/logs</c> directory, creating it if required.</summary>
        /// <exception cref="ArgumentException">The plugin id is empty or not a safe directory name.</exception>
        public static Storage GetPluginLogs(Storage osuStorage, string pluginId)
            => GetPluginRoot(osuStorage, pluginId).GetStorageForDirectory(LOGS_FOLDER);

        /// <summary>Absolute path of the <c>osu-oss</c> directory.</summary>
        public static string GetRootPath(Storage osuStorage) => osuStorage.GetFullPath(ROOT_FOLDER);

        private static void validatePluginId(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId) || !plugin_id_regex.IsMatch(pluginId))
                throw new ArgumentException("Plugin id must match ^[a-z][a-z0-9_]*$.", nameof(pluginId));
        }
    }
}
