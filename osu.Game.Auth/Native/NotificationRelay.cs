// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Auth
{
    /// <summary>
    /// Forwards notifications to the game's notification overlay, waiting for it to become
    /// available if it is not resolved yet.
    /// </summary>
    internal static class NotificationRelay
    {
        private static INotificationOverlay? overlay;

        /// <summary>Binds the overlay once it is available from the game dependencies.</summary>
        public static void Bind(INotificationOverlay notificationOverlay) => overlay = notificationOverlay;

        public static async Task PostAsync(SimpleNotification notification)
        {
            while (overlay == null)
                await Task.Delay(1000).ConfigureAwait(false);

            overlay.Post(notification);
        }

        public static Task PostAsync(string text) =>
            PostAsync(new SimpleNotification { Text = text });
    }
}
