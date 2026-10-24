// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Accumulates the output history of every running script and plugin and raises an event for each
    /// appended line.
    /// </summary>
    public class OutputLog
    {
        private const int max_history = 5000;

        private static readonly Logger logger = Logger.GetLogger("scripts");

        private readonly List<OutputEntry> history = new List<OutputEntry>();

        public event Action<OutputEntry>? OutputAppended;

        public IReadOnlyList<OutputEntry> History => history;

        public void Log(OutputLevel level, string source, string text)
        {
            var entry = new OutputEntry(level, source, text);

            history.Add(entry);

            while (history.Count > max_history)
                history.RemoveAt(0);

            OutputAppended?.Invoke(entry);

            logger.Add($"[{source}] {text}", level == OutputLevel.Error ? LogLevel.Error : LogLevel.Verbose);
        }
    }
}
