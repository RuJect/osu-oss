// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Configuration;

namespace osu.Game.Online.Server
{
    /// <summary>
    /// Primary contract for the manager that bridges the runtime <see cref="IServerRegistry"/>
    /// with the user's persisted <see cref="OsuSetting.SelectedServerId"/> preference.
    /// Concrete implementations live in <c>osu.Game.API</c>; this contract is the only
    /// dependency that <c>osu.Game</c> needs to wire the subsystem.
    /// </summary>
    public interface IServerManager
    {
        /// <summary>
        /// The currently active server. When this changes, the game's online clients
        /// should disconnect from the previous server and reconnect to the new one.
        /// </summary>
        IBindable<IServer> ActiveServer { get; }

        /// <summary>
        /// The ID of the currently active server, persisted in the user's configuration.
        /// </summary>
        IBindable<string> SelectedServerId { get; }

        /// <summary>
        /// Fired when the active server changes as a result of a user preference change
        /// or a registry mutation.
        /// </summary>
        event Action<IServer>? ActiveServerChanged;

        /// <summary>
        /// Attempts to set the active server by its <see cref="IServer.Id"/>.
        /// Updates the persisted config and triggers a server switch if the server is registered.
        /// </summary>
        void SelectServer(string serverId);

        /// <summary>
        /// Refreshes the active server after the registry has been mutated (e.g. a plugin was unloaded).
        /// If the currently selected server is no longer registered, falls back to the default.
        /// </summary>
        void RefreshActiveServer();
    }
}