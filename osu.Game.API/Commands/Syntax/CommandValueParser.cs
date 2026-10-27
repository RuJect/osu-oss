// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace osu.Game.API.Commands.Syntax
{
    /// <summary>
    /// Parses command values using the osu!oss command syntax. The syntax is a superset of JSON: quoted strings may
    /// use single or double quotes, unquoted strings are allowed when they do not contain syntax characters or
    /// whitespace, and array/object elements may be separated by whitespace as well as commas.
    /// </summary>
    public sealed class CommandValueParser
    {
        private readonly string text;
        private readonly int offset;
        private readonly int end;
        private readonly List<CommandParseError> errors = new List<CommandParseError>();

        private int position;
        private bool complete = true;

        private bool atEnd => position >= end;

        private CommandValueParser(string text, int offset)
        {
            this.text = text;
            this.offset = offset;
            end = text.Length;
        }

        /// <summary>Parses a single value, requiring the whole input to be consumed (trailing whitespace is allowed).</summary>
        public static CommandValueParseResult Parse(string text, int offset = 0)
        {
            var parser = new CommandValueParser(text, offset);

            parser.skipWhitespace();

            if (parser.atEnd)
            {
                parser.error("expected a value", CommandTextSpan.Empty(parser.absolutePosition));
                return parser.result(null);
            }

            var node = parser.parseValue();
            parser.skipWhitespace();

            if (!parser.atEnd)
            {
                parser.complete = false;
                parser.error("unexpected trailing input", new CommandTextSpan(parser.absolutePosition, parser.end - parser.position));
            }

            return parser.result(node);
        }

        /// <summary>Parses a whitespace/comma separated sequence as an unwrapped array.</summary>
        public static CommandValueParseResult ParseSequence(string text, int offset = 0)
        {
            var parser = new CommandValueParser(text, offset);
            var items = parser.parseSequence();
            var node = new CommandArrayNode(new CommandTextSpan(offset, text.Length), items);
            return parser.result(node);
        }

        /// <summary>Parses a whitespace/comma separated set of <c>key: value</c> pairs as an unwrapped object.</summary>
        public static CommandValueParseResult ParseUnwrappedObject(string text, int offset = 0)
        {
            var parser = new CommandValueParser(text, offset);
            var fields = parser.parseObjectFields(hasClosingBrace: false);
            var node = new CommandObjectNode(new CommandTextSpan(offset, text.Length), fields);
            return parser.result(node);
        }

        private int absolutePosition => offset + position;

        private CommandValueParseResult result(CommandValueNode? node)
            => new CommandValueParseResult(node, complete && errors.Count == 0, errors);

        private CommandValueNode? parseValue()
        {
            if (atEnd)
            {
                error("expected a value", CommandTextSpan.Empty(absolutePosition));
                return null;
            }

            switch (text[position])
            {
                case '[':
                    return parseArray();

                case '{':
                    return parseObject();

                case '"':
                case '\'':
                    return parseQuoted();

                default:
                    return parseBare();
            }
        }

        private CommandArrayNode parseArray()
        {
            int start = position;
            position++; // '['

            var items = new List<CommandValueNode>();

            while (true)
            {
                skipSeparators();

                if (atEnd)
                {
                    complete = false;
                    error("unterminated array", new CommandTextSpan(absolutePosition, 0));
                    break;
                }

                if (text[position] == ']')
                {
                    position++;
                    break;
                }

                if (text[position] == '}')
                {
                    error("unexpected '}' in array", new CommandTextSpan(absolutePosition, 1));
                    position++;
                    continue;
                }

                int itemStart = position;
                var item = parseValue();

                if (item != null)
                    items.Add(item);

                if (position == itemStart)
                    position++; // guarantee progress after a malformed value

                skipSeparators();
            }

            return new CommandArrayNode(new CommandTextSpan(offset + start, position - start), items);
        }

        private CommandObjectNode parseObject()
        {
            int start = position;
            position++; // '{'

            var fields = parseObjectFields(hasClosingBrace: true);

            return new CommandObjectNode(new CommandTextSpan(offset + start, position - start), fields);
        }

        private List<CommandObjectField> parseObjectFields(bool hasClosingBrace)
        {
            var fields = new List<CommandObjectField>();

            while (true)
            {
                skipSeparators();

                if (atEnd)
                {
                    if (hasClosingBrace)
                    {
                        complete = false;
                        error("unterminated object", new CommandTextSpan(absolutePosition, 0));
                    }

                    break;
                }

                if (hasClosingBrace && text[position] == '}')
                {
                    position++;
                    break;
                }

                if (!hasClosingBrace && text[position] == '}')
                {
                    complete = false;
                    error("unexpected '}'", new CommandTextSpan(absolutePosition, 1));
                    position++;
                    continue;
                }

                int fieldStart = position;
                string? key = parseKey(out CommandTextSpan keySpan);

                if (key == null)
                {
                    position++;
                    continue;
                }

                skipWhitespace();

                if (atEnd)
                {
                    complete = false;
                    error($"expected ':' after field '{key}'", new CommandTextSpan(absolutePosition, 0));
                    break;
                }

                if (text[position] != ':')
                {
                    complete = false;
                    error($"expected ':' after field '{key}'", new CommandTextSpan(absolutePosition, 1));
                    skipUntilSeparator();
                    continue;
                }

                position++; // ':'
                skipWhitespace();

                if (atEnd)
                {
                    complete = false;
                    error($"expected a value for field '{key}'", new CommandTextSpan(absolutePosition, 0));
                    break;
                }

                var value = parseValue();

                if (value == null)
                {
                    skipUntilSeparator();
                    continue;
                }

                fields.Add(new CommandObjectField(key, keySpan, value, new CommandTextSpan(offset + fieldStart, position - fieldStart)));
                skipSeparators();
            }

            return fields;
        }

        private string? parseKey(out CommandTextSpan keySpan)
        {
            if (atEnd)
            {
                keySpan = CommandTextSpan.Empty(absolutePosition);
                return null;
            }

            char c = text[position];

            if (c == '"' || c == '\'')
            {
                var quoted = parseQuoted();
                keySpan = quoted.Span;
                return quoted.Text;
            }

            int start = position;

            while (!atEnd && !isBareKeyDelimiter(text[position]))
                position++;

            if (position == start)
            {
                keySpan = CommandTextSpan.Empty(absolutePosition);
                error("expected an object key", keySpan);
                return null;
            }

            keySpan = new CommandTextSpan(offset + start, position - start);
            return text.Substring(start, position - start);
        }

        private CommandScalarNode parseQuoted()
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
                    return new CommandScalarNode(new CommandTextSpan(offset + start, position - start), value.ToString(), true, CommandScalarKind.String, value.ToString());
                }

                if (c == '\\')
                {
                    position++;

                    if (atEnd)
                    {
                        complete = false;
                        error("unterminated escape sequence", new CommandTextSpan(absolutePosition, 0));
                        break;
                    }

                    appendEscape(value);
                    continue;
                }

                value.Append(c);
                position++;
            }

            complete = false;
            error("unterminated string", new CommandTextSpan(offset + start, position - start));

            return new CommandScalarNode(new CommandTextSpan(offset + start, position - start), value.ToString(), true, CommandScalarKind.String, value.ToString());
        }

        private void appendEscape(StringBuilder value)
        {
            char escaped = text[position++];

            switch (escaped)
            {
                case '\\':
                case '"':
                case '\'':
                case '/':
                    value.Append(escaped);
                    break;

                case 'n':
                    value.Append('\n');
                    break;

                case 'r':
                    value.Append('\r');
                    break;

                case 't':
                    value.Append('\t');
                    break;

                case 'b':
                    value.Append('\b');
                    break;

                case 'f':
                    value.Append('\f');
                    break;

                case 'u':
                    if (position + 4 <= end && int.TryParse(text.AsSpan(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                    {
                        value.Append((char)code);
                        position += 4;
                    }
                    else
                    {
                        complete = false;
                        error("invalid unicode escape sequence", new CommandTextSpan(absolutePosition, 0));
                    }

                    break;

                default:
                    // Preserve unknown escapes as-is so users can type Windows-style paths without surprises.
                    value.Append(escaped);
                    break;
            }
        }

        private CommandScalarNode parseBare()
        {
            int start = position;

            while (!atEnd && !isBareValueDelimiter(text[position]))
                position++;

            string raw = text.Substring(start, position - start);
            var span = new CommandTextSpan(offset + start, position - start);

            if (tryClassify(raw, out CommandScalarKind kind, out object? literal))
                return new CommandScalarNode(span, raw, false, kind, literal);

            return new CommandScalarNode(span, raw, false, CommandScalarKind.String, raw);
        }

        private List<CommandValueNode> parseSequence()
        {
            var items = new List<CommandValueNode>();

            while (true)
            {
                skipSeparators();

                if (atEnd)
                    break;

                if (text[position] is ']' or '}')
                {
                    error($"unexpected '{text[position]}'", new CommandTextSpan(absolutePosition, 1));
                    position++;
                    continue;
                }

                int itemStart = position;
                var item = parseValue();

                if (item != null)
                    items.Add(item);

                if (position == itemStart)
                    position++;

                skipSeparators();
            }

            return items;
        }

        private static bool tryClassify(string raw, out CommandScalarKind kind, out object? literal)
        {
            kind = CommandScalarKind.String;
            literal = raw;

            if (raw.Length == 0)
                return false;

            switch (raw.ToLowerInvariant())
            {
                case "null":
                case "none":
                case "undefined":
                    kind = CommandScalarKind.Null;
                    literal = null;
                    return true;

                case "true":
                case "t":
                case "+":
                    kind = CommandScalarKind.Boolean;
                    literal = true;
                    return true;

                case "false":
                case "f":
                case "-":
                    kind = CommandScalarKind.Boolean;
                    literal = false;
                    return true;
            }

            if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
            {
                kind = CommandScalarKind.Number;
                literal = integer;
                return true;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double floating))
            {
                kind = CommandScalarKind.Number;
                literal = floating;
                return true;
            }

            return false;
        }

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

        private void skipUntilSeparator()
        {
            while (!atEnd && !char.IsWhiteSpace(text[position]) && text[position] != ',' && text[position] != ']' && text[position] != '}')
                position++;
        }

        private void error(string message, CommandTextSpan span)
        {
            complete = false;
            errors.Add(new CommandParseError(message, span));
        }

        private static bool isBareValueDelimiter(char c)
            => char.IsWhiteSpace(c) || c is ',' or ':' or '[' or ']' or '{' or '}' or '"' or '\'';

        private static bool isBareKeyDelimiter(char c)
            => char.IsWhiteSpace(c) || c is ',' or ':' or '[' or ']' or '{' or '}' or '"' or '\'';
    }
}
