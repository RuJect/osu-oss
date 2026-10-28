// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using osu.Framework.Graphics;

namespace osu.Game.Theming
{
    /// <summary>
    /// Resolves the small colour expression language used by theme files.
    /// </summary>
    /// <remarks>
    /// Supported expressions:
    /// <list type="bullet">
    /// <item><description><c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c></description></item>
    /// <item><description><c>@token</c> referencing another colour in the same theme</description></item>
    /// <item><description><c>lighten(@token, 0.1)</c>, <c>darken</c>, <c>saturate</c>, <c>desaturate</c></description></item>
    /// <item><description><c>alpha(@token, 0.8)</c>, <c>hue_shift(@token, 30)</c>, <c>grayscale(@token)</c></description></item>
    /// <item><description><c>mix(@a, @b, 0.5)</c>, <c>hsl(270, 0.6, 0.5)</c>, <c>rgb(40, 30, 60)</c></description></item>
    /// </list>
    /// </remarks>
    public static class ThemeColourExpression
    {
        public static Colour4 Resolve(string expression, IReadOnlyDictionary<string, string> tokens)
            => resolve(expression, tokens, new HashSet<string>());

        private static Colour4 resolve(string expression, IReadOnlyDictionary<string, string> tokens, HashSet<string> resolving)
        {
            expression = expression.Trim();

            if (expression.Length == 0)
                throw new ThemeLoadException("Colour expression cannot be empty.");

            if (expression[0] == '@')
            {
                string token = expression.Substring(1);

                if (!tokens.TryGetValue(token, out string? referenced))
                    throw new ThemeLoadException($"Unknown theme colour token '@{token}'.");

                if (!resolving.Add(token))
                    throw new ThemeLoadException($"Circular theme colour reference involving '@{token}'.");

                try
                {
                    return resolve(referenced, tokens, resolving);
                }
                finally
                {
                    resolving.Remove(token);
                }
            }

            if (expression[0] == '#')
            {
                try
                {
                    return Colour4.FromHex(expression);
                }
                catch (Exception e)
                {
                    throw new ThemeLoadException($"Invalid colour '{expression}'.", e);
                }
            }

            int open = expression.IndexOf('(');

            if (open > 0 && expression.EndsWith(')'))
            {
                string function = expression.Substring(0, open).Trim().ToLowerInvariant();
                string argumentString = expression.Substring(open + 1, expression.Length - open - 2);
                string[] arguments = splitArguments(argumentString);

                switch (function)
                {
                    case "lighten":
                    case "darken":
                    case "saturate":
                    case "desaturate":
                    {
                        requireArguments(function, arguments, 2);
                        Colour4 colour = resolve(arguments[0], tokens, resolving);
                        float amount = parseFloat(arguments[1], function);
                        Vector4 hsl = colour.ToHSL();

                        switch (function)
                        {
                            case "lighten":
                                hsl.Z = Math.Clamp(hsl.Z + amount, 0, 1);
                                break;

                            case "darken":
                                hsl.Z = Math.Clamp(hsl.Z - amount, 0, 1);
                                break;

                            case "saturate":
                                hsl.Y = Math.Clamp(hsl.Y + amount, 0, 1);
                                break;

                            case "desaturate":
                                hsl.Y = Math.Clamp(hsl.Y - amount, 0, 1);
                                break;
                        }

                        return withHslAlpha(hsl, colour.A);
                    }

                    case "alpha":
                    {
                        requireArguments(function, arguments, 2);
                        Colour4 colour = resolve(arguments[0], tokens, resolving);
                        float alpha = Math.Clamp(parseFloat(arguments[1], function), 0, 1);
                        return new Colour4(colour.R, colour.G, colour.B, alpha);
                    }

                    case "hue_shift":
                    {
                        requireArguments(function, arguments, 2);
                        Colour4 colour = resolve(arguments[0], tokens, resolving);
                        Vector4 hsl = colour.ToHSL();
                        hsl.X = (hsl.X + parseFloat(arguments[1], function) / 360f % 1f + 1f) % 1f;
                        return withHslAlpha(hsl, colour.A);
                    }

                    case "grayscale":
                    {
                        requireArguments(function, arguments, 1);
                        Colour4 colour = resolve(arguments[0], tokens, resolving);
                        Vector4 hsl = colour.ToHSL();
                        hsl.Y = 0;
                        return withHslAlpha(hsl, colour.A);
                    }

                    case "mix":
                    {
                        requireArguments(function, arguments, 3);
                        Colour4 first = resolve(arguments[0], tokens, resolving);
                        Colour4 second = resolve(arguments[1], tokens, resolving);
                        float amount = Math.Clamp(parseFloat(arguments[2], function), 0, 1);

                        return new Colour4(
                            first.R + (second.R - first.R) * amount,
                            first.G + (second.G - first.G) * amount,
                            first.B + (second.B - first.B) * amount,
                            first.A + (second.A - first.A) * amount);
                    }

                    case "hsl":
                    {
                        if (arguments.Length != 3 && arguments.Length != 4)
                            throw new ThemeLoadException("hsl() expects 3 or 4 arguments.");

                        float hue = parseFloat(arguments[0], function) / 360f % 1f;
                        float saturation = Math.Clamp(parseFloat(arguments[1], function), 0, 1);
                        float lightness = Math.Clamp(parseFloat(arguments[2], function), 0, 1);
                        float alpha = arguments.Length == 4 ? Math.Clamp(parseFloat(arguments[3], function), 0, 1) : 1;

                        if (hue < 0)
                            hue += 1;

                        return Colour4.FromHSL(hue, saturation, lightness).Opacity(alpha);
                    }

                    case "rgb":
                    {
                        if (arguments.Length != 3 && arguments.Length != 4)
                            throw new ThemeLoadException("rgb() expects 3 or 4 arguments.");

                        float red = Math.Clamp(parseFloat(arguments[0], function), 0, 255) / 255f;
                        float green = Math.Clamp(parseFloat(arguments[1], function), 0, 255) / 255f;
                        float blue = Math.Clamp(parseFloat(arguments[2], function), 0, 255) / 255f;
                        float alpha = arguments.Length == 4 ? Math.Clamp(parseFloat(arguments[3], function), 0, 1) : 1;

                        return new Colour4(red, green, blue, alpha);
                    }
                }

                throw new ThemeLoadException($"Unknown colour function '{function}'.");
            }

            throw new ThemeLoadException($"Invalid colour expression '{expression}'.");
        }

        private static Colour4 withHslAlpha(Vector4 hsl, float alpha) => Colour4.FromHSL(hsl.X, hsl.Y, hsl.Z).Opacity(alpha);

        private static string[] splitArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return Array.Empty<string>();

            var result = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < arguments.Length; i++)
            {
                switch (arguments[i])
                {
                    case '(':
                        depth++;
                        break;

                    case ')':
                        depth--;
                        break;

                    case ',' when depth == 0:
                        result.Add(arguments.Substring(start, i - start).Trim());
                        start = i + 1;
                        break;
                }
            }

            result.Add(arguments.Substring(start).Trim());
            return result.ToArray();
        }

        private static void requireArguments(string function, string[] arguments, int count)
        {
            if (arguments.Length != count)
                throw new ThemeLoadException($"{function}() expects {count} argument(s).");
        }

        private static float parseFloat(string value, string function)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                throw new ThemeLoadException($"'{value}' is not a valid number in {function}().");

            return result;
        }
    }
}
