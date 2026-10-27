// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
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
        /// Decodes <paramref name="json"/> and marks up entry points detected on disk under
        /// <paramref name="directoryPath"/> (<c>bin/&lt;id&gt;.dll</c>, <c>scripts/&lt;id&gt;.js</c>).
        /// </summary>
        public static PluginManifest? Deserialize(string json, string directoryPath)
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, options);

            if (manifest == null)
                return null;

            manifest.DirectoryPath = directoryPath;

            if (File.Exists(Path.Combine(directoryPath, "bin", $"{manifest.Id}.dll")))
                manifest.CSharpEntryPoint = Path.Combine(directoryPath, "bin", $"{manifest.Id}.dll");

            if (File.Exists(Path.Combine(directoryPath, "scripts", $"{manifest.Id}.js")))
                manifest.JavaScriptEntryPoint = Path.Combine(directoryPath, "scripts", $"{manifest.Id}.js");

            return manifest;
        }
    }
}
