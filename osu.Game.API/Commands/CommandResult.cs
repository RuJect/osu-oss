// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.API.Commands.Syntax;

namespace osu.Game.API.Commands
{
    public enum CommandStatus
    {
        Success,
        NotFound,
        InvalidArguments,
        Error,
    }

    /// <summary>The outcome of executing a command string.</summary>
    public sealed class CommandResult
    {
        public CommandStatus Status { get; }

        public CommandDefinition? Command { get; }

        public CommandInvocation? Invocation { get; }

        public string? Message { get; }

        public IReadOnlyList<CommandParseError> Errors { get; }

        public CommandResult(CommandStatus status, CommandDefinition? command = null, CommandInvocation? invocation = null,
                             string? message = null, IReadOnlyList<CommandParseError>? errors = null)
        {
            Status = status;
            Command = command;
            Invocation = invocation;
            Message = message;
            Errors = errors ?? System.Array.Empty<CommandParseError>();
        }

        public bool Succeeded => Status == CommandStatus.Success;
    }
}
