// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.API.Plugins
{
    /// <summary>Assigns a configuration section to an enum member. Unannotated members use <c>General</c>.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PluginConfigSectionAttribute : Attribute
    {
        /// <summary>The INI section name.</summary>
        public string Name { get; }

        public PluginConfigSectionAttribute(string name)
        {
            Name = name;
        }
    }
}
