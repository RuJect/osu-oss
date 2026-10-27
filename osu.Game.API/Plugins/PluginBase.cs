// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Base class for C# plugins. Override <see cref="Load"/> and <see cref="Unload"/> to implement plugin logic.
    /// </summary>
    public abstract class PluginBase
    {
        /// <summary>
        /// Invoked once when the plugin is loaded. Use <paramref name="context"/> to reach game services.
        /// </summary>
        public virtual void Load(PluginContext context) { }

        /// <summary>
        /// Invoked once when the plugin is unloaded. Release any resources registered on the context here.
        /// </summary>
        public virtual void Unload() { }
    }
}
