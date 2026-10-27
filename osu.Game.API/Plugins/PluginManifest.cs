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
        private static readonly Regex id_pattern = new Regex(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>The id of the built-in plugin that hosts the scripting environment.</summary>
        public const string SYSTEM_ID = "system";

        /// <summary>The plugin's identifier. Also names the containing directory, its assembly and its script.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>The plugin's release version. Required; must be a valid semantic version.</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// The plugin API version the plugin targets. Optional; when present it must be a valid semantic
        /// version whose major component matches <see cref="PluginApi.CurrentVersion"/>.
        /// </summary>
        public string? ApiVersion { get; set; }

        public PluginPermissions Permissions { get; set; } = new PluginPermissions();

        /// <summary>The resolved path of the C# plugin assembly, or <c>null</c> if none is present.</summary>
        public string? CSharpEntryPoint { get; internal set; }

        /// <summary>The resolved path of the JavaScript entry script, or <c>null</c> if none is present.</summary>
        public string? JavaScriptEntryPoint { get; internal set; }

        /// <summary>The absolute path of the plugin directory.</summary>
        public string DirectoryPath { get; set; } = string.Empty;

        public bool IsValidId => !string.IsNullOrEmpty(Id) && id_pattern.IsMatch(Id);

        /// <summary>Validates the manifest, returning a human-readable reason on failure.</summary>
        public bool TryValidate(out string? error)
        {
            if (!IsValidId)
            {
                error = $"invalid plugin id '{Id}' (expected ^[a-z][a-z0-9_]*$)";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Version))
            {
                error = "missing plugin version";
                return false;
            }

            if (!PluginApi.TryParseVersion(Version, out _))
            {
                error = $"invalid plugin version '{Version}'";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(ApiVersion))
            {
                if (!PluginApi.TryParseVersion(ApiVersion, out var apiVersion))
                {
                    error = $"invalid plugin API version '{ApiVersion}'";
                    return false;
                }

                if (apiVersion.Major != PluginApi.CurrentVersion.Major)
                {
                    error = $"incompatible plugin API version '{ApiVersion}' (expected major {PluginApi.CurrentVersion.Major})";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public sealed class PluginPermissions
        {
            /// <summary>Grants everything; only meant for built-in (system) plugins.</summary>
            public bool Trusted { get; set; }

            /// <summary>Access to the event bus.</summary>
            public bool Bus { get; set; }

            /// <summary>Persistent per-plugin data directory.</summary>
            public bool Data { get; set; }

            /// <summary>Access to the JavaScript runtime.</summary>
            public bool Js { get; set; }

            public bool CanUseBus => Trusted || Bus;

            public bool CanUseData => Trusted || Data;

            public bool CanUseJs => Trusted || Js;
        }
    }
}
