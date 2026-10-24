// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;
using osuTK;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides control of the game cursor via <c>osu.cursor</c>. Positions are in window (screen) pixels.
    /// </summary>
    public class CursorApiProvider : ScriptApiProvider
    {
        private int moveTimer = -1;
        private Vector2 moveStart;
        private Vector2 moveTarget;
        private double moveStartTime;
        private double moveDuration;
        private TaskCompletionSource<bool>? moveCompletion;

        /// <summary>Returns the cursor position as <c>{ x, y }</c>, or <c>undefined</c> when input is unavailable.</summary>
        [ScriptApi(Target = "cursor")]
        public JsValue Position()
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
                return JsValue.Undefined;

            JsObject position = Context.CreateObject();
            position.Set("x", JsValue.FromObject(Context.Engine, (double)input.MousePosition.X));
            position.Set("y", JsValue.FromObject(Context.Engine, (double)input.MousePosition.Y));
            return position;
        }

        /// <summary>
        /// Moves the cursor to <paramref name="x"/>, <paramref name="y"/> immediately, or over
        /// <paramref name="duration"/> milliseconds when provided. Awaitable; completes when the movement
        /// ends or is superseded.
        /// </summary>
        [ScriptApi(Target = "cursor")]
        public Task MoveTo(double x, double y, JsValue duration)
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
                return Task.CompletedTask;

            var target = new Vector2((float)x, (float)y);

            if (!duration.IsNumber() || duration.AsNumber() <= 0)
            {
                input.MoveMouseTo(target);
                cancelMove();
                return Task.CompletedTask;
            }

            return startMove(input, target, duration.AsNumber());
        }

        /// <summary>
        /// Moves the cursor to <paramref name="x"/>, <paramref name="y"/> at <paramref name="speed"/> pixels per
        /// second. Awaitable; completes when the movement ends or is superseded.
        /// </summary>
        [ScriptApi(Target = "cursor")]
        public Task MoveAtSpeed(double x, double y, double speed)
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
                return Task.CompletedTask;

            var target = new Vector2((float)x, (float)y);
            double duration = speed > 0 ? Vector2.Distance(input.MousePosition, target) / speed * 1000 : 0;

            if (duration <= 0)
            {
                input.MoveMouseTo(target);
                cancelMove();
                return Task.CompletedTask;
            }

            return startMove(input, target, duration);
        }

        private Task startMove(ScriptInput input, Vector2 target, double duration)
        {
            var completion = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool>? previous = moveCompletion;
            moveCompletion = completion;

            moveStart = input.MousePosition;
            moveTarget = target;
            moveStartTime = Context.Instance.Scheduler.Elapsed;
            moveDuration = duration;

            if (moveTimer < 0)
            {
                JsValue tick = new ClrFunction(Context.Engine, "cursorMove", (_, _) =>
                {
                    tickMove();
                    return JsValue.Undefined;
                });

                moveTimer = Context.Instance.Scheduler.AddTimer(tick, 0, true);
            }

            previous?.TrySetResult(true);
            return completion.Task;
        }

        private void tickMove()
        {
            ScriptInput? input = ScriptInput.FromServices(Context.Services);

            if (input == null)
            {
                cancelMove();
                return;
            }

            double progress = Math.Clamp((Context.Instance.Scheduler.Elapsed - moveStartTime) / moveDuration, 0, 1);
            input.MoveMouseTo(Vector2.Lerp(moveStart, moveTarget, (float)progress));

            if (progress >= 1)
                cancelMove();
        }

        private void cancelMove()
        {
            if (moveTimer >= 0)
            {
                Context.Instance.Scheduler.CancelTimer(moveTimer);
                moveTimer = -1;
            }

            TaskCompletionSource<bool>? completion = moveCompletion;
            moveCompletion = null;
            completion?.TrySetResult(true);
        }
    }
}
