// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osuTK;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Moves the game cursor, immediately or over a duration. Positions are in window (screen) pixels.
    /// </summary>
    public class CursorService
    {
        private readonly ApiServices services;
        private readonly SchedulerService scheduler;

        private int moveTimer = -1;
        private Vector2 moveStart;
        private Vector2 moveTarget;
        private double moveStartTime;
        private double moveDuration;
        private TaskCompletionSource<bool>? moveCompletion;

        public CursorService(ApiServices services, SchedulerService scheduler)
        {
            this.services = services;
            this.scheduler = scheduler;
        }

        /// <summary>The current cursor position, or <c>null</c> when input is unavailable.</summary>
        public Vector2? Position() => currentInput()?.MousePosition;

        /// <summary>
        /// Moves the cursor to <paramref name="x"/>, <paramref name="y"/> immediately, or over
        /// <paramref name="duration"/> milliseconds when provided. Awaitable; completes when the movement
        /// ends or is superseded.
        /// </summary>
        public Task MoveTo(double x, double y, double? duration)
        {
            InputService? input = currentInput();

            if (input == null)
                return Task.CompletedTask;

            var target = new Vector2((float)x, (float)y);

            if (duration == null || duration <= 0)
            {
                input.MoveMouseTo(target);
                cancelMove();
                return Task.CompletedTask;
            }

            return startMove(input, target, duration.Value);
        }

        /// <summary>
        /// Moves the cursor to <paramref name="x"/>, <paramref name="y"/> at <paramref name="speed"/> pixels per
        /// second. Awaitable; completes when the movement ends or is superseded.
        /// </summary>
        public Task MoveAtSpeed(double x, double y, double speed)
        {
            InputService? input = currentInput();

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

        private Task startMove(InputService input, Vector2 target, double duration)
        {
            var completion = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool>? previous = moveCompletion;
            moveCompletion = completion;

            moveStart = input.MousePosition;
            moveTarget = target;
            moveStartTime = scheduler.Elapsed;
            moveDuration = duration;

            if (moveTimer < 0)
                moveTimer = scheduler.AddTimer(tickMove, 0, true);

            previous?.TrySetResult(true);
            return completion.Task;
        }

        private void tickMove()
        {
            InputService? input = currentInput();

            if (input == null)
            {
                cancelMove();
                return;
            }

            double progress = Math.Clamp((scheduler.Elapsed - moveStartTime) / moveDuration, 0, 1);
            input.MoveMouseTo(Vector2.Lerp(moveStart, moveTarget, (float)progress));

            if (progress >= 1)
                cancelMove();
        }

        private void cancelMove()
        {
            if (moveTimer >= 0)
            {
                scheduler.CancelTimer(moveTimer);
                moveTimer = -1;
            }

            TaskCompletionSource<bool>? completion = moveCompletion;
            moveCompletion = null;
            completion?.TrySetResult(true);
        }

        private InputService? currentInput() => InputService.FromServices(services);
    }
}
