// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace osu.Game.API.Events
{
    /// <summary>
    /// Base type for all events published on <see cref="OsuEventBus"/>.
    /// </summary>
    /// <remarks>
    /// Every event carries its own data payload (exposed via the concrete event's properties) as well as an
    /// optional <see cref="UI"/> drawable related to the event, when one exists.
    /// </remarks>
    public abstract record OsuApiEvent
    {
        /// <summary>
        /// The drawable most closely related to this event, or <see langword="null"/> if the event is global.
        /// </summary>
        public Drawable? UI { get; init; }
    }
}
