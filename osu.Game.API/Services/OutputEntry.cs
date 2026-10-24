// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.API.Services
{
    /// <summary>
    /// A single line of output emitted by a running script or plugin.
    /// </summary>
    public class OutputEntry
    {
        public OutputLevel Level { get; }

        /// <summary>The name of the script or plugin that produced this output.</summary>
        public string Source { get; }

        public string Text { get; }

        public DateTimeOffset Timestamp { get; }

        public OutputEntry(OutputLevel level, string source, string text)
        {
            Level = level;
            Source = source;
            Text = text;
            Timestamp = DateTimeOffset.Now;
        }
    }
}
