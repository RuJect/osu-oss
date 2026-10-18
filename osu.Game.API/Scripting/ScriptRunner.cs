// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.API.Scripting.Api;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Owns the running <see cref="ScriptInstance"/>s: executes scripts, advances their frame/timer
    /// callbacks and dispatches event subscriptions. All mutation happens on the game's update thread.
    /// </summary>
    public class ScriptRunner
    {
        private readonly ScriptApiEnvironment environment;
        private readonly ScriptEngineFactory engineFactory;
        private readonly List<ScriptInstance> running = new List<ScriptInstance>();

        public event Action? RunningScriptsChanged;

        public IReadOnlyList<ScriptInstance> Running => running;

        public ScriptRunner(ScriptApiEnvironment environment, ScriptEngineFactory engineFactory)
        {
            this.environment = environment;
            this.engineFactory = engineFactory;
        }

        public ScriptInstance Run(ScriptDefinition definition)
        {
            var instance = new ScriptInstance(definition);
            instance.Engine = engineFactory.Create(instance, this);

            try
            {
                instance.Engine.Execute(definition.Source, definition.Name);
            }
            catch (Exception e)
            {
                instance.State = ScriptExecutionState.Error;
                environment.Output.Log(ScriptOutputLevel.Error, definition.Name, e.Message);
                instance.TearDown();
                running.Add(instance);
                RunningScriptsChanged?.Invoke();
                return instance;
            }

            if (!instance.Scheduler.HasPendingCallbacks && !environment.Events.HasSubscriptions(instance))
            {
                instance.State = ScriptExecutionState.Completed;
                environment.Output.Log(ScriptOutputLevel.Info, definition.Name, "completed");
                instance.TearDown();
                RunningScriptsChanged?.Invoke();
                return instance;
            }

            running.Add(instance);
            environment.Output.Log(ScriptOutputLevel.Info, definition.Name, "started");
            RunningScriptsChanged?.Invoke();
            return instance;
        }

        public ScriptInstance? RunByName(string name)
        {
            ScriptDefinition? definition = environment.Repository.Definitions.FirstOrDefault(d => d.Name == name);

            return definition == null ? null : Run(definition);
        }

        public ScriptInstance? RunFile(string fileName)
        {
            ScriptStorage? storage = environment.Repository.Storage;

            if (storage == null || !storage.Exists(fileName))
                return null;

            var definition = new ScriptDefinition(System.IO.Path.GetFileNameWithoutExtension(fileName), fileName, storage.Read(fileName));
            return Run(definition);
        }

        public void Stop(ScriptInstance instance) => stop(instance, ScriptExecutionState.Stopped);

        public void StopAll()
        {
            foreach (ScriptInstance instance in running.ToArray())
                Stop(instance);
        }

        private void stop(ScriptInstance instance, ScriptExecutionState state)
        {
            if (!running.Remove(instance))
                return;

            instance.State = state;
            instance.TearDown();
            environment.Events.RemoveFor(instance);
            environment.Output.Log(ScriptOutputLevel.Info, instance.Name, state == ScriptExecutionState.Error ? "stopped after error" : "stopped");
            RunningScriptsChanged?.Invoke();
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

                instance.Scheduler.Tick(instance.Engine, currentTime, e => fail(instance, e));
            }
        }

        public void Emit(string eventName, object?[] args)
        {
            foreach (ScriptEventBus.EventSubscription subscription in environment.Events.HandlersFor(eventName))
            {
                if (!running.Contains(subscription.Instance) || subscription.Instance.State != ScriptExecutionState.Running)
                    continue;

                try
                {
                    subscription.Instance.Engine.Invoke(subscription.Handler, args);
                }
                catch (Exception e)
                {
                    fail(subscription.Instance, e);
                }
            }
        }

        private void fail(ScriptInstance instance, Exception e)
        {
            environment.Output.Log(ScriptOutputLevel.Error, instance.Name, e.Message);
            stop(instance, ScriptExecutionState.Error);
        }
    }
}
