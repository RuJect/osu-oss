// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Game.Online.Server;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Services handed to a plugin on load. Shared capabilities delegate live to the common
    /// <see cref="ApiServices"/> (so late-resolved services become visible automatically); per-plugin
    /// capabilities (event gate, scheduler, storage, logger, manifest) are owned by this context.
    /// </summary>
    public class PluginContext
    {
        private readonly ApiServices services;

        public PluginContext(ApiServices services, PluginEventService events, SchedulerService scheduler,
                             StorageService? storage, PluginLogger logger, PluginManifest manifest)
        {
            this.services = services;
            Events = events;
            Scheduler = scheduler;
            Storage = storage;
            Logger = logger;
            Manifest = manifest;
        }

        public string Version => services.Version;

        public OsuGame Game => services.Game;

        public GameHost Host => services.Host;

        public OutputLog Output => services.Output;

        /// <summary>Permission-gated access to the shared event bus.</summary>
        public PluginEventService Events { get; }

        /// <summary>Storage scoped to the plugin's data directory, or <c>null</c> without the "data" permission.</summary>
        public StorageService? Storage { get; }

        public MusicService? Music => services.Music;

        public ConfigService? Config => services.Config;

        public NotificationService? Notifications => services.Notifications;

        /// <summary>The plugin's own scheduler, advanced by the host on the update thread.</summary>
        public SchedulerService Scheduler { get; }

        public PluginLogger Logger { get; }

        public PluginManifest Manifest { get; }

        public IServerRegistry? Servers => services.Servers;

        public IServerManager? ServerManager => services.ServerManager;

        public InputManager? InputManager => services.InputManager;
    }
}
