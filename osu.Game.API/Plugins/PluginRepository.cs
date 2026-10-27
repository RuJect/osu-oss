// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Discovers plugins under a scoped <see cref="Storage"/> directory: one sub-directory per plugin,
    /// each containing a <c>manifest.json</c> (see <see cref="PluginManifest"/>).
    /// </summary>
    public class PluginRepository
    {
        private static readonly Logger framework_logger = Logger.GetLogger("plugins");

        private readonly Storage storage;
        private readonly PluginLogger? logger;

        public PluginRepository(Storage storage, PluginLogger? logger = null)
        {
            this.storage = storage;
            this.logger = logger;
        }

        /// <summary>Enumerates the manifests of every valid plugin directory. Invalid ones are skipped and logged.</summary>
        public IReadOnlyList<PluginManifest> EnumeratePlugins()
        {
            var manifests = new List<PluginManifest>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dir in storage.GetDirectories(string.Empty))
            {
                string directoryName = getDirectoryName(dir);
                string manifestPath = Path.Combine(dir, "manifest.json");

                try
                {
                    if (!storage.Exists(manifestPath))
                        continue;

                    string json;

                    using (Stream stream = storage.GetStream(manifestPath, FileAccess.Read, FileMode.Open))
                    using (var reader = new StreamReader(stream))
                        json = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    var manifest = PluginManifestJson.Deserialize(json, storage.GetFullPath(dir));

                    if (manifest == null)
                    {
                        logError($"{directoryName}: manifest is empty");
                        continue;
                    }

                    if (string.Equals(manifest.Id, PluginManifest.SYSTEM_ID, StringComparison.OrdinalIgnoreCase))
                    {
                        logError($"{directoryName}: plugin id '{PluginManifest.SYSTEM_ID}' is reserved for the built-in plugin");
                        continue;
                    }

                    if (!manifest.TryValidate(out string? error))
                    {
                        logError($"{directoryName}: {error}");
                        continue;
                    }

                    if (!ids.Add(manifest.Id))
                    {
                        logError($"{directoryName}: duplicate plugin id '{manifest.Id}'");
                        continue;
                    }

                    manifests.Add(manifest);
                }
                catch (Exception e)
                {
                    logError($"{directoryName}: failed to read manifest: {e.Message}");
                }
            }

            return manifests;
        }

        private void logError(string message)
        {
            if (logger != null)
                logger.Error(message);
            else
                framework_logger.Add(message, LogLevel.Error);
        }

        private static string getDirectoryName(string path)
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(trimmed);
        }
    }
}
