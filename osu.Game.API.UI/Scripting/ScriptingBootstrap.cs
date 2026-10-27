// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game;
using osu.Game.API.Plugins;
using osu.Game.API.Scripting;

namespace osu.Game.API.UI.Scripting
{
    /// <summary>
    /// Composition root for the scripting subsystem's user interface. Constructed from
    /// <c>osu.Desktop/Program.cs</c> around <c>host.Run(game)</c>; wires the
    /// <see cref="ScriptingConsoleOverlay"/> into the game once the runtime has initialised.
    /// </summary>
    public class ScriptingBootstrap : IDisposable
    {
        private readonly ScriptingLoader loader;
        private readonly OsuGame game;

        private ScriptingConsoleOverlay? overlay;
        private IDisposable? overlayRegistration;

        public ScriptManager? Manager => loader.Manager;

        /// <summary>
        /// Must be constructed after the <see cref="PluginBootstrap"/> which provides the system plugin
        /// context the scripting runtime executes in.
        /// </summary>
        public ScriptingBootstrap(OsuGame game, PluginBootstrap pluginBootstrap)
        {
            this.game = game;

            loader = new ScriptingLoader(game, pluginBootstrap);
            loader.Initialised += onInitialised;
        }

        private void onInitialised(ScriptManager manager)
        {
            overlay = new ScriptingConsoleOverlay(manager);
            overlayRegistration = game.RegisterExternalOverlay(overlay);
            game.ToggleScriptingConsole = toggle;
        }

        private void toggle() => overlay?.ToggleVisibility();

        public void Dispose()
        {
            loader.Initialised -= onInitialised;
            game.ToggleScriptingConsole = null;

            overlayRegistration?.Dispose();
            overlayRegistration = null;
            overlay = null;

            loader.Dispose();
        }
    }
}
