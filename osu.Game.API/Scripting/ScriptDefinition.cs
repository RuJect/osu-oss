// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// A script which can be executed by the <see cref="ScriptManager"/>.
    /// </summary>
    public class ScriptDefinition
    {
        /// <summary>
        /// The display name of the script.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The file name (relative to the scripts directory) backing this script, or <c>null</c> for an anonymous in-memory script.
        /// </summary>
        public string? FileName { get; }

        /// <summary>
        /// The JavaScript source of the script. Editable in the console.
        /// </summary>
        public string Source { get; set; }

        public bool IsFile => FileName != null;

        public ScriptDefinition(string name, string? fileName, string source)
        {
            Name = name;
            FileName = fileName;
            Source = source;
        }
    }
}
