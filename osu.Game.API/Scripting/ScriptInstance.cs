// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using Jint;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// A live, running script. Owns its own Jint <see cref="Engine"/> and callback scheduler.
    /// </summary>
    public class ScriptInstance
    {
        public ScriptDefinition Definition { get; }

        public ScriptExecutionState State { get; internal set; } = ScriptExecutionState.Running;

        public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;

        public string Name => Definition.Name;

        internal Engine Engine = null!;
        internal CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
        internal ScriptScheduler Scheduler { get; } = new ScriptScheduler();

        private bool tornDown;

        internal ScriptInstance(ScriptDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>
        /// Cancels any pending execution and disposes the underlying JavaScript engine.
        /// Safe to call more than once.
        /// </summary>
        internal void TearDown()
        {
            if (tornDown)
                return;

            tornDown = true;

            try
            {
                Cancellation.Cancel();
            }
            catch
            {
                // ignored
            }

            try
            {
                Engine?.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                Cancellation.Dispose();
            }
            catch
            {
                // ignored
            }

            Scheduler.Clear();
        }
    }
}
