// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input;
using osu.Framework.Platform;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Bundle of cross-cutting game services shared by scripts and C# plugins. Holds no scripting (Jint)
    /// types so it can be consumed by either runtime.
    /// </summary>
    public class ApiServices
    {
        public OsuGame Game { get; }

        public GameHost Host { get; }

        public string Version { get; }

        public OutputLog Output { get; }

        /// <summary>Storage scoped to the scripts data directory, resolved once the game has loaded.</summary>
        public StorageService? Storage { get; set; }

        /// <summary>The global music controller, resolved once the game has loaded.</summary>
        public MusicService? Music { get; set; }

        /// <summary>The global osu! settings config, resolved once the game has loaded.</summary>
        public ConfigService? Config { get; set; }

        /// <summary>The notification overlay, resolved once the game has loaded.</summary>
        public NotificationService? Notifications { get; set; }

        /// <summary>The game's root input manager, resolved once the game has loaded.</summary>
        public InputManager? InputManager { get; set; }

        public ApiServices(OsuGame game, GameHost host, OutputLog output)
        {
            Game = game;
            Host = host;
            Version = game.Version;
            Output = output;
        }
    }
}
