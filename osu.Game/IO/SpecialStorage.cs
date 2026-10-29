// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.IO
{
    /// <summary>
    /// Resolves the fixed <c>osu-oss</c> storage directory which holds all files specific to
    /// this fork (configuration, downloaded blobs, themes, plugins, ...), regardless of the
    /// active game data path.
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

        /// <summary>Where user themes live.</summary>
        public const string THEMES_FOLDER = "themes";

        /// <summary>Where plugin-side persistent data lives (inside a plugin's scoped directory).</summary>
        public const string DATA_FOLDER = "data";

        /// <summary>Where plugin-side read-only resources live (inside a plugin's scoped directory).</summary>
        public const string RESOURCES_FOLDER = "res";

        /// <summary>Where logs live.</summary>
        public const string LOGS_FOLDER = "logs";

        /// <summary>Config file name (relative to <see cref="ROOT_FOLDER"/>).</summary>
        public const string CONFIG_FILENAME = "config.ini";

        /// <summary>
        /// Returns the fixed location used for osu!oss-specific data, independent of the game's data path.
        /// </summary>
        public static string GetDefaultRootPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ROOT_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss</c> directory, creating it if required.</summary>
        public static Storage GetRoot(Storage osuStorage)
        {
            if (osuStorage is OsuStorage { SpecialStorageRoot: not null } osuStorageWithSpecialRoot)
                return osuStorageWithSpecialRoot.SpecialStorageRoot;

            return osuStorage.GetStorageForDirectory(ROOT_FOLDER);
        }

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
        public static string GetRootPath(Storage osuStorage) => GetRoot(osuStorage).GetFullPath(string.Empty);

        /// <summary>
        /// Migrates legacy <c>osu-oss</c> directories into the central special storage root.
        /// Existing files in the destination are preserved and the source is only removed after a successful copy.
        /// </summary>
        public static void MigrateLegacyStorage(Storage specialRoot, IEnumerable<Storage> legacyRoots)
        {
            string specialRootPath = Path.GetFullPath(specialRoot.GetFullPath(string.Empty));

            foreach (Storage legacyRoot in legacyRoots)
            {
                string legacyPath = Path.GetFullPath(legacyRoot.GetFullPath(string.Empty));

                if (string.Equals(legacyPath, specialRootPath, StringComparison.Ordinal)
                    || isSubDirectory(legacyPath, specialRootPath)
                    || !Directory.Exists(legacyPath))
                    continue;

                if (tryMigrateLegacyStorage(legacyPath, specialRootPath))
                    Logger.Log($"Migrated osu-oss data from {legacyPath} to {specialRootPath}.", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        /// <summary>
        /// Locates the official osu! user storage directory and migrates its legacy auth cache into <paramref name="specialRoot"/>.
        /// </summary>
        public static void MigrateLegacyAuthStorage(GameHost host, Storage specialRoot)
        {
            string specialRootPath = Path.GetFullPath(specialRoot.GetFullPath(string.Empty));
            string targetPath = Path.Combine(specialRootPath, AUTH_BLOBS_FOLDER);

            foreach (string userStoragePath in host.UserStoragePaths)
            {
                string legacyPath = Path.Combine(userStoragePath, @"osu", ROOT_FOLDER, AUTH_BLOBS_FOLDER);

                if (Directory.Exists(legacyPath) && tryMigrateLegacyStorage(legacyPath, targetPath))
                    Logger.Log($"Migrated osu-oss auth blobs from {legacyPath} to {targetPath}.", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        /// <summary>
        /// Locates known legacy <c>osu-oss</c> directories and migrates them into <paramref name="specialRoot"/>.
        /// </summary>
        public static void MigrateLegacyStorage(GameHost host, Storage specialRoot, string? currentStoragePath = null)
        {
            List<string> legacyPaths = new List<string>();

            void addLegacyPath(string? path)
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    legacyPaths.Add(path);
            }

            if (!string.IsNullOrEmpty(currentStoragePath))
                addLegacyPath(Path.Combine(currentStoragePath, ROOT_FOLDER));

            addLegacyPath(Path.Combine(specialRoot.GetFullPath(string.Empty), ROOT_FOLDER));
            addLegacyPath(Path.Combine(host.Storage.GetFullPath(string.Empty), ROOT_FOLDER));

            foreach (string userStoragePath in host.UserStoragePaths)
                addLegacyPath(Path.Combine(userStoragePath, @"osu", ROOT_FOLDER));

            MigrateLegacyStorage(
                specialRoot,
                legacyPaths.Distinct(StringComparer.Ordinal).Select(host.GetStorage).ToArray());
        }

        private static bool isSubDirectory(string parentPath, string childPath)
        {
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentPath));
            string child = Path.TrimEndingDirectorySeparator(Path.GetFullPath(childPath));

            return child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static bool tryMigrateLegacyStorage(string sourcePath, string targetPath)
        {
            try
            {
                Directory.CreateDirectory(targetPath);

                foreach (string sourceFile in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(sourcePath, sourceFile);
                    string targetFile = Path.Combine(targetPath, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

                    if (!File.Exists(targetFile))
                        File.Copy(sourceFile, targetFile);
                }

                Directory.Delete(sourcePath, true);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to migrate osu-oss data from {sourcePath} to {targetPath}");
                return false;
            }
        }

        private static void validatePluginId(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId) || !plugin_id_regex.IsMatch(pluginId))
                throw new ArgumentException("Plugin id must match ^[a-z][a-z0-9_]*$.", nameof(pluginId));
        }
    }
}
