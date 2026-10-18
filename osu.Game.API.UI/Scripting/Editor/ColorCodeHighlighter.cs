// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ColorCode;
using ColorCode.Common;
using ColorCode.Parsing;

namespace osu.Game.API.UI.Scripting.Editor
{
    /// <summary>
    /// A syntax highlighter backed by the ColorCode parser, with a small supplemental pass to cover the
    /// constructs (numbers, operators, function calls, properties) its JavaScript grammar does not emit.
    /// </summary>
    public partial class ColorCodeHighlighter : CodeColorizerBase, ISyntaxHighlighter
    {
        private static readonly Regex number_regex = new Regex(
            @"\b(0[xX][0-9a-fA-F]+|0[bB][01]+|0[oO][0-7]+|(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?)\b",
            RegexOptions.Compiled);

        private static readonly Regex function_regex = new Regex(
            @"\b[A-Za-z_$][A-Za-z0-9_$]*(?=\s*\()",
            RegexOptions.Compiled);

        private static readonly Regex property_regex = new Regex(
            @"(?<=\.)[A-Za-z_$][A-Za-z0-9_$]*",
            RegexOptions.Compiled);

        private static readonly Regex operator_regex = new Regex(
            @"[+\-*/%=<>!&|^~?]+",
            RegexOptions.Compiled);

        private static readonly Regex punctuation_regex = new Regex(
            @"[{}()\[\];,.:]",
            RegexOptions.Compiled);

        public ColorCodeHighlighter()
            : base(null!, null!)
        {
        }

        protected override void Write(string parsedSourceCode, IList<Scope> scopes)
        {
            // The base class API is only used for its parser; output is produced by Tokenize.
        }

        public JsTokenType[] Tokenize(string source)
        {
            var types = new JsTokenType[source.Length];

            if (source.Length == 0)
                return types;

            int offset = 0;

            languageParser.Parse(source, Languages.JavaScript, (fragment, scopes) =>
            {
                foreach (Scope scope in scopes)
                    applyScope(scope, types, offset, source.Length);

                offset += fragment.Length;
            });

            applySupplement(types, source, function_regex, JsTokenType.Function);
            applySupplement(types, source, property_regex, JsTokenType.Property);
            applySupplement(types, source, number_regex, JsTokenType.Number);
            applySupplement(types, source, operator_regex, JsTokenType.Operator);
            applySupplement(types, source, punctuation_regex, JsTokenType.Punctuation);

            return types;
        }

        private static void applyScope(Scope scope, JsTokenType[] types, int offset, int length)
        {
            JsTokenType type = mapScope(scope.Name);

            int start = Math.Clamp(scope.Index + offset, 0, length);
            int end = Math.Clamp(scope.Index + scope.Length + offset, 0, length);

            for (int i = start; i < end; i++)
                types[i] = type;

            foreach (Scope child in scope.Children)
                applyScope(child, types, offset, length);
        }

        private static void applySupplement(JsTokenType[] types, string source, Regex regex, JsTokenType type)
        {
            foreach (Match match in regex.Matches(source))
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    if (types[i] == JsTokenType.Plain)
                        types[i] = type;
                }
            }
        }

        private static JsTokenType mapScope(string name) => name switch
        {
            ScopeName.Comment or ScopeName.XmlComment or ScopeName.HtmlComment => JsTokenType.Comment,
            ScopeName.String or ScopeName.StringCSharpVerbatim or ScopeName.JsonString => JsTokenType.String,
            ScopeName.Keyword or ScopeName.PreprocessorKeyword => JsTokenType.Keyword,
            ScopeName.Number or ScopeName.JsonNumber => JsTokenType.Number,
            ScopeName.BuiltinFunction or ScopeName.SqlSystemFunction => JsTokenType.Function,
            ScopeName.ClassName => JsTokenType.ClassName,
            ScopeName.Type or ScopeName.TypeVariable => JsTokenType.Type,
            ScopeName.Operator or ScopeName.PowerShellOperator => JsTokenType.Operator,
            _ => JsTokenType.Plain,
        };
    }
}
