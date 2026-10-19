// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// Declarative metadata for one argument of a command.
    /// </summary>
    public sealed class CommandArgument
    {
        public string Name { get; }

        public IReadOnlyList<string> Aliases { get; }

        public string? Description { get; }

        public Type ValueType { get; }

        public bool IsOptional { get; }

        public object? DefaultValue { get; }

        /// <summary>
        /// Provides completion suggestions for the fragment currently being typed. The context contains the values
        /// of surrounding arguments and, for composite values, the path to the current element.
        /// </summary>
        public Func<ArgumentCompletionContext, IEnumerable<CommandSuggestion>>? SuggestionProvider { get; }

        public CommandArgument(string name, Type valueType, bool optional = false, object? defaultValue = null,
                               string? description = null, IReadOnlyList<string>? aliases = null,
                               Func<ArgumentCompletionContext, IEnumerable<CommandSuggestion>>? suggestionProvider = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument name must not be empty.", nameof(name));

            Name = name;
            ValueType = valueType;
            IsOptional = optional;
            DefaultValue = defaultValue;
            Description = description;
            Aliases = aliases ?? Array.Empty<string>();
            SuggestionProvider = suggestionProvider;
        }

        public bool MatchesName(string name) => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase)
                                                || Aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        public override string ToString() => IsOptional ? $"[{Name}: {ValueType.Name}]" : $"{Name}: {ValueType.Name}";
    }

    /// <summary>
    /// Fluent configuration for a single command argument.
    /// </summary>
    public sealed class CommandArgumentBuilder<T>
    {
        private readonly string name;
        private readonly List<string> aliases = new List<string>();

        private bool optional;
        private object? defaultValue;
        private string? description;
        private Func<ArgumentCompletionContext, IEnumerable<CommandSuggestion>>? suggestionProvider;

        public CommandArgumentBuilder(string name)
        {
            this.name = name;
        }

        public CommandArgumentBuilder<T> Alias(string alias)
        {
            aliases.Add(alias);
            return this;
        }

        public CommandArgumentBuilder<T> Description(string description)
        {
            this.description = description;
            return this;
        }

        public CommandArgumentBuilder<T> Optional(T defaultValue = default!)
        {
            optional = true;
            this.defaultValue = defaultValue;
            return this;
        }

        public CommandArgumentBuilder<T> WithDefault(T defaultValue)
        {
            optional = true;
            this.defaultValue = defaultValue;
            return this;
        }

        /// <summary>Provides suggestions for the scalar currently being edited.</summary>
        public CommandArgumentBuilder<T> Suggest(Func<ArgumentCompletionContext, IEnumerable<CommandSuggestion>> provider)
        {
            suggestionProvider = provider;
            return this;
        }

        /// <summary>Provides suggestions for each scalar element of an array argument.</summary>
        public CommandArgumentBuilder<T> SuggestElements(Func<ElementCompletionContext, IEnumerable<CommandSuggestion>> provider)
        {
            suggestionProvider = context => context.SuggestElements(provider);
            return this;
        }

        /// <summary>Provides suggestions for each field of an object argument.</summary>
        public CommandArgumentBuilder<T> SuggestFields(Func<FieldCompletionContext, IEnumerable<CommandSuggestion>> provider)
        {
            suggestionProvider = context => context.SuggestFields(provider);
            return this;
        }

        internal CommandArgument Build()
            => new CommandArgument(name, typeof(T), optional, defaultValue, description, aliases, suggestionProvider);
    }
}
