// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.RegularExpressions;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Describes a plugin as a directory under <c>osu-oss/plugins</c> containing a <c>manifest.json</c>.
    /// Entry points (<c>bin/&lt;id&gt;.dll</c>, <c>scripts/&lt;id&gt;.js</c>) are detected on disk, not stored in the manifest.
    /// </summary>
    public sealed class PluginManifest
    {
        /// <summary>The plugin's identifier. Also names the containing directory, its assembly and its script.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Free-text description from the manifest.</summary>
        public string About { get; set; } = string.Empty;

        public PluginPermissions Permissions { get; set; } = new PluginPermissions();

        /// <summary>The resolved path of the C# plugin assembly, or <c>null</c> if none is present.</summary>
        public string? CSharpEntryPoint { get; set; }

        /// <summary>The resolved path of the JavaScript entry script, or <c>null</c> if none is present.</summary>
        public string? JavaScriptEntryPoint { get; set; }

        /// <summary>The absolute path of the plugin directory.</summary>
        public string DirectoryPath { get; set; } = string.Empty;

        public bool IsValidId => !string.IsNullOrEmpty(Id) && Regex.IsMatch(Id, @"^[a-z][a-z0-9_]*$");

        public sealed class PluginPermissions
        {
            /// <summary>Grants everything; only meant for built-in (system) plugins.</summary>
            public bool Trusted { get; set; }

            /// <summary>Access to the event bus.</summary>
            public bool Bus { get; set; }

            /// <summary>Persistent per-plugin data directory.</summary>
            public bool Data { get; set; }

            /// <summary>Access to static CLR helpers from JavaScript.</summary>
            public bool JsStatic { get; set; }

            /// <summary>Access to runtime CLR helpers from JavaScript.</summary>
            public bool JsRuntime { get; set; }

            public bool HasPermission(string permission) => Trusted || permission switch
            {
                "bus" => Bus,
                "data" => Data,
                "js:static" => JsStatic,
                "js:runtime" => JsRuntime,
                _ => false
            };
        }
    }
}
