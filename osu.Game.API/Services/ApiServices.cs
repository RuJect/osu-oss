// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Game.Online.Server;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Bundle of cross-cutting game services scoped to a single plugin. Holds no scripting (Jint) types so it
    /// can be consumed by either runtime; the scripting subsystem reuses the same instance through
    /// <c>ScriptServices</c>.
    /// </summary>
    public class ApiServices
    {
        private readonly Func<InputManager?>? inputManager;

        public OsuGame Game { get; }

        public GameHost Host { get; }

        public string Version => Game.Version;

        public OutputLog Output { get; }

        /// <summary>Storage scoped to this plugin's data directory, or <c>null</c> without the "data" permission.</summary>
        public StorageService? Storage { get; }

        /// <summary>Alias for <see cref="Storage"/>.</summary>
        public StorageService? Data => Storage;

        /// <summary>Read-only access to this plugin's resources directory.</summary>
        public ResourceStore? Resources { get; }

        /// <summary>The global music controller.</summary>
        public MusicService? Music { get; }

        /// <summary>The global osu! settings config.</summary>
        public ConfigService? Config { get; }

        /// <summary>The notification overlay.</summary>
        public NotificationService? Notifications { get; }

        /// <summary>The game's root input manager, if it has been resolved.</summary>
        public InputManager? InputManager => inputManager?.Invoke();

        /// <summary>The registry of all servers available at runtime. Plugins and scripts can register
        /// custom servers here; they are never persisted to the user's configuration file.</summary>
        public IServerRegistry? Servers { get; }

        /// <summary>The manager that bridges the registry with the user's persisted server preference.</summary>
        public IServerManager? ServerManager { get; }

        public ApiServices(OsuGame game, GameHost host, OutputLog output, StorageService? storage = null,
                           MusicService? music = null, ConfigService? config = null,
                           NotificationService? notifications = null, Func<InputManager?>? inputManager = null,
                           IServerRegistry? servers = null, IServerManager? serverManager = null,
                           ResourceStore? resources = null)
        {
            Game = game;
            Host = host;
            Output = output;
            Storage = storage;
            Music = music;
            Config = config;
            Notifications = notifications;
            this.inputManager = inputManager;
            Servers = servers;
            ServerManager = serverManager;
            Resources = resources;
        }

        /// <summary>
        /// Copies every value from <paramref name="other"/> without creating fresh service wrappers. Used by
        /// script-specific bundles that extend this one.
        /// </summary>
        protected ApiServices(ApiServices other)
        {
            Game = other.Game;
            Host = other.Host;
            Output = other.Output;
            Storage = other.Storage;
            Music = other.Music;
            Config = other.Config;
            Notifications = other.Notifications;
            inputManager = other.inputManager;
            Servers = other.Servers;
            ServerManager = other.ServerManager;
            Resources = other.Resources;
        }
    }
}
