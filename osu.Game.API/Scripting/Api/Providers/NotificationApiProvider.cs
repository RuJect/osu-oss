// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides <c>osu.notify</c>, which posts a simple notification to the user.
    /// </summary>
    public class NotificationApiProvider : ScriptApiProvider
    {
        [ScriptApi]
        public void Notify(string? message)
        {
            if (message == null)
                return;

            try
            {
                INotificationOverlay? overlay = Context.Services.Game.Dependencies.Get<INotificationOverlay>();

                if (overlay != null)
                    overlay.Post(new SimpleNotification { Text = message });
            }
            catch (Exception e)
            {
                Context.Services.Output.Log(ScriptOutputLevel.Warn, "osu", $"notify failed: {e.Message}");
            }
        }
    }
}
