// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.API.Events;
using osu.Game.API.Services;
using osu.Game.Events;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Owns loaded plugin entries: creates their per-plugin context, drives their schedulers on the update
    /// thread and reports lifecycle changes. All mutation happens on the update thread.
    /// </summary>
    public class PluginHost
    {
        private readonly OsuEventBus events;
        private readonly Func<PluginManifest, ApiServices> servicesFactory;
        private readonly List<LoadedPlugin> loaded = new List<LoadedPlugin>();

        public event Action? LoadedPluginsChanged;

        public IReadOnlyList<LoadedPlugin> Loaded => loaded;

        public PluginHost(OsuEventBus events, Func<PluginManifest, ApiServices> servicesFactory)
        {
            this.events = events;
            this.servicesFactory = servicesFactory;
        }

        /// <summary>Loads a discovered plugin and returns its entry. Load failures are logged.</summary>
        public LoadedPlugin Load(PluginLoadResult result)
        {
            var manifest = result.Manifest;

            ApiServices services = servicesFactory(manifest);
            var context = new PluginContext(services, new PluginEventService(events, manifest), new SchedulerService(),
                new PluginLogger(manifest.Id), manifest);
            var entry = new LoadedPlugin(result.CSharpPlugin, context, manifest);

            loaded.Add(entry);

            try
            {
                events.Publish(new PluginLoadingEvent(manifest));

                result.CSharpPlugin.Load(context);

                events.Publish(new PluginLoadedEvent(manifest));

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

        /// <summary>
        /// Registers the built-in "system" plugin written into the scripting subsystem. No assembly is loaded;
        /// the entry only holds state and drives the scheduler.
        /// </summary>
        public LoadedPlugin LoadSystemPlugin(PluginManifest manifest)
        {
            ApiServices services = servicesFactory(manifest);
            var context = new PluginContext(services, new PluginEventService(events, manifest), new SchedulerService(),
                new PluginLogger(manifest.Id), manifest);
            var entry = new LoadedPlugin(null, context, manifest);

            events.Publish(new PluginLoadingEvent(manifest));

            loaded.Add(entry);

            events.Publish(new PluginLoadedEvent(manifest));

            entry.State = PluginState.Running;
            services.Output.Log(OutputLevel.Info, entry.Name, "loaded (system)");
            LoadedPluginsChanged?.Invoke();
            return entry;
        }

        /// <summary>Unloads <paramref name="entry"/>, invoking its <see cref="PluginBase.Unload"/>.</summary>
        public void Unload(LoadedPlugin entry)
        {
            if (!loaded.Remove(entry))
                return;

            events.Publish(new PluginUnloadingEvent(entry.Manifest));

            try
            {
                entry.Plugin?.Unload();
            }
            catch (Exception e)
            {
                entry.Logger.Error(e.Message);
                entry.Context.Services.Output.Log(OutputLevel.Error, entry.Name, e.Message);
            }

            entry.Context.Dispose();
            entry.State = PluginState.Stopped;
            events.Publish(new PluginUnloadedEvent(entry.Manifest));
            entry.Context.Services.Output.Log(OutputLevel.Info, entry.Name, "unloaded");
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
            entry.Context.Events.Dispose();
            events.Publish(new PluginLoadFailedEvent(entry.Manifest, e.Message));
            entry.Logger.Error(e.Message);
            entry.Context.Services.Output.Log(OutputLevel.Error, entry.Name, e.Message);
        }
    }
}
