// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.API.Scripting;
using osuTK.Graphics;
using osu.Game.API.Services;

namespace osu.Game.API.UI.Scripting
{
    /// <summary>
    /// Shared colour palette for the scripting console. Values are chosen to match the osu! dark theme.
    /// </summary>
    public static class ScriptingColours
    {
        public static readonly Color4 DimBackground = new Color4(0, 0, 0, 180);
        public static readonly Color4 PanelBackground = new Color4(20, 20, 26, 255);
        public static readonly Color4 Surface = new Color4(30, 30, 38, 255);
        public static readonly Color4 SurfaceLight = new Color4(42, 42, 52, 255);
        public static readonly Color4 SurfaceHover = new Color4(52, 52, 64, 255);
        public static readonly Color4 Border = new Color4(58, 58, 72, 255);

        public static readonly Color4 Accent = new Color4(255, 102, 170, 255);

        public static readonly Color4 Text = new Color4(235, 235, 242, 255);
        public static readonly Color4 TextDim = new Color4(150, 150, 166, 255);

        public static readonly Color4 Selection = new Color4(255, 102, 170, 70);
        public static readonly Color4 Caret = new Color4(255, 255, 255, 255);

        public static readonly Color4 LevelLog = new Color4(205, 205, 216, 255);
        public static readonly Color4 LevelInfo = new Color4(102, 204, 255, 255);
        public static readonly Color4 LevelWarn = new Color4(255, 204, 102, 255);
        public static readonly Color4 LevelError = new Color4(255, 102, 130, 255);

        public static readonly Color4 TokenPlain = new Color4(228, 228, 238, 255);
        public static readonly Color4 TokenKeyword = new Color4(255, 102, 170, 255);
        public static readonly Color4 TokenString = new Color4(179, 217, 68, 255);
        public static readonly Color4 TokenComment = new Color4(122, 122, 138, 255);
        public static readonly Color4 TokenNumber = new Color4(255, 204, 102, 255);
        public static readonly Color4 TokenBoolean = new Color4(255, 138, 91, 255);
        public static readonly Color4 TokenOperator = new Color4(198, 198, 214, 255);
        public static readonly Color4 TokenPunctuation = new Color4(160, 160, 176, 255);
        public static readonly Color4 TokenFunction = new Color4(102, 204, 255, 255);
        public static readonly Color4 TokenClassName = new Color4(120, 224, 200, 255);
        public static readonly Color4 TokenType = new Color4(120, 224, 200, 255);
        public static readonly Color4 TokenProperty = new Color4(255, 214, 121, 255);

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
