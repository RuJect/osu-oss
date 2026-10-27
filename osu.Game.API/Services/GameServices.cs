// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Game.API.Plugins;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.Online.Server;
using osu.Game.Overlays;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Immutable snapshot of the game-level dependencies required by plugins and scripts, resolved once
    /// after the game has loaded. Creates a fresh scoped <see cref="ApiServices"/> for every plugin.
    /// </summary>
    public sealed class GameServices
    {
        public OsuGame Game { get; }

        public GameHost Host { get; }

        public OutputLog Output { get; }

        /// <summary>The root osu! storage.</summary>
        public Storage Storage { get; }

        /// <summary>Storage scoped to the <c>osu-oss/scripts</c> directory.</summary>
        public Storage ScriptsStorage { get; }

        public MusicController Music { get; }

        public OsuConfigManager Config { get; }

        public INotificationOverlay Notifications { get; }

        public IServerRegistry Servers { get; }

        public IServerManager ServerManager { get; }

        public Func<InputManager?> InputManager { get; }

        public GameServices(OsuGame game, GameHost host, OutputLog output, Storage storage, Storage scriptsStorage,
                            MusicController music, OsuConfigManager config, INotificationOverlay notifications,
                            IServerRegistry servers, IServerManager serverManager, Func<InputManager?> inputManager)
        {
            Game = game;
            Host = host;
            Output = output;
            Storage = storage;
            ScriptsStorage = scriptsStorage;
            Music = music;
            Config = config;
            Notifications = notifications;
            Servers = servers;
            ServerManager = serverManager;
            InputManager = inputManager;
        }

        /// <summary>Creates a per-plugin service bundle with storage scoped to that plugin.</summary>
        public ApiServices CreatePluginServices(PluginManifest manifest)
        {
            if (!manifest.IsValidId)
                throw new ArgumentException("Plugin id must match ^[a-z][a-z0-9_]*$.", nameof(manifest));

            StorageService? storage = manifest.Permissions.CanUseData
                ? new StorageService(new FileStore(SpecialStorage.GetPluginData(Storage, manifest.Id)))
                : null;

            var resources = new ResourceStore(new FileStore(SpecialStorage.GetPluginResources(Storage, manifest.Id)));

            OutputLog output = manifest.Id == PluginManifest.SYSTEM_ID ? Output : new OutputLog();

            return new ApiServices(Game, Host, output, storage,
                new MusicService(Music, Host), new ConfigService(Config), new NotificationService(Notifications),
                InputManager, Servers, ServerManager, resources);
        }

        /// <summary>Creates a logger writing to the logs directory scoped to <paramref name="manifest"/>.</summary>
        public PluginLogger CreatePluginLogger(PluginManifest manifest)
            => new PluginLogger(manifest.Id, SpecialStorage.GetPluginLogs(Storage, manifest.Id));

        /// <summary>Returns the storage used to load and save <paramref name="manifest"/>'s configuration.</summary>
        public Storage CreatePluginConfigStorage(PluginManifest manifest)
            => SpecialStorage.GetPluginRoot(Storage, manifest.Id);

        /// <summary>Creates a logger for errors that occur before a plugin logger can be scoped (e.g. repository scanning).</summary>
        public PluginLogger CreateLogFallback() => new PluginLogger("plugins", SpecialStorage.GetLogs(Storage));
    }
}
