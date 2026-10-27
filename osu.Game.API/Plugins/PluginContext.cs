// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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

        public PluginContext(ApiServices services, PluginEventService events, SchedulerService scheduler,
                             PluginLogger logger, PluginManifest manifest)
        {
            Services = services;
            Events = events;
            Scheduler = scheduler;
            Logger = logger;
            Manifest = manifest;
        }

        public void Dispose()
        {
            Events.Dispose();
            Scheduler.Clear();
        }
    }
}
