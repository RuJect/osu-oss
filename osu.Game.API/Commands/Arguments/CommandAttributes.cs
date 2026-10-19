// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.API.Commands
{
    /// <summary>Provides a human readable description for a command argument type.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
    public sealed class CommandDescriptionAttribute : Attribute
    {
        public string Description { get; }

        public CommandDescriptionAttribute(string description)
        {
            Description = description;
        }
    }

    /// <summary>Provides a human readable description for an enum value offered as a suggestion.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CommandEnumMemberAttribute : Attribute
    {
        public string? Description { get; init; }
    }

    /// <summary>Overrides the inferred name, documentation and optionality of an object field.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class CommandParameterAttribute : Attribute
    {
        public string? Name { get; init; }

        public string? Description { get; init; }

        public bool Optional { get; init; }

        public object? Default { get; init; }

        public string[] Aliases { get; init; } = Array.Empty<string>();
    }
}
