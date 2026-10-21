// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Theming
{
    public class ThemeLoadException : Exception
    {
        public ThemeLoadException(string message)
            : base(message)
        {
        }

        public ThemeLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
