// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Jint;
using Jint.Native;
using osu.Game.API.Services;

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
                Context.Services.Notifications?.Post(message, level);
            }
            catch (Exception e)
            {
                Context.Services.Output.Log(OutputLevel.Warn, "osu", $"notify failed: {e.Message}");
            }
        }

        /// <summary>
        /// Posts an ongoing operation and returns a handle with <c>setProgress</c>, <c>complete</c> and <c>cancel</c>.
        /// </summary>
        [ScriptApi(Target = "notify")]
        public JsValue Progress(string? message)
        {
            if (Context.Services.Notifications == null || message == null)
                return JsValue.Undefined;

            ProgressNotificationHandle notification = Context.Services.Notifications.Progress(message);

            JsObject handle = Context.CreateObject();

            Context.AddFunction(handle, "setProgress", (_, args) =>
            {
                if (args.Length >= 1 && args[0].IsNumber())
                    notification.SetProgress((float)args[0].AsNumber());

                return JsValue.Undefined;
            });

            Context.AddFunction(handle, "complete", (_, _) =>
            {
                notification.Complete();
                return JsValue.Undefined;
            });

            Context.AddFunction(handle, "cancel", (_, _) =>
            {
                notification.Cancel();
                return JsValue.Undefined;
            });

            return handle;
        }
    }
}
