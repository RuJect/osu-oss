// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using Jint;
using Jint.Runtime.Modules;
using osu.Game.API.Services;

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
        internal SchedulerService Scheduler { get; } = new SchedulerService();
        internal ModuleImportOperation? ModuleImport;

        private readonly List<Action> teardownActions = new List<Action>();

        private bool tornDown;

        internal ScriptInstance(ScriptDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>
        /// Registers an action invoked when the script is torn down (stopped, completed or failed).
        /// Used by API providers to release resources that live outside the script engine, such as overlays.
        /// </summary>
        internal void OnTearDown(Action action) => teardownActions.Add(action);

        /// <summary>
        /// Cancels any pending execution and disposes the underlying JavaScript engine.
        /// Safe to call more than once.
        /// </summary>
        internal void TearDown()
        {
            if (tornDown)
                return;

            tornDown = true;

            foreach (Action action in teardownActions)
            {
                try
                {
                    action();
                }
                catch
                {
                    // ignored
                }
            }

            teardownActions.Clear();

            ModuleImport = null;

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
