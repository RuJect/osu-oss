// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace osu.Game.API.Plugins
{
    /// <summary>Version information about the plugin API.</summary>
    public static class PluginApi
    {
        /// <summary>The current plugin API version.</summary>
        public const string VERSION = "1.0.0";

        /// <summary>The current plugin API version as a parsed <see cref="SemanticVersion"/>.</summary>
        public static SemanticVersion CurrentVersion { get; } = SemanticVersion.Parse(VERSION);

        private static readonly Regex version_pattern = new Regex(@"^[0-9]+\.[0-9]+\.[0-9]+([-+].*)?$", RegexOptions.Compiled);

        /// <summary>Parses a strict semantic version string.</summary>
        public static bool TryParseVersion(string? value, out SemanticVersion version)
        {
            version = null!;

            if (string.IsNullOrWhiteSpace(value) || !version_pattern.IsMatch(value))
                return false;

            if (!SemanticVersion.TryParse(value, out SemanticVersion? parsed) || parsed == null)
                return false;

            version = parsed;
            return true;
        }
    }
}
