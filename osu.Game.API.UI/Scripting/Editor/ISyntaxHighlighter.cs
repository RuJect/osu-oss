// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.UI.Scripting.Editor
{
    /// <summary>
    /// Classifies JavaScript source into a <see cref="JsTokenType"/> per character.
    /// </summary>
    public interface ISyntaxHighlighter
    {
        /// <summary>
        /// Classifies <paramref name="source"/>. The returned array is exactly the length of the source,
        /// with the classification of the character at the same index.
        /// </summary>
        JsTokenType[] Tokenize(string source);
    }
}
