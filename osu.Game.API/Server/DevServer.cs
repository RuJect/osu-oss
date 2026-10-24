// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API;
using osu.Game.Online.Server;

namespace osu.Game.API.Server
{
    /// <summary>
    /// The official osu! development server (dev.ppy.sh).
    /// </summary>
    public class DevServer : IServer
    {
        public string Id => "dev";

        public string Name => "Development (dev.ppy.sh)";

        public ServerConfiguration Endpoints { get; } = new ServerConfiguration
        {
            Id = "dev",
            Name = "Development (dev.ppy.sh)",
            Description = "The official osu! development server.",
            WebsiteUrl = @"https://dev.ppy.sh",
            APIUrl = @"https://dev.ppy.sh",
            APIClientSecret = @"3LP2mhUrV89xxzD1YKNndXHEhWWCRLPNKioZ9ymT",
            APIClientID = "5",
            SpectatorUrl = @"https://dev.ppy.sh/signalr/spectator",
            MultiplayerUrl = @"https://dev.ppy.sh/signalr/multiplayer",
            MetadataUrl = @"https://dev.ppy.sh/signalr/metadata",
            BeatmapSubmissionServiceUrl = @"https://dev.ppy.sh/beatmap-submission",
        };

        public bool HandleRequest(APIRequest request, IAPIProvider api) => false;

        public override string ToString() => Name;
    }
}