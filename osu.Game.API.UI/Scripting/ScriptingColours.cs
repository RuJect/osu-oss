// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.API.Services;
using osuTK.Graphics;

namespace osu.Game.API.UI.Scripting
{
    /// <summary>
    /// Shared colour palette for the scripting console. Values are chosen to match the osu! dark theme.
    /// </summary>
    public static class ScriptingColours
    {
        public static Color4 DimBackground { get; } = new Color4(0, 0, 0, 180);
        public static Color4 PanelBackground { get; } = new Color4(20, 20, 26, 255);
        public static Color4 Surface { get; } = new Color4(30, 30, 38, 255);
        public static Color4 SurfaceLight { get; } = new Color4(42, 42, 52, 255);
        public static Color4 SurfaceHover { get; } = new Color4(52, 52, 64, 255);
        public static Color4 Border { get; } = new Color4(58, 58, 72, 255);

        public static Color4 Accent { get; } = new Color4(255, 102, 170, 255);

        public static Color4 Text { get; } = new Color4(235, 235, 242, 255);
        public static Color4 TextDim { get; } = new Color4(150, 150, 166, 255);

        public static Color4 Selection { get; } = new Color4(255, 102, 170, 70);
        public static Color4 Caret { get; } = new Color4(255, 255, 255, 255);

        public static Color4 LevelLog { get; } = new Color4(205, 205, 216, 255);
        public static Color4 LevelInfo { get; } = new Color4(102, 204, 255, 255);
        public static Color4 LevelWarn { get; } = new Color4(255, 204, 102, 255);
        public static Color4 LevelError { get; } = new Color4(255, 102, 130, 255);

        public static Color4 TokenPlain { get; } = new Color4(228, 228, 238, 255);
        public static Color4 TokenKeyword { get; } = new Color4(255, 102, 170, 255);
        public static Color4 TokenString { get; } = new Color4(179, 217, 68, 255);
        public static Color4 TokenComment { get; } = new Color4(122, 122, 138, 255);
        public static Color4 TokenNumber { get; } = new Color4(255, 204, 102, 255);
        public static Color4 TokenBoolean { get; } = new Color4(255, 138, 91, 255);
        public static Color4 TokenOperator { get; } = new Color4(198, 198, 214, 255);
        public static Color4 TokenPunctuation { get; } = new Color4(160, 160, 176, 255);
        public static Color4 TokenFunction { get; } = new Color4(102, 204, 255, 255);
        public static Color4 TokenClassName { get; } = new Color4(120, 224, 200, 255);
        public static Color4 TokenType { get; } = new Color4(120, 224, 200, 255);
        public static Color4 TokenProperty { get; } = new Color4(255, 214, 121, 255);

        public static Color4 ForToken(Editor.JsTokenType type) => type switch
        {
            Editor.JsTokenType.Keyword => TokenKeyword,
            Editor.JsTokenType.String => TokenString,
            Editor.JsTokenType.Comment => TokenComment,
            Editor.JsTokenType.Number => TokenNumber,
            Editor.JsTokenType.Boolean => TokenBoolean,
            Editor.JsTokenType.Operator => TokenOperator,
            Editor.JsTokenType.Punctuation => TokenPunctuation,
            Editor.JsTokenType.Function => TokenFunction,
            Editor.JsTokenType.ClassName => TokenClassName,
            Editor.JsTokenType.Type => TokenType,
            Editor.JsTokenType.Property => TokenProperty,
            _ => TokenPlain,
        };

        public static Color4 ForLevel(OutputLevel level) => level switch
        {
            OutputLevel.Info => LevelInfo,
            OutputLevel.Warn => LevelWarn,
            OutputLevel.Error => LevelError,
            _ => LevelLog,
        };
    }
}
