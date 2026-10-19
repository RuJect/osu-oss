// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.API.Commands.Syntax;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// The result of binding an argument string to a command definition. Partial values and syntax nodes are exposed
    /// even when errors are present so the command palette can provide completions while the user is typing.
    /// </summary>
    public sealed class CommandBindingResult
    {
        public CommandInvocation Invocation { get; }

        public IReadOnlyDictionary<string, CommandValueNode> Nodes { get; }

        public IReadOnlyDictionary<string, CommandArgumentSegment> Segments { get; }

        public IReadOnlyList<CommandParseError> Errors { get; }

        public bool Success => Errors.Count == 0;

        public CommandBindingResult(CommandInvocation invocation, IReadOnlyDictionary<string, CommandValueNode> nodes,
                                    IReadOnlyDictionary<string, CommandArgumentSegment> segments, IReadOnlyList<CommandParseError> errors)
        {
            Invocation = invocation;
            Nodes = nodes;
            Segments = segments;
            Errors = errors;
        }
    }

    /// <summary>
    /// Binds a raw argument string to a <see cref="CommandDefinition"/>, applying positional/named argument rules,
    /// single-argument shorthands and defaults.
    /// </summary>
    public static class CommandBinder
    {
        public static CommandBindingResult Bind(CommandDefinition command, string argumentText)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var nodes = new Dictionary<string, CommandValueNode>(StringComparer.OrdinalIgnoreCase);
            var segments = new Dictionary<string, CommandArgumentSegment>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<CommandParseError>();

            if (command.Arguments.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(argumentText))
                    errors.Add(new CommandParseError("this command does not accept arguments", new CommandTextSpan(0, argumentText.Length)));

                return buildResult(command, argumentText, values, nodes, segments, errors);
            }

            if (command.Arguments.Count == 1)
                bindSingle(command.Arguments[0], argumentText);
            else
                bindMany(command, argumentText);

            return buildResult(command, argumentText, values, nodes, segments, errors);

            void bindSingle(CommandArgument argument, string text)
            {
                if (trySplitNamed(text, argument, out string valueText, out int valueOffset, out string? explicitName))
                {
                    var segment = new CommandArgumentSegment(explicitName + "=" + valueText, new CommandTextSpan(0, text.Length), explicitName,
                        new CommandTextSpan(valueOffset, valueText.Length));

                    segments[argument.Name] = segment;
                    bindValue(argument, valueText, valueOffset, segment.Span);
                    return;
                }

                string trimmed = text.Trim();
                int offset = text.Length - text.TrimStart().Length;

                var unnamedSegment = new CommandArgumentSegment(trimmed, new CommandTextSpan(offset, trimmed.Length), null,
                    new CommandTextSpan(offset, trimmed.Length));

                segments[argument.Name] = unnamedSegment;

                if (trimmed.Length == 0)
                {
                    applyMissing(argument, unnamedSegment.Span);
                    return;
                }

                bindValue(argument, trimmed, offset, unnamedSegment.Span);
            }

            void bindMany(CommandDefinition definition, string text)
            {
                var parsedSegments = CommandArgumentSplitter.Split(text);
                var assigned = new HashSet<CommandArgument>();
                var positioned = new List<CommandArgumentSegment>();

                foreach (var segment in parsedSegments)
                {
                    if (segment.Name == null)
                    {
                        positioned.Add(segment);
                        continue;
                    }

                    var argument = definition.FindArgument(segment.Name);

                    if (argument == null)
                    {
                        errors.Add(new CommandParseError($"unknown argument '{segment.Name}'", segment.Span, segment.Name));
                        continue;
                    }

                    if (!assigned.Add(argument))
                    {
                        errors.Add(new CommandParseError($"argument '{argument.Name}' was specified more than once", segment.Span, argument.Name));
                        continue;
                    }

                    segments[argument.Name] = segment;
                    bindValue(argument, segment.ValueText, segment.ValueSpan.Start, segment.Span);
                }

                int positionedIndex = 0;

                foreach (var argument in definition.Arguments)
                {
                    if (assigned.Contains(argument))
                        continue;

                    if (positionedIndex < positioned.Count)
                    {
                        var segment = positioned[positionedIndex++];
                        assigned.Add(argument);
                        segments[argument.Name] = segment;
                        bindValue(argument, segment.ValueText, segment.ValueSpan.Start, segment.Span);
                    }
                    else
                        applyMissing(argument, new CommandTextSpan(text.Length, 0));
                }

                for (int i = positionedIndex; i < positioned.Count; i++)
                    errors.Add(new CommandParseError("too many arguments", positioned[i].Span));
            }

            void bindValue(CommandArgument argument, string valueText, int offset, CommandTextSpan span)
            {
                if (string.IsNullOrWhiteSpace(valueText))
                {
                    applyMissing(argument, span);
                    return;
                }

                var parseResult = parseValue(argument, valueText, offset);

                if (parseResult.Node == null)
                {
                    errors.AddRange(parseResult.Errors);
                    return;
                }

                nodes[argument.Name] = parseResult.Node;

                foreach (var error in parseResult.Errors)
                    errors.Add(new CommandParseError(error.Message, error.Span, argument.Name));

                if (!CommandValueConverter.TryConvert(parseResult.Node, argument.ValueType, out object? converted, out string? conversionError))
                {
                    errors.Add(new CommandParseError(conversionError!, span, argument.Name));
                    return;
                }

                values[argument.Name] = converted;
            }

            void applyMissing(CommandArgument argument, CommandTextSpan span)
            {
                if (!argument.IsOptional)
                {
                    errors.Add(new CommandParseError($"missing required argument '{argument.Name}'", span, argument.Name));
                    return;
                }

                values[argument.Name] = argument.DefaultValue ?? defaultValue(argument.ValueType);
            }
        }

        private static CommandValueParseResult parseValue(CommandArgument argument, string text, int offset)
        {
            Type type = Nullable.GetUnderlyingType(argument.ValueType) ?? argument.ValueType;
            string trimmed = text.Trim();
            int trimOffset = offset + (text.Length - text.TrimStart().Length);

            if (type == typeof(string) || type == typeof(char) || type == typeof(object))
            {
                if (trimmed.StartsWith('"') || trimmed.StartsWith('\''))
                {
                    var quoted = CommandValueParser.Parse(trimmed, trimOffset);

                    if (quoted.Node is CommandScalarNode { Quoted: true })
                        return quoted;
                }

                return new CommandValueParseResult(
                    new CommandScalarNode(new CommandTextSpan(trimOffset, trimmed.Length), trimmed, false, CommandScalarKind.String, trimmed),
                    true, Array.Empty<CommandParseError>());
            }

            if (isArrayLike(type))
            {
                return trimmed.StartsWith('[')
                           ? CommandValueParser.Parse(trimmed, trimOffset)
                           : CommandValueParser.ParseSequence(trimmed, trimOffset);
            }

            if (isObjectLike(type))
            {
                return trimmed.StartsWith('{')
                           ? CommandValueParser.Parse(trimmed, trimOffset)
                           : CommandValueParser.ParseUnwrappedObject(trimmed, trimOffset);
            }

            return CommandValueParser.Parse(trimmed, trimOffset);
        }

        private static bool trySplitNamed(string text, CommandArgument argument, out string valueText, out int valueOffset, out string? name)
        {
            valueText = string.Empty;
            valueOffset = 0;
            name = null;

            int start = 0;

            while (start < text.Length && char.IsWhiteSpace(text[start]))
                start++;

            int equals = text.IndexOf('=', start);

            if (equals <= start)
                return false;

            string candidate = text.Substring(start, equals - start);

            if (!isValidName(candidate) || !argument.MatchesName(candidate))
                return false;

            int valueStart = equals + 1;

            while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
                valueStart++;

            valueText = text.Substring(valueStart).TrimEnd();
            valueOffset = valueStart;
            name = argument.Name;
            return true;
        }

        private static bool isValidName(string name)
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

        internal static bool isArrayLike(Type type)
        {
            if (type.IsArray)
                return true;

            if (!type.IsGenericType)
                return false;

            Type definition = type.GetGenericTypeDefinition();

            return definition == typeof(List<>)
                   || definition == typeof(IList<>)
                   || definition == typeof(IReadOnlyList<>)
                   || definition == typeof(IEnumerable<>)
                   || definition == typeof(ICollection<>)
                   || definition == typeof(IReadOnlyCollection<>)
                   || definition == typeof(HashSet<>)
                   || definition == typeof(ISet<>)
                   || definition == typeof(IReadOnlySet<>);
        }

        internal static bool isObjectLike(Type type)
        {
            if (type == typeof(string) || type == typeof(object) || type.IsEnum || type.IsPrimitive)
                return false;

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();

                if (definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                    return true;
            }

            if (type.IsArray)
                return false;

            return type.IsClass || type.IsValueType;
        }

        private static object? defaultValue(Type type)
            => type.IsValueType && Nullable.GetUnderlyingType(type) == null ? Activator.CreateInstance(type) : null;

        private static CommandBindingResult buildResult(CommandDefinition command, string argumentText, Dictionary<string, object?> values,
                                                        Dictionary<string, CommandValueNode> nodes, Dictionary<string, CommandArgumentSegment> segments,
                                                        List<CommandParseError> errors)
        {
            var invocation = new CommandInvocation(command, argumentText, values);
            return new CommandBindingResult(invocation, nodes, segments, errors);
        }
    }
}
