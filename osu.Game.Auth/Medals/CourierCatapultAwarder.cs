// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Auth.Medals
{
    /// <summary>Courier catapult (356): fling a toast notification off-screen fast enough.</summary>
    public class CourierCatapultAwarder : IMedalAwarder
    {
        private NotificationOverlayToastTray? tray;

        private Dictionary<Notification, float> previous = new Dictionary<Notification, float>();
        private Dictionary<Notification, float> current = new Dictionary<Notification, float>();

        public int MedalId => 356;

        public bool Enabled { get; set; }

        public bool CheckMedalCriteria(OsuGame game)
        {
            tray ??= game.ChildrenOfType<NotificationOverlayToastTray>().SingleOrDefault();

            if (tray == null)
                return false;

            if (!tray.IsDisplayingToasts)
            {
                previous.Clear();
                return false;
            }

            foreach (Notification notification in tray.Notifications)
            {
                float? x = notification.MainContent.Parent?.X;

                if (x.HasValue)
                    current[notification] = x.Value;
            }

            foreach (var (notification, x) in current)
            {
                if (previous.TryGetValue(notification, out float oldX) && (x - oldX) / game.Clock.ElapsedFrameTime < -3.5)
                    return true;
            }

            (previous, current) = (current, previous);
            current.Clear();
            return false;
        }
    }
}
