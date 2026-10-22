// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Jint;
using Jint.Native;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides notifications via <c>osu.notify</c>: <c>osu.notify.post</c> posts a simple notification and
    /// <c>osu.notify.progress</c> posts an ongoing progress notification with a handle to update it.
    /// </summary>
    public class NotificationApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "notify")]
        public void Post(string? message, string? level = null)
        {
            if (message == null)
                return;

            try
            {
                Context.Services.Notifications?.Post(createNotification(message, level));
            }
            catch (Exception e)
            {
                Context.Services.Output.Log(ScriptOutputLevel.Warn, "osu", $"notify failed: {e.Message}");
            }
        }

        /// <summary>
        /// Posts an ongoing operation and returns a handle with <c>setProgress</c>, <c>complete</c> and <c>cancel</c>.
        /// </summary>
        [ScriptApi(Target = "notify")]
        public JsValue Progress(string? message)
        {
            INotificationOverlay? overlay = Context.Services.Notifications;

            if (overlay == null || message == null)
                return JsValue.Undefined;

            var notification = new ProgressNotification { Text = message };
            overlay.Post(notification);

            JsObject handle = Context.CreateObject();

            Context.AddFunction(handle, "setProgress", (_, args) =>
            {
                if (args.Length >= 1 && args[0].IsNumber())
                    notification.Progress = (float)Math.Clamp(args[0].AsNumber(), 0, 1);

                return JsValue.Undefined;
            });

            Context.AddFunction(handle, "complete", (_, args) =>
            {
                notification.State = ProgressNotificationState.Completed;
                return JsValue.Undefined;
            });

            Context.AddFunction(handle, "cancel", (_, _) =>
            {
                notification.State = ProgressNotificationState.Cancelled;
                return JsValue.Undefined;
            });

            return handle;
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
