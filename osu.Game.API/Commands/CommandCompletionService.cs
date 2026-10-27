// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.API.Commands.Syntax;

namespace osu.Game.API.Commands
{
    /// <summary>The current argument, replacement span and suggestions for a command input.</summary>
    public sealed class CommandCompletion
    {
        public CommandArgument? Argument { get; }

        public CommandTextSpan ReplacementSpan { get; }

        public string CurrentText { get; }

        public IReadOnlyList<CommandSuggestion> Suggestions { get; }

        public IReadOnlyDictionary<string, object?> Arguments { get; }

        public IReadOnlyList<CompletionPathSegment> Path { get; }

        public bool IsKey { get; }

        public CommandCompletion(CommandArgument? argument, CommandTextSpan replacementSpan, string currentText,
                                 IReadOnlyList<CommandSuggestion> suggestions, IReadOnlyDictionary<string, object?> arguments,
                                 IReadOnlyList<CompletionPathSegment> path, bool isKey)
        {
            Argument = argument;
            ReplacementSpan = replacementSpan;
            CurrentText = currentText;
            Suggestions = suggestions;
            Arguments = arguments;
            Path = path;
            IsKey = isKey;
        }
    }

    /// <summary>
    /// Computes argument suggestions for the command palette. This service owns the structural traversal of array and
    /// object values, leaving command authors to only describe the values relevant to their argument.
    /// </summary>
    public static class CommandCompletionService
    {
        public static CommandCompletion GetCompletion(CommandDefinition command, string argumentText)
        {
            if (command.Arguments.Count == 0)
                return empty(null, argumentText);

            var binding = CommandBinder.Bind(command, argumentText);

            var target = findTarget(command, argumentText, binding, out CommandArgumentSegment? segment, out bool expectNextArgument);

            if (target == null)
                return empty(null, argumentText, binding.Invocation.Arguments);

            if (expectNextArgument || segment == null)
            {
                var emptySpan = new CommandTextSpan(argumentText.Length, 0);
                var emptyLocation = new CompletionLocation(emptySpan, string.Empty, Array.Empty<CompletionPathSegment>(), false);
                return build(command, target, argumentText, binding, emptyLocation);
            }

            string valueText = segment.ValueText;
            int offset = segment.ValueSpan.Start;

            Type type = Nullable.GetUnderlyingType(target.ValueType) ?? target.ValueType;

            CompletionLocation? location;

            if (type == typeof(string) || type == typeof(char))
                location = CommandCompletionScanner.LocateWhole(valueText, offset);
            else
            {
                bool unwrappedArray = CommandBinder.IsArrayLike(type) && !valueText.TrimStart().StartsWith('[');
                bool unwrappedObject = CommandBinder.IsObjectLike(type) && !valueText.TrimStart().StartsWith('{');

                location = CommandCompletionScanner.Locate(valueText, offset, unwrappedArray, unwrappedObject);
            }

            if (location == null)
                return empty(target, argumentText, binding.Invocation.Arguments);

            return build(command, target, argumentText, binding, location);
        }

        private static CommandCompletion build(CommandDefinition command, CommandArgument argument, string argumentText,
                                               CommandBindingResult binding, CompletionLocation location)
        {
            binding.Nodes.TryGetValue(argument.Name, out CommandValueNode? node);

            var context = new ArgumentCompletionContext(
                argument.Name,
                argumentText,
                location.CurrentText,
                location.ReplacementSpan,
                location.Path,
                location.IsKey,
                binding.Invocation.Arguments,
                node);

            IEnumerable<CommandSuggestion> suggestions = argument.SuggestionProvider?.Invoke(context)
                                                          ?? getAutomaticSuggestions(argument, location);

            var filtered = suggestions
                           .Where(s => string.IsNullOrEmpty(location.CurrentText)
                                       || s.Display.StartsWith(location.CurrentText, StringComparison.OrdinalIgnoreCase)
                                       || s.InsertText.StartsWith(location.CurrentText, StringComparison.OrdinalIgnoreCase))
                           .GroupBy(s => s.InsertText, StringComparer.OrdinalIgnoreCase)
                           .Select(g => g.First())
                           .ToList();

            return new CommandCompletion(argument, location.ReplacementSpan, location.CurrentText, filtered,
                binding.Invocation.Arguments, location.Path, location.IsKey);
        }

