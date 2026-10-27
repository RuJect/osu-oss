// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online;
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

        public ServerConfiguration Endpoints { get; } = new ServerConfiguration(new DevelopmentEndpointConfiguration())
        {
            Id = "dev",
            Name = "Development (dev.ppy.sh)",
            Description = "The official osu! development server.",
        };

        public bool HandleRequest(APIRequest request, IAPIProvider api) => false;

        public override string ToString() => Name;
    }
}