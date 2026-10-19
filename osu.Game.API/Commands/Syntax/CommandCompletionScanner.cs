// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text;

namespace osu.Game.API.Commands.Syntax
{
    internal sealed class CompletionLocation
    {
        public CommandTextSpan ReplacementSpan { get; }

        public string CurrentText { get; }

        public IReadOnlyList<CompletionPathSegment> Path { get; }

        public bool IsKey { get; }

        public CompletionLocation(CommandTextSpan replacementSpan, string currentText, IReadOnlyList<CompletionPathSegment> path, bool isKey)
        {
            ReplacementSpan = replacementSpan;
            CurrentText = currentText;
            Path = path;
            IsKey = isKey;
        }
    }

    /// <summary>
    /// Finds the fragment the caret (assumed to be at the end of the input) is currently editing, along with its path
    /// into nested arrays and objects. The scanner is deliberately tolerant of incomplete input.
    /// </summary>
    internal sealed class CommandCompletionScanner
    {
        private readonly string text;
        private readonly int offset;
        private readonly int end;
        private readonly List<CompletionPathSegment> path = new List<CompletionPathSegment>();

        private int position;

        private bool atEnd => position >= end;

        private CommandCompletionScanner(string text, int offset)
        {
            this.text = text;
            this.offset = offset;
            end = text.Length;
        }

        public static CompletionLocation? Locate(string text, int offset, bool unwrappedArray, bool unwrappedObject)
        {
            var scanner = new CommandCompletionScanner(text, offset);

            if (unwrappedArray)
                return scanner.scanSequence();

            if (unwrappedObject)
                return scanner.scanObjectFields(false);

            scanner.skipWhitespace();

            if (scanner.atEnd)
                return scanner.current(string.Empty);

            var location = scanner.scanValue();

            if (location != null)
                return location;

            scanner.skipWhitespace();
            return null;
        }

        /// <summary>Locates the whole argument as a single editable string fragment.</summary>
        public static CompletionLocation LocateWhole(string text, int offset)
        {
            string trimmed = text.Trim();
            int start = offset + (text.Length - text.TrimStart().Length);
            string current = trimmed;

            if (trimmed.Length >= 1 && (trimmed[0] == '"' || trimmed[0] == '\''))
            {
                int length = trimmed.Length >= 2 && trimmed[^1] == trimmed[0] ? trimmed.Length - 2 : trimmed.Length - 1;
                current = unescape(trimmed.Substring(1, length));
            }

            return new CompletionLocation(new CommandTextSpan(start, trimmed.Length), current,
                Array.Empty<CompletionPathSegment>(), false);
        }

        private CompletionLocation? scanValue()
        {
            skipWhitespace();

            if (atEnd)
                return current(string.Empty);

            char c = text[position];

            if (c == '[')
                return scanArray();

            if (c == '{')
                return scanObject();

            if (c == '"' || c == '\'')
                return scanQuoted();

            return scanBare();
        }

        private CompletionLocation? scanArray()
        {
            position++; // '['
            int index = 0;

            while (true)
            {
                skipSeparators();

                if (atEnd)
                {
                    path.Add(new CompletionPathSegment(CompletionPathKind.ArrayElement, index));
                    var empty = current(string.Empty);
                    path.RemoveAt(path.Count - 1);
                    return empty;
                }

                if (text[position] == ']')
                {
                    position++;
                    return null;
                }

                path.Add(new CompletionPathSegment(CompletionPathKind.ArrayElement, index));
                var location = scanValue();

                if (location != null)
                    return location;

                path.RemoveAt(path.Count - 1);

                if (atEnd)
                    return null;

                index++;
            }
        }

        private CompletionLocation? scanObject()
        {
            position++; // '{'
            return scanObjectFields(true);
        }

