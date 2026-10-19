// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.API.Commands.Syntax
{
    /// <summary>
    /// One top-level argument segment of a command input.
    /// </summary>
    public sealed class CommandArgumentSegment
    {
        /// <summary>The full segment text, including an optional <c>name=</c> prefix.</summary>
        public string Text { get; }

        /// <summary>The span of the full segment within the command argument string.</summary>
        public CommandTextSpan Span { get; }

        /// <summary>The explicit argument name, when the segment uses <c>name=value</c> syntax.</summary>
        public string? Name { get; }

        /// <summary>The span of the value portion within the command argument string.</summary>
        public CommandTextSpan ValueSpan { get; }

        public string ValueText { get; }

        public CommandArgumentSegment(string text, CommandTextSpan span, string? name, CommandTextSpan valueSpan)
        {
            Text = text;
            Span = span;
            Name = name;
            ValueSpan = valueSpan;
            ValueText = text.Substring(valueSpan.Start - span.Start, valueSpan.Length);
        }

        public override string ToString() => Name == null ? Text : $"{Name}={ValueText}";
    }

    /// <summary>
    /// Splits a command's argument string into top-level segments. Segments are separated by commas or whitespace,
    /// except when the separator is inside quotes, an array or an object.
    /// </summary>
    public static class CommandArgumentSplitter
    {
        public static List<CommandArgumentSegment> Split(string input)
        {
            var segments = new List<CommandArgumentSegment>();

            int segmentStart = -1;
            int depth = 0;
            char quote = '\0';
            bool escaped = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (quote != '\0')
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == quote)
                        quote = '\0';

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (segmentStart < 0)
                        segmentStart = i;

                    quote = c;
                    continue;
                }

                if (c is '[' or '{')
                {
                    if (segmentStart < 0)
                        segmentStart = i;

                    depth++;
                    continue;
                }

                if (c is ']' or '}')
                {
                    if (segmentStart < 0)
                        segmentStart = i;

                    depth = Math.Max(0, depth - 1);
                    continue;
                }

                bool separator = c == ',' || char.IsWhiteSpace(c);

                if (separator && depth == 0)
                {
                    flush(i);
                    continue;
                }

                if (segmentStart < 0)
                    segmentStart = i;
            }

            flush(input.Length);

            return segments;

            void flush(int end)
            {
                if (segmentStart < 0)
                    return;

                int start = segmentStart;
                int trimmedEnd = end;

                while (start < trimmedEnd && char.IsWhiteSpace(input[start]))
                    start++;

                while (trimmedEnd > start && char.IsWhiteSpace(input[trimmedEnd - 1]))
                    trimmedEnd--;

                if (trimmedEnd > start)
                    addSegment(start, trimmedEnd);

                segmentStart = -1;
            }

            void addSegment(int start, int end)
            {
                string text = input.Substring(start, end - start);
                int equals = findTopLevelEquals(text);

                if (equals <= 0)
                {
                    segments.Add(new CommandArgumentSegment(text, new CommandTextSpan(start, end - start), null, new CommandTextSpan(start, end - start)));
                    return;
                }

                string name = text.Substring(0, equals).Trim();

                if (!isValidArgumentName(name))
                {
                    segments.Add(new CommandArgumentSegment(text, new CommandTextSpan(start, end - start), null, new CommandTextSpan(start, end - start)));
                    return;
                }

                int valueStart = equals + 1;

                while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
                    valueStart++;

                string value = text.Substring(valueStart);

                segments.Add(new CommandArgumentSegment(text, new CommandTextSpan(start, end - start), name,
                    new CommandTextSpan(start + valueStart, value.Length)));
            }
        }

        private static int findTopLevelEquals(string text)
        {
            int depth = 0;
            char quote = '\0';
            bool escaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (quote != '\0')
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == quote)
                        quote = '\0';

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quote = c;
                    continue;
                }

                if (c is '[' or '{')
                    depth++;
                else if (c is ']' or '}')
                    depth = Math.Max(0, depth - 1);
                else if (c == '=' && depth == 0)
                    return i;
            }

            return -1;
        }

        private static bool isValidArgumentName(string name)
        {
            if (name.Length == 0)
                return false;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    return false;
            }

            return true;
        }
    }
}
