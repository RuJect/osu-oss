// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API;
using osu.Game.Online.Server;

namespace osu.Game.API.Server
{
    /// <summary>
    /// The official osu! Bancho server (production).
    /// </summary>
    public class BanchoServer : IServer
    {
        public string Id => "bancho";

        public string Name => "Bancho (Official)";

        public ServerConfiguration Endpoints { get; } = new ServerConfiguration
        {
            Id = "bancho",
            Name = "Bancho (Official)",
            Description = "The official osu! server.",
            WebsiteUrl = @"https://osu.ppy.sh",
            APIUrl = @"https://osu.ppy.sh",
            APIClientSecret = @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk",
            APIClientID = "5",
            SpectatorUrl = "https://spectator.osu.ppy.sh/spectator",
            MultiplayerUrl = "https://spectator.osu.ppy.sh/multiplayer",
            MetadataUrl = "https://spectator.osu.ppy.sh/metadata",
            BeatmapSubmissionServiceUrl = "https://bss.ppy.sh",
        };

        public bool HandleRequest(APIRequest request, IAPIProvider api) => false;

        public override string ToString() => Name;
    }
}