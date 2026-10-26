// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;
using osu.Game.Events;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Services handed to an <see cref="IPlugin"/> on load. Mirrors the capabilities available to scripts.
    /// </summary>
    public interface IPluginContext
    {
        string Version { get; }

        OsuGame Game { get; }

        GameHost Host { get; }

        OutputLog Output { get; }

        /// <summary>The shared typed event bus.</summary>
        OsuEventBus Events { get; }

        /// <summary>Storage scoped to the scripts data directory, or <c>null</c> before the game has loaded.</summary>
        StorageService? Storage { get; }

        MusicService? Music { get; }

        ConfigService? Config { get; }

        NotificationService? Notifications { get; }

        /// <summary>The plugin's own scheduler, advanced by the host on the update thread.</summary>
        SchedulerService Scheduler { get; }
    }
}
