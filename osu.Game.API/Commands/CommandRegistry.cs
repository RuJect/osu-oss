// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;

namespace osu.Game.API.Commands
{
    /// <summary>A command together with the fuzzy match information used to rank/filter it.</summary>
    public sealed record CommandMatch(CommandDefinition Command, int Score, IReadOnlyList<int> Indices);

    /// <summary>
    /// Stores and searches registered commands. Parsing and execution are delegated to <see cref="CommandExecutor"/>.
    /// </summary>
    public class CommandRegistry
    {
        private static readonly Logger logger = Logger.GetLogger("commands");

        private readonly Dictionary<string, CommandDefinition> byName = new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CommandDefinition> commands = new List<CommandDefinition>();

        /// <summary>Raised whenever the set of registered commands changes.</summary>
        public event Action? Changed;

        public IReadOnlyList<CommandDefinition> Commands => commands;

        /// <summary>Starts building a declarative command. The command is registered once a handler is supplied.</summary>
        public CommandBuilder Add(string name) => new CommandBuilder(this, name);

        /// <summary>Registers <paramref name="command"/>.</summary>
        /// <returns>A token which unregisters the command when disposed.</returns>
        public IDisposable Register(CommandDefinition command)
        {
            foreach (string name in command.AllNames)
            {
                if (byName.TryGetValue(name, out var existing) && existing != command)
                    throw new InvalidOperationException($"A command named '{name}' is already registered.");
            }

            commands.Add(command);

            foreach (string name in command.AllNames)
                byName[name] = command;

            Changed?.Invoke();

            return new InvokeOnDisposal(() => Unregister(command));
        }

        /// <summary>Unregisters the command registered under <paramref name="name"/> (or one of its aliases).</summary>
        public bool Unregister(string name)
        {
            if (!byName.TryGetValue(name, out var command))
                return false;

            return Unregister(command);
        }

        /// <summary>Unregisters <paramref name="command"/>.</summary>
        public bool Unregister(CommandDefinition command)
        {
            if (!commands.Remove(command))
                return false;

            foreach (string name in command.AllNames)
            {
                if (byName.TryGetValue(name, out var existing) && existing == command)
                    byName.Remove(name);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Finds a command by its exact name or an alias (case-insensitive).</summary>
        public CommandDefinition? Find(string name)
        {
            name = name.Trim();

            if (byName.TryGetValue(name, out var command))
                return command;

            return commands.FirstOrDefault(c => c.MatchesName(name));
        }

        /// <summary>Fuzzy-searches the registered commands.</summary>
        public IReadOnlyList<CommandMatch> Search(string query)
        {
            query = query.Trim();

            if (string.IsNullOrEmpty(query))
                return commands.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(c => new CommandMatch(c, 0, Array.Empty<int>())).ToList();

            var matches = new List<CommandMatch>();

            foreach (var command in commands)
            {
                var nameMatch = FuzzyMatcher.Match(query, command.Name);

                if (nameMatch != null)
                {
                    matches.Add(new CommandMatch(command, nameMatch.Value.Score, nameMatch.Value.Indices));
                    continue;
                }

                // fall back to aliases, but rank them below name matches.
                foreach (string alias in command.Aliases)
                {
                    var aliasMatch = FuzzyMatcher.Match(query, alias);

                    if (aliasMatch != null)
                    {
                        matches.Add(new CommandMatch(command, aliasMatch.Value.Score - 100, aliasMatch.Value.Indices));
                        break;
                    }
                }
            }

            return matches.OrderByDescending(m => m.Score).ThenBy(m => m.Command.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Parses and executes a raw command string (with or without a leading <c>/</c>). Errors are returned rather
        /// than thrown.
        /// </summary>
        public CommandResult Execute(string input) => CommandExecutor.Execute(this, input);

        /// <summary>Parses <paramref name="argument"/> and executes <paramref name="command"/>.</summary>
        public CommandResult Execute(CommandDefinition command, string argument) => CommandExecutor.Execute(command, argument);

        internal static Logger Logger => logger;

        private sealed class InvokeOnDisposal : IDisposable
        {
            private Action? action;

            public InvokeOnDisposal(Action action)
            {
                this.action = action;
            }

            public void Dispose()
            {
                action?.Invoke();
                action = null;
            }
        }
    }
}
