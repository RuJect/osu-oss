// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.API.Plugins;
using osu.Game.Events;

namespace osu.Game.API.Events
{
    /// <summary>A plugin is about to load and receive its context.</summary>
    public sealed record PluginLoadingEvent(PluginManifest Manifest) : OsuApiEvent;

    /// <summary>A plugin finished loading.</summary>
    public sealed record PluginLoadedEvent(PluginManifest Manifest) : OsuApiEvent;

    /// <summary>A plugin is about to be unloaded.</summary>
    public sealed record PluginUnloadingEvent(PluginManifest Manifest) : OsuApiEvent;

    /// <summary>A plugin was unloaded; its event subscriptions and scheduler are released.</summary>
    public sealed record PluginUnloadedEvent(PluginManifest Manifest) : OsuApiEvent;

    /// <summary>A plugin failed to load or threw while running; its context has been released.</summary>
    public sealed record PluginLoadFailedEvent(PluginManifest Manifest, string Message) : OsuApiEvent;
}
