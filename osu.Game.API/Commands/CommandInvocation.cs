// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// A parsed and bound command ready for execution.
    /// </summary>
    public sealed class CommandInvocation
    {
        public CommandDefinition Command { get; }

        /// <summary>The raw argument text supplied by the caller.</summary>
        public string ArgumentText { get; }

        /// <summary>Bound values keyed by canonical argument name.</summary>
        public IReadOnlyDictionary<string, object?> Arguments { get; }

        public CommandInvocation(CommandDefinition command, string argumentText, IReadOnlyDictionary<string, object?> arguments)
        {
            Command = command;
            ArgumentText = argumentText;
            Arguments = arguments;
        }

        public object? Get(string name) => Arguments.TryGetValue(name, out object? value) ? value : null;

        public T Get<T>(string name)
        {
            if (!Arguments.TryGetValue(name, out object? value))
                throw new ArgumentException($"No argument named '{name}' was bound.", nameof(name));

            if (value is T typed)
                return typed;

            if (value == null)
                return default!;

            throw new InvalidCastException($"Argument '{name}' is a {value.GetType().Name}, not a {typeof(T).Name}.");
        }
    }
}
