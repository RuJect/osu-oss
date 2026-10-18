// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.UI.Scripting.Editor
{
    /// <summary>
    /// Classification of a single character of JavaScript source, used to pick a colour when rendering.
    /// </summary>
    public enum JsTokenType
    {
        Plain,
        Keyword,
        String,
        Comment,
        Number,
        Boolean,
        Operator,
        Punctuation,
        Function,
        ClassName,
        Type,
        Property,
        Variable,
    }
}
