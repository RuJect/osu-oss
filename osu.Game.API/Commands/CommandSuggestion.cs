// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Commands
{
    /// <summary>
    /// A completion suggestion. <see cref="InsertText"/> is the final text to insert in place of the fragment span
    /// supplied by the completion context.
    /// </summary>
    public sealed record CommandSuggestion(string InsertText, string? DisplayText = null, string? Description = null)
    {
        public string Display => DisplayText ?? InsertText;
    }

    public enum CompletionPathKind
    {
        ArrayElement,
        ObjectKey,
        ObjectValue,
        DictionaryKey,
        DictionaryValue,
    }

    /// <summary>One step into a nested value, identifying the element currently being edited.</summary>
    public readonly record struct CompletionPathSegment(CompletionPathKind Kind, int Index = -1, string? Key = null);
}
