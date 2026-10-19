// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// A raw command line split into its name query and argument parts. The name query is matched fuzzily against
    /// the registered commands; the argument is everything after the first whitespace.
    /// </summary>
    public readonly struct CommandInput
    {
        private static readonly SearchValues<char> whitespace = SearchValues.Create(" \t");

        /// <summary>The command name being typed (may be partial).</summary>
        public string Name { get; }

        /// <summary>The argument string following the command name.</summary>
        public string Argument { get; }

        /// <summary>Whether a whitespace separator was present (i.e. the user has started typing arguments).</summary>
        public bool HasArgument { get; }

        public CommandInput(string name, string argument, bool hasArgument)
        {
            Name = name;
            Argument = argument;
            HasArgument = hasArgument;
        }

        /// <summary>
        /// Parses a raw command line. A leading <c>/</c> is ignored.
        /// </summary>
        public static CommandInput Parse(string? input)
        {
            string text = (input ?? string.Empty).TrimStart();

            if (text.StartsWith('/'))
                text = text.Substring(1);

            int separator = text.AsSpan().IndexOfAny(whitespace);

            if (separator < 0)
                return new CommandInput(text.Trim(), string.Empty, false);

            return new CommandInput(text.Substring(0, separator).Trim(), text.Substring(separator + 1).TrimStart(), true);
        }
    }
}
