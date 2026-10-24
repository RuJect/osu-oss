// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Jint;
using Jint.Native;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Platform;
using osu.Game.Graphics.Sprites;
using osuTK;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides script-owned UI via <c>osu.ui</c>. <c>osu.ui.overlay</c> creates an overlay and returns a handle
    /// used to show/hide it and to add interactive labels and buttons.
    /// </summary>
    public class UiApiProvider : ScriptApiProvider
    {
        /// <summary>
        /// Creates an overlay titled <paramref name="title"/> and returns a handle with <c>show</c>, <c>hide</c>,
        /// <c>toggle</c>, <c>addLabel</c> and <c>addButton</c>. The overlay is removed when the script stops.
        /// </summary>
        [ScriptApi(Target = "ui")]
        public JsValue Overlay(string? title)
        {
            GameHost? host = Context.Services.Host;
            OsuGame game = Context.Services.Game;
            Engine engine = Context.Engine;
            ScriptInstance instance = Context.Instance;
            OutputLog output = Context.Services.Output;

            if (host == null)
                return JsValue.Undefined;

            var overlay = new ScriptOverlay(title ?? "Script");
            IDisposable? registration = null;

            void schedule(Action action) => host.UpdateThread.Scheduler.Add(action);

            schedule(() =>
            {
                if (game.IsLoaded)
                    registration = game.RegisterExternalOverlay(overlay);
            });

            instance.OnTearDown(() => schedule(() => registration?.Dispose()));

            JsObject handle = Context.CreateObject();

            Context.AddFunction(handle, "show", (_, _) =>
            {
                schedule(overlay.Show);
                return JsValue.Undefined;
            });

            Context.AddFunction(handle, "hide", (_, _) =>
            {
                schedule(overlay.Hide);
                return JsValue.Undefined;
            });

            Context.AddFunction(handle, "toggle", (_, _) =>
            {
                schedule(overlay.ToggleVisibility);
                return JsValue.Undefined;
            });

            Context.AddFunction(handle, "addLabel", (_, args) =>
            {
                var label = new OsuSpriteText
                {
                    Text = toText(args, 0),
                    Colour = Colour4.White,
                };

                schedule(() => overlay.AddContent(label));

                JsObject labelHandle = Context.CreateObject();

                Context.AddFunction(labelHandle, "setText", (_, labelArgs) =>
                {
                    string text = toText(labelArgs, 0);
                    schedule(() => label.Text = text);
                    return JsValue.Undefined;
                });

                return labelHandle;
            });

            Context.AddFunction(handle, "addButton", (_, args) =>
            {
                JsValue callback = args.Length >= 2 ? args[1] : JsValue.Undefined;

                var button = new BasicButton
                {
                    Text = toText(args, 0),
                    Size = new Vector2(180, 36),
                };

                if (callback.IsCallable())
                {
                    button.Action = () =>
                    {
                        try
                        {
                            engine.Invoke(callback);
                            engine.Advanced.ProcessTasks();
                        }
                        catch (Exception e)
                        {
                            output.Log(OutputLevel.Error, instance.Name, e.Message);
                        }
                    };
                }

                schedule(() => overlay.AddContent(button));

                return JsValue.Undefined;
            });

            return handle;
        }

        private static string toText(JsValue[] args, int index)
            => index < args.Length && !args[index].IsUndefined() && !args[index].IsNull() ? args[index].ToString() : string.Empty;
    }
}
