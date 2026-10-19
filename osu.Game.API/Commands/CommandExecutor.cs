// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Logging;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// Parses a command line, binds its arguments and invokes the selected command. All failures are returned as a
    /// <see cref="CommandResult"/>; exceptions never escape to callers.
    /// </summary>
    public static class CommandExecutor
    {
        public static CommandResult Execute(CommandRegistry registry, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new CommandResult(CommandStatus.NotFound, message: "no command specified");

            var parsed = CommandInput.Parse(input);

            var command = registry.Find(parsed.Name);

            if (command == null)
            {
                CommandRegistry.Logger.Add($"Unknown command '{parsed.Name}'", LogLevel.Verbose);
                return new CommandResult(CommandStatus.NotFound, message: $"unknown command '{parsed.Name}'");
            }

            return Execute(command, parsed.Argument);
        }

        public static CommandResult Execute(CommandDefinition command, string argument)
        {
            try
            {
                var binding = CommandBinder.Bind(command, argument);

                if (!binding.Success)
                {
                    string message = binding.Errors.Count > 0 ? string.Join("; ", binding.Errors.Select(e => e.ToString())) : "invalid arguments";
                    CommandRegistry.Logger.Add($"Command '{command.Name}' rejected: {message}", LogLevel.Verbose);
                    return new CommandResult(CommandStatus.InvalidArguments, command, binding.Invocation, message, binding.Errors);
                }

                command.Execute(binding.Invocation);
                return new CommandResult(CommandStatus.Success, command, binding.Invocation);
            }
            catch (Exception e)
            {
                CommandRegistry.Logger.Add($"Command '{command.Name}' failed: {e.Message}", LogLevel.Error);
                return new CommandResult(CommandStatus.Error, command, message: e.Message);
            }
        }
    }
}
