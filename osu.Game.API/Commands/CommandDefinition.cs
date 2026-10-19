// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// Immutable metadata and execution delegate for a registered command.
    /// </summary>
    public sealed class CommandDefinition
    {
        public string Name { get; }

        public IReadOnlyList<string> Aliases { get; }

        public string? Description { get; }

        public IReadOnlyList<CommandArgument> Arguments { get; }

        internal Action<CommandInvocation> Execute { get; }

        public CommandDefinition(string name, Action<CommandInvocation> execute, IReadOnlyList<CommandArgument>? arguments = null,
                                 string? description = null, IReadOnlyList<string>? aliases = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Command name must not be empty.", nameof(name));

            Name = name;
            Execute = execute;
            Arguments = arguments ?? Array.Empty<CommandArgument>();
            Description = description;
            Aliases = aliases ?? Array.Empty<string>();

            validateArguments();
        }

        public IEnumerable<string> AllNames => new[] { Name }.Concat(Aliases);

        public bool MatchesName(string value) => AllNames.Any(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));

        public CommandArgument? FindArgument(string name) => Arguments.FirstOrDefault(a => a.MatchesName(name));

        /// <summary>The argument portion of <see cref="Usage"/>, e.g. <c>mods...</c>.</summary>
        public string ArgumentUsage => Arguments.Count == 0
            ? string.Empty
            : string.Join(", ", Arguments.Select(a => a.IsOptional ? $"[{a.Name}]" : a.Name));

        /// <summary>A human-readable usage string, e.g. <c>/mods mods...</c>.</summary>
        public string Usage => Arguments.Count == 0 ? $"/{Name}" : $"/{Name} {ArgumentUsage}";

        private void validateArguments()
        {
            bool optionalSeen = false;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var argument in Arguments)
            {
                if (!names.Add(argument.Name))
                    throw new ArgumentException($"Duplicate argument name '{argument.Name}' in command '{Name}'.");

                foreach (string alias in argument.Aliases)
                {
                    if (!names.Add(alias))
                        throw new ArgumentException($"Duplicate argument alias '{alias}' in command '{Name}'.");
                }

                if (argument.IsOptional)
                    optionalSeen = true;
                else if (optionalSeen)
                    throw new ArgumentException($"Required argument '{argument.Name}' cannot follow an optional argument in command '{Name}'.");

                if (argument.DefaultValue != null && !argument.ValueType.IsInstanceOfType(argument.DefaultValue))
                    throw new ArgumentException($"Default value for argument '{argument.Name}' is not a {argument.ValueType.Name}.");
            }
        }
    }
}
