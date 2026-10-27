// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using Jint;
using Jint.Native;
using osu.Game.API.Services;
using osuTK;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides control of the game cursor via <c>osu.cursor</c>. Positions are in window (screen) pixels.
    /// </summary>
    public class CursorApiProvider : ScriptApiProvider
    {
        private CursorService? cursor;

        private CursorService cursorService => cursor ??= new CursorService(Context.Plugin.Services, Context.Instance.Scheduler);

        /// <summary>Returns the cursor position as <c>{ x, y }</c>, or <c>undefined</c> when input is unavailable.</summary>
        [ScriptApi(Target = "cursor")]
        public JsValue Position()
        {
            Vector2? position = cursorService.Position();

            if (position == null)
                return JsValue.Undefined;

            JsObject result = Context.CreateObject();
            result.Set("x", JsValue.FromObject(Context.Engine, (double)position.Value.X));
            result.Set("y", JsValue.FromObject(Context.Engine, (double)position.Value.Y));
            return result;
        }

        /// <summary>
        /// Moves the cursor to <paramref name="x"/>, <paramref name="y"/> immediately, or over
        /// <paramref name="duration"/> milliseconds when provided. Awaitable; completes when the movement
        /// ends or is superseded.
        /// </summary>
        [ScriptApi(Target = "cursor")]
        public Task MoveTo(double x, double y, JsValue duration)
            => cursorService.MoveTo(x, y, duration.IsNumber() ? duration.AsNumber() : null);

        /// <summary>
        /// Moves the cursor to <paramref name="x"/>, <paramref name="y"/> at <paramref name="speed"/> pixels per
        /// second. Awaitable; completes when the movement ends or is superseded.
        /// </summary>
        [ScriptApi(Target = "cursor")]
        public Task MoveAtSpeed(double x, double y, double speed) => cursorService.MoveAtSpeed(x, y, speed);
    }
}
