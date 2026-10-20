// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Configuration
{
    /// <summary>
    /// The screen edge a <see cref="Overlays.Toolbar.Toolbar"/> is docked to.
    /// </summary>
    public enum ToolbarPosition
    {
        /// <summary>The toolbar is docked to the top edge (the default behaviour).</summary>
        Top,

        /// <summary>The toolbar is docked to the bottom edge.</summary>
        Bottom,

        /// <summary>The toolbar is docked to the left edge, rendered as a vertical bar.</summary>
        Left,

        /// <summary>The toolbar is docked to the right edge, rendered as a vertical bar.</summary>
        Right,
    }
}
