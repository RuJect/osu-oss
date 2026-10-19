// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.API.Events
{
    /// <summary>
    /// A development subscriber that listens to every event on the <see cref="OsuEventBus"/> and posts a
    /// notification naming the event which fired. Useful to verify the event pipeline end-to-end.
    /// </summary>
    public partial class NotificationEventSubscriber : Component
    {
        private readonly OsuEventBus bus;

        private IDisposable? subscription;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        public NotificationEventSubscriber(OsuEventBus bus)
        {
            this.bus = bus;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Marked critical so the notification is delivered immediately even while gameplay is active.
            // Non-critical notifications are queued by the notification overlay while playback is in progress
            // (overlay activation mode is disabled during gameplay) and would only surface later, which makes the
            // test subscriber unsuitable for verifying event timing.
            subscription = bus.SubscribeAll(e => notificationOverlay?.Post(new SimpleNotification
            {
                Text = $"Event: {e.GetType().Name}",
                IsCritical = true,
            }));
        }

        protected override void Dispose(bool isDisposing)
        {
            subscription?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