        private CompletionLocation? scanObjectFields(bool hasClosingBrace)
        {
            while (true)
            {
                skipSeparators();

                if (atEnd)
                    return current(string.Empty, isKey: true);

                if (hasClosingBrace && text[position] == '}')
                {
                    position++;
                    return null;
                }

                if (!hasClosingBrace && text[position] == '}')
                {
                    position++;
                    return null;
                }

                var key = readKey();

                if (key == null)
                {
                    position++;
                    continue;
                }

                skipWhitespace();

                if (atEnd)
                {
                    path.Add(new CompletionPathSegment(CompletionPathKind.ObjectKey, Key: key.Value.Text));
                    var keyLocation = current(key.Value.Text, isKey: true, span: key.Value.Span);
                    path.RemoveAt(path.Count - 1);
                    return keyLocation;
                }

                if (text[position] != ':')
                {
                    // The key is complete but the separator is missing; keep the key editable.
                    path.Add(new CompletionPathSegment(CompletionPathKind.ObjectKey, Key: key.Value.Text));
                    var keyLocation = current(key.Value.Text, isKey: true, span: key.Value.Span);
                    path.RemoveAt(path.Count - 1);
                    return keyLocation;
                }

                position++; // ':'
                skipWhitespace();

                path.Add(new CompletionPathSegment(CompletionPathKind.ObjectValue, Key: key.Value.Text));

                if (atEnd)
                    return current(string.Empty);

                var valueLocation = scanValue();

                if (valueLocation != null)
                    return valueLocation;

                path.RemoveAt(path.Count - 1);

                if (atEnd)
                    return null;

                skipSeparators();

                if (atEnd)
                    return current(string.Empty, isKey: true);

                if (hasClosingBrace && text[position] == '}')
                {
                    position++;
                    return null;
                }
            }
        }

        private CompletionLocation? scanSequence()
        {
            int index = 0;

            while (true)
            {
                skipSeparators();

                if (atEnd)
                {
                    path.Add(new CompletionPathSegment(CompletionPathKind.ArrayElement, index));
                    var empty = current(string.Empty);
                    path.RemoveAt(path.Count - 1);
                    return empty;
                }

                path.Add(new CompletionPathSegment(CompletionPathKind.ArrayElement, index));
                var location = scanValue();

                if (location != null)
                    return location;

                path.RemoveAt(path.Count - 1);

                if (atEnd)
                    return null;

                index++;
            }
        }

        private CompletionLocation? scanQuoted()
        {
            int start = position;
            char quote = text[position++];
            var value = new StringBuilder();
            bool terminated = false;

            while (!atEnd)
            {
                char c = text[position];

                if (c == quote)
                {
                    position++;
                    terminated = true;
                    break;
                }

                if (c == '\\' && position + 1 < end)
                {
                    position++;
                    value.Append(text[position]);
                    position++;
                    continue;
                }

                value.Append(c);
                position++;
            }

            if (terminated && !atEnd)
                return null;

            return current(value.ToString(), span: new CommandTextSpan(offset + start, position - start));
        }

        private CompletionLocation? scanBare()
        {
            int start = position;

            while (!atEnd && !isDelimiter(text[position]))
                position++;

            if (position == start)
                return current(string.Empty, span: CommandTextSpan.Empty(offset + position));

            if (!atEnd)
                return null;

            return current(text.Substring(start, position - start), span: new CommandTextSpan(offset + start, position - start));
        }

        private (string Text, CommandTextSpan Span)? readKey()
        {
            if (atEnd)
                return null;

            if (text[position] == '"' || text[position] == '\'')
            {
                int start = position;
                char quote = text[position++];
                var value = new StringBuilder();

                while (!atEnd)
                {
                    char c = text[position];

                    if (c == quote)
                    {
                        position++;
                        return (value.ToString(), new CommandTextSpan(offset + start, position - start));
                    }

                    if (c == '\\' && position + 1 < end)
                    {
                        position++;
                        value.Append(text[position]);
                        position++;
                        continue;
                    }

                    value.Append(c);
                    position++;
                }

                return (value.ToString(), new CommandTextSpan(offset + start, position - start));
            }

            int bareStart = position;

            while (!atEnd && !isDelimiter(text[position]) && text[position] != ':')
                position++;

            if (position == bareStart)
                return null;

            return (text.Substring(bareStart, position - bareStart), new CommandTextSpan(offset + bareStart, position - bareStart));
        }

        private CompletionLocation current(string currentText, bool isKey = false, CommandTextSpan? span = null)
            => new CompletionLocation(span ?? CommandTextSpan.Empty(offset + position), currentText, path.ToArray(), isKey);

        private void skipWhitespace()
        {
            while (!atEnd && char.IsWhiteSpace(text[position]))
                position++;
        }

        private void skipSeparators()
        {
            while (!atEnd && (char.IsWhiteSpace(text[position]) || text[position] == ','))
                position++;
        }

        private static bool isDelimiter(char c)
            => char.IsWhiteSpace(c) || c is ',' or ':' or '[' or ']' or '{' or '}' or '"' or '\'';

        private static string unescape(string value)
        {
            if (!value.Contains('\\'))
                return value;

            var builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(value[i]);
                    continue;
                }

                i++;

                switch (value[i])
                {
                    case 'n':
                        builder.Append('\n');
                        break;

                    case 'r':
                        builder.Append('\r');
                        break;

                    case 't':
                        builder.Append('\t');
                        break;

                    default:
                        builder.Append(value[i]);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
