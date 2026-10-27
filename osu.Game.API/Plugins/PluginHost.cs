// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.API.Events;
using osu.Game.Events;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Owns loaded plugin entries: creates their per-plugin context, drives their schedulers on the update
    /// thread and reports lifecycle changes. All mutation happens on the update thread.
    /// </summary>
    public class PluginHost
    {
        private readonly OsuGame game;
        private readonly ApiServices services;
        private readonly OsuEventBus events;
        private readonly List<LoadedPlugin> loaded = new List<LoadedPlugin>();

        public event Action? LoadedPluginsChanged;

        public IReadOnlyList<LoadedPlugin> Loaded => loaded;

        public PluginHost(OsuGame game, ApiServices services, OsuEventBus events)
        {
            this.game = game;
            this.services = services;
            this.events = events;
        }

        /// <summary>Loads a discovered plugin and returns its entry. Load failures are logged.</summary>
        public LoadedPlugin Load(PluginLoadResult result)
        {
            var manifest = result.Manifest;

            var pluginStorage = pluginDataStorage(manifest);
            var logger = new PluginLogger(manifest.Id, pluginStorage);
            var pluginEvents = new PluginEventService(events, manifest);
            var context = new PluginContext(services, pluginEvents, new SchedulerService(), pluginStorage, logger, manifest);
            var entry = new LoadedPlugin(result.CSharpPlugin, context, manifest);

            loaded.Add(entry);

            try
            {
                pluginEvents.LoadPlugin(result.CSharpPlugin, context);

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
        /// Registers the built-in "system" plugin written into the scripting subsystem. The caller builds
        /// its context: no assembly is loaded, the entry only holds state and drives the scheduler.
        /// </summary>
        public LoadedPlugin LoadSystemPlugin(PluginManifest manifest, PluginContext context)
        {
            var entry = new LoadedPlugin(null, context, manifest);

            context.Events.Publish(new PluginLoadingEvent(manifest));

            loaded.Add(entry);

            entry.State = PluginState.Running;
            services.Output.Log(OutputLevel.Info, entry.Name, "loaded (system)");
            context.Events.Publish(new PluginLoadedEvent(manifest));
            LoadedPluginsChanged?.Invoke();
            return entry;
        }

        /// <summary>Unloads <paramref name="entry"/>, invoking its <see cref="PluginBase.Unload"/>.</summary>
        public void Unload(LoadedPlugin entry)
        {
            if (!loaded.Remove(entry))
                return;

            try
            {
                if (entry.Plugin != null)
                {
                    entry.Context.Events.UnloadPlugin(entry.Plugin);
                }
                else
                {
                    entry.Context.Events.Publish(new PluginUnloadingEvent(entry.Manifest));
                    entry.Context.Events.Dispose();
                    entry.Context.Events.Publish(new PluginUnloadedEvent(entry.Manifest));
                }
            }
            catch (Exception e)
            {
                entry.Logger.Error(e.Message);
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
            entry.Context.Events.Dispose();
            entry.Logger.Error(e.Message);
            services.Output.Log(OutputLevel.Error, entry.Name, e.Message);
        }

        private StorageService? pluginDataStorage(PluginManifest manifest)
        {
            if (!manifest.Permissions.HasPermission("data"))
                return null;

            try
            {
                var storage = game.Dependencies.Get<Storage>();
                return new StorageService(new ScopedStorage(SpecialStorage.GetPluginData(storage, manifest.Id)));
            }
            catch (Exception e)
            {
                services.Output.Log(OutputLevel.Error, manifest.Id, $"failed to resolve plugin data storage: {e.Message}");
                return null;
            }
        }
    }
}
