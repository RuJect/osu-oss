// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osuTK.Input;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Parses key specifications such as <c>"ctrl+shift+s"</c> or <c>["ctrl", "shift", "s"]</c>.
    /// </summary>
    public static class KeyParser
    {
        public static Key[] Parse(string specification)
            => Parse(specification.Split('+', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries));

        public static Key[] Parse(IEnumerable<string> names)
        {
            var keys = new List<Key>();

            foreach (string name in names)
                keys.Add(ParseName(name));

            if (keys.Count == 0)
                throw new System.ArgumentException("no key specified");

            return keys.ToArray();
        }

        public static Key ParseName(string name)
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

            if (System.Enum.TryParse(trimmed, true, out Key key))
                return key;

            throw new System.ArgumentException($"unknown key '{name}'");
        }
    }
}
