// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;
using osu.Game.API.Events;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Default <see cref="IPluginContext"/> implementation. Wraps the shared <see cref="ApiServices"/> and
    /// adds a per-plugin <see cref="SchedulerService"/>.
    /// </summary>
    public class PluginContext : IPluginContext
    {
        private readonly ApiServices services;

        public PluginContext(ApiServices services, OsuEventBus events, SchedulerService scheduler)
        {
            this.services = services;
            Events = events;
            Scheduler = scheduler;
        }

        public string Version => services.Version;

        public OsuGame Game => services.Game;

        public GameHost Host => services.Host;

        public OutputLog Output => services.Output;

        public OsuEventBus Events { get; }

        public StorageService? Storage => services.Storage;

        public MusicService? Music => services.Music;

        public ConfigService? Config => services.Config;

        public NotificationService? Notifications => services.Notifications;

        public SchedulerService Scheduler { get; }
    }
}
