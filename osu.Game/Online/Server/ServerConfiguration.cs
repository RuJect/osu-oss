// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online.Server
{
    /// <summary>
    /// Holds the configuration and metadata for a specific online server.
    /// Extends <see cref="EndpointConfiguration"/> so it can be passed directly to existing
    /// clients (e.g. <see cref="osu.Game.Online.API.APIAccess"/>, <see cref="Online.Spectator.OnlineSpectatorClient"/>)
    /// without requiring changes to their constructors.
    /// </summary>
    public class ServerConfiguration : EndpointConfiguration
    {
        /// <summary>
        /// Creates a new <see cref="ServerConfiguration"/> by copying all endpoint properties
        /// from the supplied <paramref name="endpoints"/>.
        /// </summary>
        public ServerConfiguration(EndpointConfiguration endpoints)
        {
            APIClientSecret = endpoints.APIClientSecret;
            APIClientID = endpoints.APIClientID;
            WebsiteUrl = endpoints.WebsiteUrl;
            APIUrl = endpoints.APIUrl;
            BeatmapSubmissionServiceUrl = endpoints.BeatmapSubmissionServiceUrl;
            SpectatorUrl = endpoints.SpectatorUrl;
            MultiplayerUrl = endpoints.MultiplayerUrl;
            MetadataUrl = endpoints.MetadataUrl;
            LivenessProbeUrl = endpoints.LivenessProbeUrl;
        }

        /// <summary>
        /// Creates a new <see cref="ServerConfiguration"/> with default values.
        /// </summary>
        public ServerConfiguration()
        {
        }

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