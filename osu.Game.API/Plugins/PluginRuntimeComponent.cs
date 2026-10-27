// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Containers;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// A zero-sized component that activates one discovered plugin per frame and drives
    /// <see cref="PluginHost.Tick"/> on the update thread, while resolving the game's root input manager
    /// for the plugin services.
    /// </summary>
    public partial class PluginRuntimeComponent : Container
    {
        private readonly PluginHost host;
        private readonly InputManagerProvider input;
        private readonly Action activateNext;

        public PluginRuntimeComponent(PluginHost host, InputManagerProvider input, Action activateNext)
        {
            this.host = host;
            this.input = input;
            this.activateNext = activateNext;
        }

        protected override void Update()
        {
            base.Update();

            input.Value = GetContainingInputManager() ?? input.Value;
            activateNext();
            host.Tick(Clock.CurrentTime);
        }
    }
}
