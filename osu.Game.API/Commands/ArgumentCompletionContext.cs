// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.API.Commands.Syntax;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// Context passed to an argument's suggestion provider while the user is typing.
    /// </summary>
    public sealed class ArgumentCompletionContext
    {
        /// <summary>The argument currently being edited.</summary>
        public string ArgumentName { get; }

        /// <summary>The raw text supplied for the whole argument.</summary>
        public string ArgumentText { get; }

        /// <summary>The text of the fragment which would be replaced by a suggestion.</summary>
        public string CurrentText { get; }

        /// <summary>The span of the fragment which would be replaced by a suggestion.</summary>
        public CommandTextSpan ReplacementSpan { get; }

        /// <summary>The path into a nested array/object value, if applicable.</summary>
        public IReadOnlyList<CompletionPathSegment> Path { get; }

        /// <summary>Whether the current fragment is an object/dictionary key.</summary>
        public bool IsKey { get; }

        /// <summary>Values of all arguments which could be resolved so far (including defaults).</summary>
        public IReadOnlyDictionary<string, object?> Arguments { get; }

        /// <summary>The parsed value node for this argument, when parsing succeeded.</summary>
        public CommandValueNode? ValueNode { get; }

        public ArgumentCompletionContext(string argumentName, string argumentText, string currentText, CommandTextSpan replacementSpan,
                                         IReadOnlyList<CompletionPathSegment> path, bool isKey, IReadOnlyDictionary<string, object?> arguments,
                                         CommandValueNode? valueNode)
        {
            ArgumentName = argumentName;
            ArgumentText = argumentText;
            CurrentText = currentText;
            ReplacementSpan = replacementSpan;
            Path = path;
            IsKey = isKey;
            Arguments = arguments;
            ValueNode = valueNode;
        }

        public IEnumerable<CommandSuggestion> Suggest(params string[] values)
        {
            foreach (string value in values)
                yield return new CommandSuggestion(value);
        }

        /// <summary>
        /// Invokes <paramref name="provider"/> only when the current fragment is an element of an array argument.
        /// </summary>
        public IEnumerable<CommandSuggestion> SuggestElements(Func<ElementCompletionContext, IEnumerable<CommandSuggestion>> provider)
        {
            if (Path.Count == 0 || Path[^1].Kind != CompletionPathKind.ArrayElement)
                return Array.Empty<CommandSuggestion>();

            return provider(new ElementCompletionContext(this, Path[^1].Index));
        }

        /// <summary>
        /// Invokes <paramref name="provider"/> only when the current fragment is a field (key or value) of an object
        /// argument.
        /// </summary>
        public IEnumerable<CommandSuggestion> SuggestFields(Func<FieldCompletionContext, IEnumerable<CommandSuggestion>> provider)
        {
            if (Path.Count == 0 || Path[^1].Kind is not (CompletionPathKind.ObjectKey or CompletionPathKind.ObjectValue))
                return Array.Empty<CommandSuggestion>();

            var segment = Path[^1];
            return provider(new FieldCompletionContext(this, segment.Key, segment.Kind == CompletionPathKind.ObjectKey));
        }
    }

    /// <summary>Context for a single element of an array argument.</summary>
    public sealed class ElementCompletionContext
    {
        private readonly ArgumentCompletionContext parent;

        public int Index { get; }

        public string CurrentText => parent.CurrentText;

        public CommandTextSpan ReplacementSpan => parent.ReplacementSpan;

        public IReadOnlyDictionary<string, object?> Arguments => parent.Arguments;

        public CommandValueNode? ValueNode => parent.ValueNode;

        public ElementCompletionContext(ArgumentCompletionContext parent, int index)
        {
            this.parent = parent;
            Index = index;
        }

        public IEnumerable<CommandSuggestion> Suggest(params string[] values)
        {
            foreach (string value in values)
                yield return new CommandSuggestion(value);
        }
    }

    /// <summary>Context for a single field of an object argument.</summary>
    public sealed class FieldCompletionContext
    {
        private readonly ArgumentCompletionContext parent;

        /// <summary>The field key, when known.</summary>
        public string? FieldName { get; }

        /// <summary>Whether the user is editing the key rather than the value.</summary>
        public bool IsKey { get; }

        public string CurrentText => parent.CurrentText;

        public CommandTextSpan ReplacementSpan => parent.ReplacementSpan;

        public IReadOnlyDictionary<string, object?> Arguments => parent.Arguments;

        public CommandValueNode? ValueNode => parent.ValueNode;

        public FieldCompletionContext(ArgumentCompletionContext parent, string? fieldName, bool isKey)
        {
            this.parent = parent;
            FieldName = fieldName;
            IsKey = isKey;
        }

        public IEnumerable<CommandSuggestion> Suggest(params string[] values)
        {
            foreach (string value in values)
                yield return new CommandSuggestion(value);
        }
    }

    /// <summary>Helpers for formatting suggestion insert text.</summary>
    public static class CommandSuggestions
    {
        /// <summary>Returns <paramref name="value"/> as-is (for tokens such as enum names or numbers).</summary>
        public static CommandSuggestion Token(string value, string? description = null) => new CommandSuggestion(value, Description: description);

        /// <summary>Quotes and escapes <paramref name="value"/> when the syntax requires it.</summary>
        public static CommandSuggestion String(string value, string? description = null)
            => new CommandSuggestion(QuoteIfNeeded(value), value, description);

        /// <summary>Quotes a value when it contains whitespace or syntax characters.</summary>
        public static string QuoteIfNeeded(string value)
        {
            if (value.Length > 0 && !needsQuotes(value))
                return value;

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool needsQuotes(string value)
        {
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c) || c is ':' or ',' or '"' or '\'' or '[' or ']' or '{' or '}' or '=')
                    return true;
            }

            return false;
        }
    }
}
