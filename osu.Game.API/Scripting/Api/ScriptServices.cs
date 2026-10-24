// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Overlays;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Bundle of cross-cutting services exposed to <see cref="IScriptApiProvider"/>s while building the
    /// global API objects for a script instance.
    /// </summary>
    public class ScriptServices
    {
        public OsuGame Game { get; }

        public GameHost Host { get; }

        public string Version { get; }

        public ScriptRepository Repository { get; }

        public ScriptOutputLog Output { get; }

        public ScriptEventBus Events { get; }

        /// <summary>
        /// Storage scoped to the scripts data directory (<c>osu-oss/scripts/data</c>), resolved once the
        /// game has loaded. Scripts may freely read and write files here.
        /// </summary>
        public ScriptDataStorage? DataStorage { get; set; }

        /// <summary>The global music controller, resolved once the game has loaded.</summary>
        public MusicController? Music { get; set; }

        /// <summary>The global osu! settings config, resolved once the game has loaded.</summary>
        public OsuConfigManager? Config { get; set; }

        /// <summary>The notification overlay, resolved once the game has loaded.</summary>
        public INotificationOverlay? Notifications { get; set; }

        /// <summary>The game's root input manager, resolved once the game has loaded.</summary>
        public InputManager? InputManager { get; set; }

        public ScriptServices(OsuGame game, GameHost host, ScriptRepository repository, ScriptOutputLog output, ScriptEventBus events)
        {
            Game = game;
            Host = host;
            Version = game.Version;
            Repository = repository;
            Output = output;
            Events = events;
        }
    }
}
