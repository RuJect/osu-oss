// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides <c>osu.notify</c>, which posts a notification to the user. An optional second argument
    /// (<c>"warn"</c> or <c>"error"</c>) selects a warning/error presentation.
    /// </summary>
    public class NotificationApiProvider : ScriptApiProvider
    {
        [ScriptApi]
        public void Notify(string? message, string? level = null)
        {
            if (message == null)
                return;

            try
            {
                INotificationOverlay? overlay = Context.Services.Game.Dependencies.Get<INotificationOverlay>();

                if (overlay != null)
                    overlay.Post(createNotification(message, level));
            }
            catch (Exception e)
            {
                Context.Services.Output.Log(ScriptOutputLevel.Warn, "osu", $"notify failed: {e.Message}");
            }
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
}
