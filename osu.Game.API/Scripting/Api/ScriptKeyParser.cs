// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using osuTK.Input;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Parses script key specifications such as <c>"ctrl+shift+s"</c> or <c>["ctrl", "shift", "s"]</c>.
    /// </summary>
    internal static class ScriptKeyParser
    {
        public static Key[] Parse(JsValue value)
        {
            if (value.IsString())
                return parseNames(value.AsString().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (value.IsArray())
            {
                var names = new List<string>();
                var array = value.AsArray();

                for (uint i = 0; i < array.Length; i++)
                {
                    JsValue item = value.Get(i.ToString());

                    if (!item.IsString())
                        throw new ArgumentException("key array must contain strings");

                    names.Add(item.AsString());
                }

                return parseNames(names);
            }

            throw new ArgumentException("key must be a string or an array of strings");
        }

        private static Key[] parseNames(IEnumerable<string> names)
        {
            var keys = new List<Key>();

            foreach (string name in names)
                keys.Add(parseName(name));

            if (keys.Count == 0)
                throw new ArgumentException("no key specified");

            return keys.ToArray();
        }

        private static Key parseName(string name)
        {
            string trimmed = name.Trim();

            switch (trimmed.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    return Key.LControl;

                case "shift":
                    return Key.LShift;

                case "alt":
                    return Key.LAlt;

                case "super":
                case "win":
                case "meta":
                case "cmd":
                case "command":
                    return Key.LWin;

                case "esc":
                    return Key.Escape;

                case "return":
                    return Key.Enter;

                case "del":
                    return Key.Delete;
            }

            if (trimmed.Length == 1 && char.IsDigit(trimmed[0]))
                return (Key)((int)Key.Number0 + (trimmed[0] - '0'));

            if (Enum.TryParse(trimmed, true, out Key key))
                return key;

            throw new ArgumentException($"unknown key '{name}'");
        }
    }
}
