// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Plugins
{
    /// <summary>A loaded plugin entry: its optional C# instance, context described by the manifest, and current state.</summary>
    public class LoadedPlugin
    {
        /// <summary>The C# plugin instance, or <c>null</c> for the system plugin.</summary>
        public PluginBase? Plugin { get; }

        public PluginContext Context { get; }

        public PluginManifest Manifest { get; }

        public string Name => Manifest.Id;

        public PluginLogger Logger => Context.Logger;

        public PluginState State { get; internal set; } = PluginState.Stopped;

        internal LoadedPlugin(PluginBase? plugin, PluginContext context, PluginManifest manifest)
        {
            Plugin = plugin;
            Context = context;
            Manifest = manifest;
        }
    }
}
