// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.API.Commands.Syntax
{
    public enum CommandScalarKind
    {
        /// <summary>An unquoted or quoted string which is not a recognised literal.</summary>
        String,

        Number,
        Boolean,
        Null,
    }

    /// <summary>
    /// A parsed command value, independent of any CLR type. Nodes retain their source spans so the command palette can
    /// highlight and replace precisely the fragment currently being edited.
    /// </summary>
    public abstract class CommandValueNode
    {
        public CommandTextSpan Span { get; }

        protected CommandValueNode(CommandTextSpan span)
        {
            Span = span;
        }
    }

    /// <summary>A single scalar value (string, number, boolean or null literal).</summary>
    public sealed class CommandScalarNode : CommandValueNode
    {
        /// <summary>The unescaped content for quoted values, or the raw text for bare values.</summary>
        public string Text { get; }

        public bool Quoted { get; }

        public CommandScalarKind Kind { get; }

        /// <summary>The converted literal value for numbers, booleans and null; the text itself for strings.</summary>
        public object? Literal { get; }

        public CommandScalarNode(CommandTextSpan span, string text, bool quoted, CommandScalarKind kind, object? literal)
            : base(span)
        {
            Text = text;
            Quoted = quoted;
            Kind = kind;
            Literal = literal;
        }

        public override string ToString() => $"{Kind}:{Text}";
    }

    /// <summary>An ordered sequence of values, e.g. <c>[1 2 3]</c>.</summary>
    public sealed class CommandArrayNode : CommandValueNode
    {
        public IReadOnlyList<CommandValueNode> Items { get; }

        public CommandArrayNode(CommandTextSpan span, IReadOnlyList<CommandValueNode> items)
            : base(span)
        {
            Items = items;
        }
    }

    /// <summary>A single key/value pair in a <see cref="CommandObjectNode"/>.</summary>
    public sealed class CommandObjectField
    {
        public string Key { get; }

        public CommandTextSpan KeySpan { get; }

        public CommandValueNode Value { get; }

        /// <summary>The span covering the key, separator and value.</summary>
        public CommandTextSpan Span { get; }

        public CommandObjectField(string key, CommandTextSpan keySpan, CommandValueNode value, CommandTextSpan span)
        {
            Key = key;
            KeySpan = keySpan;
            Value = value;
            Span = span;
        }
    }

    /// <summary>An ordered mapping of string keys to values, e.g. <c>{a: 1 b: 2}</c>.</summary>
    public sealed class CommandObjectNode : CommandValueNode
    {
        public IReadOnlyList<CommandObjectField> Fields { get; }

        public CommandObjectNode(CommandTextSpan span, IReadOnlyList<CommandObjectField> fields)
            : base(span)
        {
            Fields = fields;
        }
    }

    /// <summary>The outcome of parsing a value string, including tolerated-incomplete input.</summary>
    public sealed class CommandValueParseResult
    {
        public CommandValueNode? Node { get; }

        /// <summary>Whether the input was fully consumed without syntax errors.</summary>
        public bool Complete { get; }

        public IReadOnlyList<CommandParseError> Errors { get; }

        public CommandValueParseResult(CommandValueNode? node, bool complete, IReadOnlyList<CommandParseError> errors)
        {
            Node = node;
            Complete = complete;
            Errors = errors;
        }
    }
}
