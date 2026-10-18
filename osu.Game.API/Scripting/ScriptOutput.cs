// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Severity of a single line of output emitted by a script.
    /// </summary>
    public enum ScriptOutputLevel
    {
        Log,
        Info,
        Warn,
        Error,
    }

    /// <summary>
    /// A single line of output emitted by a running script.
    /// </summary>
    public class ScriptOutput
    {
        public ScriptOutputLevel Level { get; }

        /// <summary>The name of the script that produced this output.</summary>
        public string Source { get; }

        public string Text { get; }

        public DateTimeOffset Timestamp { get; }

        public ScriptOutput(ScriptOutputLevel level, string source, string text)
        {
            Level = level;
            Source = source;
            Text = text;
            Timestamp = DateTimeOffset.Now;
        }
    }
}
