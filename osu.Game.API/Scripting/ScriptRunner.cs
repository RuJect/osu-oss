// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint.Runtime.Modules;
using osu.Game.API.Scripting.Api;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Owns the running <see cref="ScriptInstance"/>s: executes scripts, advances their frame/timer
    /// callbacks and dispatches event subscriptions. All mutation happens on the game's update thread.
    /// </summary>
    public class ScriptRunner
    {
        private readonly ScriptServices services;
        private readonly ScriptEngineFactory engineFactory;
        private readonly ScriptLocator locator;
        private readonly List<ScriptInstance> running = new List<ScriptInstance>();

        /// <summary>
        /// Whether scripts are allowed to start. Does not affect instances already running.
        /// </summary>
        public bool Enabled { get; set; } = true;

        public event Action? RunningScriptsChanged;

        public IReadOnlyList<ScriptInstance> Running => running;

        public ScriptRunner(ScriptServices services, ScriptEngineFactory engineFactory, ScriptLocator locator)
        {
            this.services = services;
            this.engineFactory = engineFactory;
            this.locator = locator;

            services.Events.OnHandlerError = (instance, e) => fail(instance, e);
        }

        public ScriptInstance Run(ScriptDefinition definition)
        {
            if (!Enabled)
            {
                services.Output.Log(OutputLevel.Warn, definition.Name, "not run: scripting is disabled");
                return new ScriptInstance(definition) { State = ScriptExecutionState.Stopped };
            }

            var instance = new ScriptInstance(definition);
            instance.Engine = engineFactory.Create(instance, this);

            try
            {
                // Scripts run as modules so they can use top-level await.
                string moduleName = string.IsNullOrEmpty(definition.Name) ? "script" : definition.Name;
                instance.Engine.Modules.Add(moduleName, definition.Source);
                instance.ModuleImport = instance.Engine.Modules.StartImport(moduleName);
                instance.Engine.Advanced.ProcessTasks();
            }
            catch (Exception e)
            {
                return failStartup(instance, e);
            }

            if (instance.ModuleImport.IsFaulted)
                return failStartup(instance, moduleException(instance.ModuleImport));

            if (instance.ModuleImport.IsCompleted)
            {
                instance.ModuleImport = null;

                if (isIdle(instance))
                {
                    complete(instance);
                    return instance;
                }
            }

            running.Add(instance);
            services.Output.Log(OutputLevel.Info, definition.Name, "started");
            RunningScriptsChanged?.Invoke();
            services.Events.PublishScriptStarted(instance.Name);
            return instance;
        }

        public ScriptInstance? RunByName(string name) => locator.FindByName(name) is { } definition ? Run(definition) : null;

        public ScriptInstance? RunFile(string fileName) => locator.LoadFromFile(fileName) is { } definition ? Run(definition) : null;

        public void Stop(ScriptInstance instance) => stop(instance, ScriptExecutionState.Stopped);

        public void StopAll()
        {
            foreach (ScriptInstance instance in running.ToArray())
                Stop(instance);
        }

        /// <summary>
        /// Advances frame callbacks and timers for all running scripts. Called on the update thread.
        /// </summary>
        public void Tick(double currentTime)
        {
            foreach (ScriptInstance instance in running.ToArray())
            {
                if (instance.State != ScriptExecutionState.Running)
                    continue;

                instance.Scheduler.Tick(currentTime, e => fail(instance, e));

                if (instance.State != ScriptExecutionState.Running)
                    continue;

                try
                {
                    instance.Engine.Advanced.ProcessTasks();
                }
                catch (Exception e)
                {
                    fail(instance, e);
                    continue;
                }

                ModuleImportOperation? import = instance.ModuleImport;

                if (import == null || (!import.IsCompleted && !import.IsFaulted))
                    continue;

                instance.ModuleImport = null;

                if (import.IsFaulted)
                {
                    fail(instance, moduleException(import));
                    continue;
                }

                if (isIdle(instance))
                    complete(instance);
            }
        }

        public void Emit(string eventName, object?[] args) => services.Events.Emit(eventName, args);

        private void stop(ScriptInstance instance, ScriptExecutionState state)
        {
            if (!running.Remove(instance))
                return;

            instance.State = state;
            teardown(instance);
            services.Output.Log(OutputLevel.Info, instance.Name, state == ScriptExecutionState.Error ? "stopped after error" : "stopped");
            RunningScriptsChanged?.Invoke();
            services.Events.PublishScriptStopped(instance.Name);
        }

        private void fail(ScriptInstance instance, Exception e)
        {
            services.Output.Log(OutputLevel.Error, instance.Name, e.Message);
            stop(instance, ScriptExecutionState.Error);
            services.Events.PublishScriptError(instance.Name, e.Message);
        }

        private ScriptInstance failStartup(ScriptInstance instance, Exception e)
        {
            instance.State = ScriptExecutionState.Error;
            services.Output.Log(OutputLevel.Error, instance.Name, e.Message);
            teardown(instance);
            running.Add(instance);
            RunningScriptsChanged?.Invoke();
            services.Events.PublishScriptError(instance.Name, e.Message);
            return instance;
        }

        private void complete(ScriptInstance instance)
        {
            instance.State = ScriptExecutionState.Completed;
            services.Output.Log(OutputLevel.Info, instance.Name, "completed");
            teardown(instance);
            running.Remove(instance);
            RunningScriptsChanged?.Invoke();
        }

        private bool isIdle(ScriptInstance instance)
            => !instance.Scheduler.HasPendingCallbacks && !services.Events.HasSubscriptions(instance);

        private static Exception moduleException(ModuleImportOperation import)
        {
            try
            {
                import.GetResult();
            }
            catch (Exception e)
            {
                return e;
            }

            return new InvalidOperationException("Module import failed.");
        }

        /// <summary>
        /// Releases the instance's engine and removes any lingering event subscriptions in one place.
        /// </summary>
        private void teardown(ScriptInstance instance)
        {
            instance.TearDown();
            services.Events.RemoveFor(instance);
        }
    }
}
