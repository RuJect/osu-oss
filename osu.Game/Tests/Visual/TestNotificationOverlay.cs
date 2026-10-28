// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Tests.Visual
{
    public class TestNotificationOverlay : INotificationOverlay
    {
        public virtual void Post(Notification notification)
        {
        }

        public virtual void Hide()
        {
        }

        public IBindable<int> UnreadCount { get; } = new Bindable<int>();

        public IEnumerable<Notification> AllNotifications => Enumerable.Empty<Notification>();
    }
}
