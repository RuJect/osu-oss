// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Jint;
using Jint.Native;
using osu.Framework.Allocation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides <c>osu.notify</c>, which posts a simple notification to the user.
    /// </summary>
    public class NotificationApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            context.AddFunction(context.Api, "notify", (_, args) =>
            {
                if (args.Length >= 1)
                    notify(context, args[0].ToString());

                return JsValue.Undefined;
            });
        }

        private static void notify(ScriptApiContext context, string message)
        {
            try
            {
                INotificationOverlay? overlay = context.Environment.Game.Dependencies.Get<INotificationOverlay>();

                if (overlay != null)
                    overlay.Post(new SimpleNotification { Text = message });
            }
            catch (Exception e)
            {
                context.Environment.Output.Log(ScriptOutputLevel.Warn, "osu", $"notify failed: {e.Message}");
            }
        }
    }
}