        private static IEnumerable<CommandSuggestion> getAutomaticSuggestions(CommandArgument argument, CompletionLocation location)
        {
            Type type = Nullable.GetUnderlyingType(argument.ValueType) ?? argument.ValueType;

            if (location.IsKey)
                return CommandValueConverter.GetObjectFieldSuggestions(type);

            if (location.Path.Count > 0 && location.Path[^1].Kind == CompletionPathKind.ObjectValue)
            {
                Type? valueType = CommandValueConverter.GetDictionaryValueType(type);

                if (valueType == null)
                    valueType = CommandValueConverter.GetObjectFieldType(type, location.Path[^1].Key);

                if (valueType != null)
                    type = Nullable.GetUnderlyingType(valueType) ?? valueType;
            }

            return getTypeSuggestions(type);
        }

        private static IEnumerable<CommandSuggestion> getTypeSuggestions(Type type)
        {
            if (type == typeof(bool))
            {
                foreach (string value in new[] { "true", "false", "t", "f", "+", "-" })
                    yield return new CommandSuggestion(value);
            }
            else if (type.IsEnum)
            {
                foreach (string name in Enum.GetNames(type))
                    yield return new CommandSuggestion(name, Description: getEnumDescription(type, name));
            }
        }

        private static string? getEnumDescription(Type type, string name)
            => type.GetField(name)?.GetCustomAttributes(typeof(CommandEnumMemberAttribute), false)
                   .OfType<CommandEnumMemberAttribute>()
                   .FirstOrDefault()?.Description;

        private static CommandArgument? findTarget(CommandDefinition command, string argumentText, CommandBindingResult binding,
                                                   out CommandArgumentSegment? segment, out bool expectNextArgument)
        {
            segment = null;
            expectNextArgument = false;

            if (command.Arguments.Count == 1)
            {
                var argument = command.Arguments[0];

                if (binding.Segments.TryGetValue(argument.Name, out var single) && single.Name != null)
                    segment = single;
                else
                {
                    // Preserve trailing whitespace so completions insert after it rather than before it.
                    int offset = argumentText.Length - argumentText.TrimStart().Length;
                    string valueText = argumentText.Substring(offset);
                    segment = new CommandArgumentSegment(valueText, new CommandTextSpan(offset, valueText.Length), null,
                        new CommandTextSpan(offset, valueText.Length));
                }

                return argument;
            }

            var segments = CommandArgumentSplitter.Split(argumentText);

            if (segments.Count == 0)
            {
                expectNextArgument = true;
                return command.Arguments[0];
            }

            var mapped = mapSegments(command, segments);

            if (endsWithTopLevelSeparator(argumentText))
            {
                var assigned = new HashSet<CommandArgument>(mapped.Where(m => m.Argument != null).Select(m => m.Argument!));

                expectNextArgument = true;
                return command.Arguments.FirstOrDefault(a => !assigned.Contains(a));
            }

            var last = mapped[^1];
            segment = last.Segment;
            return last.Argument;
        }

        private static List<(CommandArgumentSegment Segment, CommandArgument? Argument)> mapSegments(CommandDefinition command, IReadOnlyList<CommandArgumentSegment> segments)
        {
            var named = new HashSet<CommandArgument>();

            foreach (var segment in segments)
            {
                if (segment.Name == null)
                    continue;

                var argument = command.FindArgument(segment.Name);

                if (argument != null)
                    named.Add(argument);
            }

            var mapped = new List<(CommandArgumentSegment, CommandArgument?)>();
            int cursor = 0;

            foreach (var segment in segments)
            {
                if (segment.Name != null)
                {
                    mapped.Add((segment, command.FindArgument(segment.Name)));
                    continue;
                }

                while (cursor < command.Arguments.Count && named.Contains(command.Arguments[cursor]))
                    cursor++;

                mapped.Add((segment, cursor < command.Arguments.Count ? command.Arguments[cursor++] : null));
            }

            return mapped;
        }

        private static bool endsWithTopLevelSeparator(string text)
        {
            if (text.Length == 0)
                return false;

            int depth = 0;
            char quote = '\0';
            bool escaped = false;

            foreach (char c in text)
            {
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
            }

            if (quote != '\0' || depth > 0)
                return false;

            char final = text[^1];
            return final == ',' || char.IsWhiteSpace(final);
        }

        private static CommandCompletion empty(CommandArgument? argument, string argumentText, IReadOnlyDictionary<string, object?>? arguments = null)
            => new CommandCompletion(argument, new CommandTextSpan(argumentText.Length, 0), string.Empty,
                Array.Empty<CommandSuggestion>(), arguments ?? new Dictionary<string, object?>(), Array.Empty<CompletionPathSegment>(), false);
    }
}
