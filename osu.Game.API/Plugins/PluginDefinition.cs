// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Plugins
{
    /// <summary>A plugin assembly discovered on disk.</summary>
    public class PluginDefinition
    {
        /// <summary>The display name of the plugin (the assembly file name without extension).</summary>
        public string Name { get; }

        /// <summary>The absolute path of the plugin assembly.</summary>
        public string Path { get; }

        public PluginDefinition(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }
}
