// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// A zero-sized component that drives <see cref="PluginHost.Tick"/> on the update thread and resolves
    /// the game's root input manager for the plugin services.
    /// </summary>
    public partial class PluginRuntimeComponent : Container
    {
        private readonly PluginHost host;
        private readonly InputManagerProvider input;

        public PluginRuntimeComponent(PluginHost host, InputManagerProvider input)
        {
            this.host = host;
            this.input = input;
        }

        protected override void Update()
        {
            base.Update();

            input.Value = GetContainingInputManager() ?? input.Value;
            host.Tick(Clock.CurrentTime);
        }
    }
}
