// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Platform;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Per-plugin services handed to a plugin on load. Game-level capabilities live in the scoped
    /// <see cref="Services"/> bundle; per-plugin capabilities (event gate, scheduler, logger, manifest)
    /// are owned by this context.
    /// </summary>
    public class PluginContext : IDisposable
    {
        /// <summary>The immutable service bundle scoped to this plugin.</summary>
        public ApiServices Services { get; }

        /// <summary>Permission-gated access to the shared event bus.</summary>
        public PluginEventService Events { get; }

        /// <summary>The plugin's own scheduler, advanced by the host on the update thread.</summary>
        public SchedulerService Scheduler { get; }

        public PluginLogger Logger { get; }

        public PluginManifest Manifest { get; }

        /// <summary>Storage for this plugin's persistent data, or <c>null</c> without the "data" permission.</summary>
        public StorageService? Data => Services.Data;

        /// <summary>Read-only access to this plugin's resources directory.</summary>
        public ResourceStore? Resources => Services.Resources;

        /// <summary>This plugin's configuration, or <c>null</c> if it has not created one.</summary>
        public IPluginConfig? Config => config;

        private readonly Storage? configStorage;
        private IPluginConfig? config;

        public PluginContext(ApiServices services, PluginEventService events, SchedulerService scheduler,
                             PluginLogger logger, PluginManifest manifest, Storage? configStorage = null)
        {
            Services = services;
            Events = events;
            Scheduler = scheduler;
            Logger = logger;
            Manifest = manifest;
            this.configStorage = configStorage;
        }

        /// <summary>
        /// Creates this plugin's configuration for <typeparamref name="TSetting"/>. Only one configuration
        /// contract may be created per plugin.
        /// </summary>
        /// <exception cref="InvalidOperationException">A different setting type was already configured, or no config storage is available.</exception>
        public PluginConfigManager<TSetting> CreateConfig<TSetting>(Action<PluginConfigManager<TSetting>>? configureDefaults = null)
            where TSetting : struct, Enum
        {
            if (config != null)
            {
                if (config is PluginConfigManager<TSetting> existing)
                    return existing;

                throw new InvalidOperationException("only one config contract per plugin");
            }

            Storage storage = configStorage ?? throw new InvalidOperationException("plugin config storage is not available");
            var manager = new PluginConfigManager<TSetting>(storage, Logger, configureDefaults);
            config = manager;
            return manager;
        }

        public void Dispose()
        {
            config?.Dispose();
            Events.Dispose();
            Scheduler.Clear();
        }
    }
}
