// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Online.Server;

namespace osu.Game.API.Server
{
    /// <summary>
    /// Maintains the registry of all servers available at runtime.
    /// Servers are registered dynamically (e.g. by plugins or scripts) and are never
    /// persisted to the user's configuration file. Only the selected server's <see cref="IServer.Id"/>
    /// is persisted.
    /// </summary>
    public class ServerRegistry : IServerRegistry
    {
        private readonly Dictionary<string, IServer> servers = new Dictionary<string, IServer>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fired whenever the set of available servers changes.
        /// </summary>
        public event Action? ServersChanged;

        /// <summary>
        /// A bindable list of all currently registered servers, sorted by display name.
        /// </summary>
        public IBindableList<IServer> AvailableServers => availableServers;

        private readonly BindableList<IServer> availableServers = new BindableList<IServer>();

        /// <summary>
        /// Creates a registry pre-populated with the default servers (<see cref="BanchoServer"/>
        /// and <see cref="DevServer"/>).
        /// </summary>
        public static ServerRegistry CreateDefault()
        {
            var registry = new ServerRegistry();
            registry.Register(new BanchoServer());
            registry.Register(new DevServer());
            return registry;
        }

        /// <summary>
        /// Registers a server. If a server with the same <see cref="IServer.Id"/> already exists, it is replaced.
        /// </summary>
        public void Register(IServer server)
        {
            ArgumentNullException.ThrowIfNull(server);

            if (string.IsNullOrEmpty(server.Id))
                throw new ArgumentException("Server must have a non-empty Id.", nameof(server));

            bool replaced = servers.ContainsKey(server.Id);
            servers[server.Id] = server;

            syncBindableList();
            ServersChanged?.Invoke();
        }

        /// <summary>
        /// Removes a server by its <see cref="IServer.Id"/>.
        /// </summary>
        public void Unregister(string serverId)
        {
            if (string.IsNullOrEmpty(serverId))
                return;

            if (servers.Remove(serverId))
            {
                syncBindableList();
                ServersChanged?.Invoke();
            }
        }

        /// <summary>
        /// Returns the server with the given <paramref name="serverId"/>, or <c>null</c> if not found.
        /// </summary>
        public IServer? GetServer(string serverId)
        {
            if (string.IsNullOrEmpty(serverId))
                return null;

            servers.TryGetValue(serverId, out var server);
            return server;
        }

        private void syncBindableList()
        {
            availableServers.Clear();
            foreach (var server in servers.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
                availableServers.Add(server);
        }
    }
}