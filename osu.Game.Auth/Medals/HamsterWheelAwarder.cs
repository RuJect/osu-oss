// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Graphics.Cursor;

namespace osu.Game.Auth.Medals
{
    /// <summary>Hamster wheel (355): accumulate 36000 degrees of menu cursor rotation, release at ~0.</summary>
    public class HamsterWheelAwarder : IMedalAwarder
    {
        private MenuCursorContainer? cursorContainer;
        private float maxRotation;

        public int MedalId => 355;

        public bool Enabled { get; set; }

        public bool CheckMedalCriteria(OsuGame game)
        {
            cursorContainer ??= game.ChildrenOfType<MenuCursorContainer>().SingleOrDefault();

            if (cursorContainer == null)
                return false;

            if (cursorContainer.State.Value == Visibility.Hidden)
                return false;

            maxRotation = Math.Max(maxRotation, Math.Abs(cursorContainer.ActiveCursor.Rotation));

            if (maxRotation >= 36000f)
                return Precision.AlmostEquals(cursorContainer.ActiveCursor.Rotation, 0f);

            return false;
        }
    }
}
