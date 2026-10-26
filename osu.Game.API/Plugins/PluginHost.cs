// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Events;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Owns loaded <see cref="IPlugin"/> instances: creates their per-plugin context, drives their
    /// schedulers on the update thread and reports lifecycle changes. All mutation happens on the
    /// update thread.
    /// </summary>
    public class PluginHost
    {
        private readonly ApiServices services;
        private readonly OsuEventBus events;
        private readonly List<LoadedPlugin> loaded = new List<LoadedPlugin>();

        public event Action? LoadedPluginsChanged;

        public IReadOnlyList<LoadedPlugin> Loaded => loaded;

        public PluginHost(ApiServices services, OsuEventBus events)
        {
            this.services = services;
            this.events = events;
        }

        /// <summary>Loads <paramref name="plugin"/> and returns its entry. Load failures are logged.</summary>
        public LoadedPlugin Load(IPlugin plugin)
        {
            var context = new PluginContext(services, events, new SchedulerService());
            var entry = new LoadedPlugin(plugin, context);

            loaded.Add(entry);

            try
            {
                plugin.Load(context);
                entry.State = PluginState.Running;
                services.Output.Log(OutputLevel.Info, entry.Name, "loaded");
            }
            catch (Exception e)
            {
                fail(entry, e);
            }

            LoadedPluginsChanged?.Invoke();
            return entry;
        }

        /// <summary>Unloads <paramref name="entry"/>, invoking its <see cref="IPlugin.Unload"/>.</summary>
        public void Unload(LoadedPlugin entry)
        {
            if (!loaded.Remove(entry))
                return;

            try
            {
                entry.Plugin.Unload();
            }
            catch (Exception e)
            {
                services.Output.Log(OutputLevel.Error, entry.Name, e.Message);
            }

            entry.Context.Scheduler.Clear();
            entry.State = PluginState.Stopped;
            services.Output.Log(OutputLevel.Info, entry.Name, "unloaded");
            LoadedPluginsChanged?.Invoke();
        }

        public void UnloadAll()
        {
            foreach (LoadedPlugin entry in loaded.ToArray())
                Unload(entry);
        }

        /// <summary>Advances every running plugin's frame callbacks and timers. Called on the update thread.</summary>
        public void Tick(double currentTime)
        {
            foreach (LoadedPlugin entry in loaded.ToArray())
            {
                if (entry.State != PluginState.Running)
                    continue;

                entry.Context.Scheduler.Tick(currentTime, e => fail(entry, e));
            }
        }

        private void fail(LoadedPlugin entry, Exception e)
        {
            entry.State = PluginState.Error;
            services.Output.Log(OutputLevel.Error, entry.Name, e.Message);
        }
    }
}
