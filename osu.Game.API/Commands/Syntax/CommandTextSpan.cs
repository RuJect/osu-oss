// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Commands.Syntax
{
    /// <summary>
    /// A half-open range of characters within a command input string.
    /// </summary>
    public readonly record struct CommandTextSpan(int Start, int Length)
    {
        public int End => Start + Length;

        public static CommandTextSpan Empty(int position) => new CommandTextSpan(position, 0);

        /// <summary>Whether <paramref name="position"/> lies within the span (inclusive of the end).</summary>
        public bool Contains(int position) => position >= Start && position <= End;

        public string Slice(string text)
        {
            if (Start < 0 || Length <= 0 || Start >= text.Length)
                return string.Empty;

            return text.Substring(Start, System.Math.Min(Length, text.Length - Start));
        }

        public override string ToString() => $"[{Start}..{End})";
    }

    /// <summary>
    /// A syntax or binding error tied to a specific range of the input.
    /// </summary>
    public sealed class CommandParseError
    {
        public string Message { get; }

        public CommandTextSpan Span { get; }

        /// <summary>The name of the argument the error relates to, when known.</summary>
        public string? ArgumentName { get; }

        public CommandParseError(string message, CommandTextSpan span, string? argumentName = null)
        {
            Message = message;
            Span = span;
            ArgumentName = argumentName;
        }

        public override string ToString() => ArgumentName == null ? Message : $"{ArgumentName}: {Message}";
    }
}
