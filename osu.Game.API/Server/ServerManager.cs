// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Online.Server;

namespace osu.Game.API.Server
{
    /// <summary>
    /// Manages the currently active server, bridging the runtime <see cref="IServerRegistry"/>
    /// with the user's persisted <see cref="OsuSetting.SelectedServerId"/> preference.
    /// </summary>
    public class ServerManager : IServerManager
    {
        private readonly IServerRegistry registry;
        private readonly OsuConfigManager config;

        /// <summary>
        /// The currently active server. When this changes, the game's online clients
        /// should disconnect from the previous server and reconnect to the new one.
        /// </summary>
        public IBindable<IServer> ActiveServer => activeServer;

        private readonly Bindable<IServer> activeServer;

        /// <summary>
        /// The ID of the currently active server, persisted in the user's configuration.
        /// </summary>
        public IBindable<string> SelectedServerId => selectedServerId;

        private readonly Bindable<string> selectedServerId;

        /// <summary>
        /// Fired when the active server changes as a result of a user preference change
        /// or a registry mutation.
        /// </summary>
        public event Action<IServer>? ActiveServerChanged;

        public ServerManager(IServerRegistry registry, OsuConfigManager config)
        {
            this.registry = registry;
            this.config = config;

            selectedServerId = config.GetBindable<string>(OsuSetting.SelectedServerId);
            selectedServerId.ValueChanged += onSelectedServerIdChanged;

            activeServer = new Bindable<IServer>(resolveActiveServer());

            registry.ServersChanged += onServersChanged;
        }

        /// <summary>
        /// Attempts to set the active server by its <see cref="IServer.Id"/>.
        /// Updates the persisted config and triggers a server switch if the server is registered.
        /// </summary>
        public void SelectServer(string serverId)
        {
            if (string.IsNullOrEmpty(serverId))
                throw new ArgumentNullException(nameof(serverId));

            config.SetValue(OsuSetting.SelectedServerId, serverId);
        }

        /// <summary>
        /// Refreshes the active server after the registry has been mutated (e.g. a plugin was unloaded).
        /// If the currently selected server is no longer registered, falls back to the default.
        /// </summary>
        public void RefreshActiveServer()
        {
            var resolved = resolveActiveServer();
            if (!ReferenceEquals(activeServer.Value, resolved))
            {
                activeServer.Value = resolved;
                ActiveServerChanged?.Invoke(resolved);
            }
        }

        private void onSelectedServerIdChanged(ValueChangedEvent<string> e)
        {
            var resolved = registry.GetServer(e.NewValue) ?? registry.GetServer("bancho");
            if (resolved == null)
                throw new InvalidOperationException("No servers are registered in the registry.");

            if (!ReferenceEquals(activeServer.Value, resolved))
            {
                activeServer.Value = resolved;
                ActiveServerChanged?.Invoke(resolved);
            }
        }

        private void onServersChanged()
        {
            // If the selected server was removed from the registry, fall back to the default.
            if (registry.GetServer(selectedServerId.Value) == null)
                config.SetValue(OsuSetting.SelectedServerId, "bancho");
            else
                RefreshActiveServer();
        }

        private IServer resolveActiveServer()
        {
            return registry.GetServer(selectedServerId.Value)
                ?? registry.GetServer("bancho")
                ?? throw new InvalidOperationException("The default 'bancho' server is not registered.");
        }
    }
}