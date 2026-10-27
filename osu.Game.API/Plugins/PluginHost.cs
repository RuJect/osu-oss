// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.API.Events;
using osu.Game.API.Scripting;
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
        private readonly Func<PluginManifest, PluginLogger> loggerFactory;
        private readonly Func<PluginManifest, string?, PluginContext, PluginScriptHost?>? scriptFactory;
        private readonly List<LoadedPlugin> loaded = new List<LoadedPlugin>();

        public event Action? LoadedPluginsChanged;

        public IReadOnlyList<LoadedPlugin> Loaded => loaded;

        public PluginHost(OsuEventBus events, Func<PluginManifest, ApiServices> servicesFactory,
                          Func<PluginManifest, PluginLogger>? loggerFactory = null,
                          Func<PluginManifest, string?, PluginContext, PluginScriptHost?>? scriptFactory = null)
        {
            this.events = events;
            this.servicesFactory = servicesFactory;
            this.loggerFactory = loggerFactory ?? (manifest => new PluginLogger(manifest.Id));
            this.scriptFactory = scriptFactory;
        }

        /// <summary>Loads a discovered plugin and returns its entry, or <c>null</c> if its context could not be created.</summary>
        public LoadedPlugin? Load(PluginLoadResult result)
        {
            var manifest = result.Manifest;
            PluginLogger logger = loggerFactory(manifest);

            ApiServices services;
            LoadedPlugin entry;

            try
            {
                services = servicesFactory(manifest);
                var context = new PluginContext(services, new PluginEventService(events, manifest, logger), new SchedulerService(), logger, manifest);
                entry = new LoadedPlugin(result.CSharpPlugin, context, manifest);
            }
            catch (Exception e)
            {
                logger.Error($"failed to create plugin context: {e.Message}");
                return null;
            }

            loaded.Add(entry);

            try
            {
                events.Publish(new PluginLoadingEvent(manifest));

                if (result.CSharpPlugin == null)
                {
                    if (result.HasJavaScript)
                        logger.Info("javascript entry registered");

                    events.Publish(new PluginLoadedEvent(manifest));

                    entry.State = PluginState.Running;
                    services.Output.Log(OutputLevel.Info, entry.Name, "loaded");
                }
                else
                {
                    result.CSharpPlugin.Load(entry.Context);

                    events.Publish(new PluginLoadedEvent(manifest));

                    entry.State = PluginState.Running;
                    services.Output.Log(OutputLevel.Info, entry.Name, "loaded");
                }
            }
            catch (Exception e)
            {
                fail(entry, e);
            }

            if (entry.State == PluginState.Running)
                attachScripts(entry, result.JavaScriptEntryPoint);

            LoadedPluginsChanged?.Invoke();
            return entry;
        }

        /// <summary>
        /// Registers the built-in "system" plugin written into the scripting subsystem. No assembly is loaded;
        /// the entry only holds state and drives the scheduler.
        /// </summary>
        public LoadedPlugin LoadSystemPlugin(PluginManifest manifest)
        {
            PluginLogger logger = loggerFactory(manifest);
            ApiServices services = servicesFactory(manifest);
            var context = new PluginContext(services, new PluginEventService(events, manifest, logger), new SchedulerService(), logger, manifest);
            var entry = new LoadedPlugin(null, context, manifest);

            events.Publish(new PluginLoadingEvent(manifest));

            loaded.Add(entry);

            events.Publish(new PluginLoadedEvent(manifest));

            entry.State = PluginState.Running;
            services.Output.Log(OutputLevel.Info, entry.Name, "loaded (system)");

            attachScripts(entry, null);

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
                entry.Scripts?.Shutdown();
            }
            catch (Exception e)
            {
                entry.Logger.Error(e.Message);
                entry.Context.Services.Output.Log(OutputLevel.Error, entry.Name, e.Message);
            }

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

        /// <summary>Unloads every loaded plugin, external plugins first and the built-in system plugin last.</summary>
        public void Shutdown()
        {
            LoadedPlugin[] snapshot = loaded.ToArray();

            for (int i = snapshot.Length - 1; i >= 0; i--)
                Unload(snapshot[i]);
        }

        public void UnloadAll() => Shutdown();

        /// <summary>Advances every running plugin's frame callbacks and timers. Called on the update thread.</summary>
        public void Tick(double currentTime)
        {
            foreach (LoadedPlugin entry in loaded.ToArray())
            {
                if (entry.State != PluginState.Running)
                    continue;

                entry.Context.Scheduler.Tick(currentTime, e => fail(entry, e));

                if (entry.State != PluginState.Running)
                    continue;

                entry.Scripts?.Tick(currentTime);
            }
        }

        /// <summary>Applies updated global scripting preferences to every loaded plugin.</summary>
        public void ApplyScriptingSettings(ScriptingSettings settings)
        {
            foreach (LoadedPlugin entry in loaded.ToArray())
                entry.Scripts?.ApplySettings(settings);
        }

        private void attachScripts(LoadedPlugin entry, string? jsEntryPoint)
        {
            if (scriptFactory == null)
                return;

            try
            {
                PluginScriptHost? scripts = scriptFactory(entry.Manifest, jsEntryPoint, entry.Context);
                entry.Scripts = scripts;
                scripts?.Start();
            }
            catch (Exception e)
            {
                string message = $"failed to initialise plugin scripts: {e.Message}";
                entry.Logger.Error(message);
                entry.Context.Services.Output.Log(OutputLevel.Error, entry.Name, message);
                events.Publish(new PluginErrorEvent(entry.Manifest, message));
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
