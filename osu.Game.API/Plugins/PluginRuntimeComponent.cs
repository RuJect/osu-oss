// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// A zero-sized component whose only purpose is to drive <see cref="PluginHost.Tick"/> on the update thread.
    /// </summary>
    public partial class PluginRuntimeComponent : Container
    {
        private readonly PluginHost host;

        public PluginRuntimeComponent(PluginHost host)
        {
            this.host = host;
        }

        protected override void Update()
        {
            base.Update();
            host.Tick(Clock.CurrentTime);
        }
    }
}
