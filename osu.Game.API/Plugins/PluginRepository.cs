// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using osu.Framework.Platform;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Discovers plugins under a scoped <see cref="Storage"/> directory: one sub-directory per plugin,
    /// each containing a <c>manifest.json</c> (see <see cref="PluginManifest"/>).
    /// </summary>
    public class PluginRepository
    {
        private readonly Storage storage;

        public PluginRepository(Storage storage)
        {
            this.storage = storage;
        }

        /// <summary>Enumerates the manifests of every valid plugin directory. Invalid ones are skipped.</summary>
        public IEnumerable<PluginManifest> EnumeratePlugins()
        {
            foreach (string dir in storage.GetDirectories(string.Empty))
            {
                string manifestPath = Path.Combine(dir, "manifest.json");

                if (!storage.Exists(manifestPath))
                    continue;

                string json;

                using (Stream stream = storage.GetStream(manifestPath, FileAccess.Read, FileMode.Open))
                using (var reader = new StreamReader(stream))
                    json = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(json))
                    continue;

                var manifest = PluginManifestJson.Deserialize(json, storage.GetFullPath(dir));

                if (manifest == null || !manifest.IsValidId)
                    continue;

                yield return manifest;
            }
        }
    }
}
