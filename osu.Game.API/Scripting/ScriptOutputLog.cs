// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Accumulates the output history of every running script and raises an event for each appended line.
    /// </summary>
    public class ScriptOutputLog
    {
        private const int max_history = 5000;

        private static readonly Logger logger = Logger.GetLogger("scripts");

        private readonly List<ScriptOutput> history = new List<ScriptOutput>();

        public event Action<ScriptOutput>? OutputAppended;

        public IReadOnlyList<ScriptOutput> History => history;

        public void Log(ScriptOutputLevel level, string source, string text)
        {
            var entry = new ScriptOutput(level, source, text);

            history.Add(entry);

            while (history.Count > max_history)
                history.RemoveAt(0);

            OutputAppended?.Invoke(entry);

            logger.Add($"[{source}] {text}", level == ScriptOutputLevel.Error ? LogLevel.Error : LogLevel.Verbose);
        }
    }
}
