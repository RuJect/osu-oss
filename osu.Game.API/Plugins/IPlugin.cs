// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// A C# plugin loaded in-process. Plugins run with full trust and consume the same services as scripts
    /// via <see cref="IPluginContext"/>.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Invoked once when the plugin is loaded. Use <paramref name="context"/> to reach game services.
        /// </summary>
        void Load(IPluginContext context);

        /// <summary>
        /// Invoked once when the plugin is unloaded. Release any resources registered on the context here.
        /// </summary>
        void Unload();
    }
}
