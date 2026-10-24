// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Plugins
{
    /// <summary>A plugin instance together with its context and current state.</summary>
    public class LoadedPlugin
    {
        public IPlugin Plugin { get; }

        public IPluginContext Context { get; }

        public string Name => Plugin.GetType().Name;

        public PluginState State { get; internal set; } = PluginState.Stopped;

        internal LoadedPlugin(IPlugin plugin, IPluginContext context)
        {
            Plugin = plugin;
            Context = context;
        }
    }
}
