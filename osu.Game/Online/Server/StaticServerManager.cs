// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Online.API;
using osu.Game.Online.Server;

namespace osu.Game.Online.Server
{
    /// <summary>
    /// A trivial <see cref="IServerManager"/> backed by a static <see cref="EndpointConfiguration"/>.
    /// Used by <see cref="APIAccess"/>'s legacy constructor when the server subsystem is not wired
    /// (e.g. tests, tournament client). The active server never changes.
    /// </summary>
    internal sealed class StaticServerManager : IServerManager
    {
        private readonly Bindable<IServer> activeServer;

        public StaticServerManager(EndpointConfiguration endpoints)
        {
            activeServer = new Bindable<IServer>(new StaticServer(endpoints));
        }

        public IBindable<IServer> ActiveServer => activeServer;

        public IBindable<string> SelectedServerId => new Bindable<string>(string.Empty);

        public event Action<IServer>? ActiveServerChanged;

        public void SelectServer(string serverId)
        {
        }

        public void RefreshActiveServer()
        {
        }

        private sealed class StaticServer : IServer
        {
            private readonly ServerConfiguration configuration;

            public StaticServer(EndpointConfiguration endpoints)
            {
                configuration = new ServerConfiguration
                {
                    Id = "static",
                    Name = "Static",
                    Description = string.Empty,
                    APIClientID = endpoints.APIClientID,
                    APIClientSecret = endpoints.APIClientSecret,
                    WebsiteUrl = endpoints.WebsiteUrl,
                    APIUrl = endpoints.APIUrl,
                    BeatmapSubmissionServiceUrl = endpoints.BeatmapSubmissionServiceUrl,
                    SpectatorUrl = endpoints.SpectatorUrl,
                    MultiplayerUrl = endpoints.MultiplayerUrl,
                    MetadataUrl = endpoints.MetadataUrl,
                    LivenessProbeUrl = endpoints.LivenessProbeUrl,
                };
            }

            public string Id => "static";
            public string Name => "Static";
            public ServerConfiguration Endpoints => configuration;

            public bool HandleRequest(APIRequest request, IAPIProvider api) => false;
        }
    }
}