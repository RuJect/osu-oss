// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Posts simple and progress notifications to the notification overlay.
    /// </summary>
    public class NotificationService
    {
        private readonly INotificationOverlay overlay;

        public NotificationService(INotificationOverlay overlay)
        {
            this.overlay = overlay;
        }

        public void Post(string message, string? level = null)
            => overlay.Post(createNotification(message, level));

        /// <summary>Posts an ongoing progress notification and returns a handle to update it.</summary>
        public ProgressNotificationHandle Progress(string message)
        {
            var notification = new ProgressNotification { Text = message };
            overlay.Post(notification);
            return new ProgressNotificationHandle(notification);
        }

        private static SimpleNotification createNotification(string message, string? level)
        {
            var notification = new SimpleNotification { Text = message };

            switch (level)
            {
                case "error":
                    notification.Icon = FontAwesome.Solid.ExclamationTriangle;
                    notification.IconColour = Colour4.Red;
                    break;

                case "warn":
                    notification.Icon = FontAwesome.Solid.ExclamationTriangle;
                    notification.IconColour = Colour4.Yellow;
                    break;
            }

            return notification;
        }
    }

    /// <summary>A handle to a posted <see cref="ProgressNotification"/>.</summary>
    public sealed class ProgressNotificationHandle
    {
        private readonly ProgressNotification notification;

        internal ProgressNotificationHandle(ProgressNotification notification)
        {
            this.notification = notification;
        }

        public void SetProgress(float value) => notification.Progress = Math.Clamp(value, 0, 1);

        public void Complete() => notification.State = ProgressNotificationState.Completed;

        public void Cancel() => notification.State = ProgressNotificationState.Cancelled;
    }
}
