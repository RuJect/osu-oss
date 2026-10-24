// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Online.Server;

namespace osu.Game.Online.Server
{
    /// <summary>
    /// Primary contract for the registry of all servers available at runtime.
    /// Concrete implementations live in <c>osu.Game.API</c>; this contract is the only
    /// dependency that <c>osu.Game</c> needs to wire the subsystem.
    /// </summary>
    public interface IServerRegistry
    {
        /// <summary>
        /// Fired whenever the set of available servers changes.
        /// </summary>
        event Action? ServersChanged;

        /// <summary>
        /// A bindable list of all currently registered servers, sorted by display name.
        /// </summary>
        IBindableList<IServer> AvailableServers { get; }

        /// <summary>
        /// Registers a server. If a server with the same <see cref="IServer.Id"/> already exists, it is replaced.
        /// </summary>
        void Register(IServer server);

        /// <summary>
        /// Removes a server by its <see cref="IServer.Id"/>.
        /// </summary>
        void Unregister(string serverId);

        /// <summary>
        /// Returns the server with the given <paramref name="serverId"/>, or <c>null</c> if not found.
        /// </summary>
        IServer? GetServer(string serverId);
    }
}