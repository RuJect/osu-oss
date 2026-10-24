// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online;

namespace osu.Game.Online.Server
{
    /// <summary>
    /// Holds the configuration and metadata for a specific online server.
    /// Extends <see cref="EndpointConfiguration"/> so it can be passed directly to existing
    /// clients (e.g. <see cref="APIAccess"/>, <see cref="Online.Spectator.OnlineSpectatorClient"/>)
    /// without requiring changes to their constructors.
    /// </summary>
    public class ServerConfiguration : EndpointConfiguration
    {
        /// <summary>
        /// The unique identifier of the server (e.g. <c>"bancho"</c>, <c>"dev"</c>).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The display name of the server.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A short description of the server, shown in the settings UI.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}