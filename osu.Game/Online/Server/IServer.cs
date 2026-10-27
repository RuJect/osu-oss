// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Online.API;

namespace osu.Game.Online.Server
{
    /// <summary>
    /// Represents a registered online server that the game can connect to.
    /// Servers are registered at runtime (via plugins, scripts, or the application entry point)
    /// and are never persisted directly to the user's configuration file.
    /// </summary>
    public interface IServer
    {
        /// <summary>
        /// The unique identifier used to select this server from <see cref="OsuSetting.SelectedServerId"/>.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// A human-readable name displayed in the settings UI.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The endpoint configuration for this server.
        /// </summary>
        ServerConfiguration Endpoints { get; }

        /// <summary>
        /// Attempts to handle an API request with server-specific logic.
        /// </summary>
        /// <param name="request">The request being performed.</param>
        /// <param name="api">The API provider performing the request.</param>
        /// <returns><c>true</c> if the request was handled by the server; otherwise <c>false</c> to fall back to the default HTTP request pipeline.</returns>
        bool HandleRequest(APIRequest request, IAPIProvider api);
    }
}