// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;

namespace osu.Game.API.Plugins
{
    /// <summary>Deserialises <see cref="PluginManifest"/> JSON documents.</summary>
    internal static class PluginManifestJson
    {
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// Decodes <paramref name="json"/> and records the plugin's <paramref name="directoryPath"/>.
        /// Entry points are resolved separately by <see cref="PluginLoader"/>.
        /// </summary>
        public static PluginManifest? Deserialize(string json, string directoryPath)
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, options);

            if (manifest == null)
                return null;

            manifest.DirectoryPath = directoryPath;
            return manifest;
        }
    }
}
