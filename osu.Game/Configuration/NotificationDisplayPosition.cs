// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Configuration
{
    /// <summary>
    /// The screen corner in which temporary notifications are displayed.
    /// </summary>
    public enum NotificationDisplayPosition
    {
        /// <summary>Notifications are displayed in the top-left corner.</summary>
        TopLeft,

        /// <summary>Notifications are displayed in the top-right corner (the default behaviour).</summary>
        TopRight,

        /// <summary>Notifications are displayed in the bottom-left corner.</summary>
        BottomLeft,

        /// <summary>Notifications are displayed in the bottom-right corner.</summary>
        BottomRight,
    }
}
