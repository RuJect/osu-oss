// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.API.Scripting.Api;

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

        public event Action? RunningScriptsChanged;

        public IReadOnlyList<ScriptInstance> Running => running;

        public ScriptRunner(ScriptServices services, ScriptEngineFactory engineFactory, ScriptLocator locator)
        {
            this.services = services;
            this.engineFactory = engineFactory;
            this.locator = locator;
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
                services.Output.Log(ScriptOutputLevel.Error, definition.Name, e.Message);
                teardown(instance);
                running.Add(instance);
                RunningScriptsChanged?.Invoke();
                Emit("scriptError", new object?[] { instance.Name, e.Message });
                return instance;
            }

            if (!instance.Scheduler.HasPendingCallbacks && !services.Events.HasSubscriptions(instance))
            {
                instance.State = ScriptExecutionState.Completed;
                services.Output.Log(ScriptOutputLevel.Info, definition.Name, "completed");
                teardown(instance);
                RunningScriptsChanged?.Invoke();
                return instance;
            }

            running.Add(instance);
            services.Output.Log(ScriptOutputLevel.Info, definition.Name, "started");
            RunningScriptsChanged?.Invoke();
            Emit("scriptStarted", new object?[] { instance.Name });
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

                instance.Scheduler.Tick(instance.Engine, currentTime, e => fail(instance, e));
            }
        }

        public void Emit(string eventName, object?[] args)
        {
            foreach (ScriptEventBus.EventSubscription subscription in services.Events.HandlersFor(eventName))
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
                    continue;
                }

                if (subscription.Once)
                    services.Events.Unsubscribe(subscription.Instance, eventName, subscription.Handler);
            }
        }

        private void stop(ScriptInstance instance, ScriptExecutionState state)
        {
            if (!running.Remove(instance))
                return;

            instance.State = state;
            teardown(instance);
            services.Output.Log(ScriptOutputLevel.Info, instance.Name, state == ScriptExecutionState.Error ? "stopped after error" : "stopped");
            RunningScriptsChanged?.Invoke();
            Emit("scriptStopped", new object?[] { instance.Name });
        }

        private void fail(ScriptInstance instance, Exception e)
        {
            services.Output.Log(ScriptOutputLevel.Error, instance.Name, e.Message);
            stop(instance, ScriptExecutionState.Error);
            Emit("scriptError", new object?[] { instance.Name, e.Message });
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
