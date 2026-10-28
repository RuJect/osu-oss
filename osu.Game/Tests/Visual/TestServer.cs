// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Server;

namespace osu.Game.Tests.Visual
{
    internal sealed class TestServerRegistry : IServerRegistry
    {
        private readonly TestServer server = new TestServer();

#pragma warning disable CS0067 // The test registry never changes.
        public event Action? ServersChanged;
#pragma warning restore CS0067

        public IBindableList<IServer> AvailableServers => availableServers;

        private readonly BindableList<IServer> availableServers = new BindableList<IServer>();

        public TestServerRegistry()
        {
            availableServers.Add(server);
        }

        public void Register(IServer server)
        {
        }

        public void Unregister(string serverId)
        {
        }

        public IServer? GetServer(string serverId) => serverId == server.Id ? server : null;
    }

    internal sealed class TestServerManager : IServerManager
    {
        public TestServerManager(IServerRegistry registry)
        {
            ActiveServer = new Bindable<IServer>(registry.GetServer("test-dev")!);
            SelectedServerId = new Bindable<string>("test-dev");
        }

        public IBindable<IServer> ActiveServer { get; }

        public IBindable<string> SelectedServerId { get; }

#pragma warning disable CS0067 // The test server never changes.
        public event Action<IServer>? ActiveServerChanged;
#pragma warning restore CS0067

        public void SelectServer(string serverId)
        {
        }

        public void RefreshActiveServer()
        {
        }
    }

    internal sealed class TestServer : IServer
    {
        public string Id => "test-dev";

        public string Name => "Test Development Server";

        public ServerConfiguration Endpoints { get; } = new ServerConfiguration(new DevelopmentEndpointConfiguration())
        {
            Id = "test-dev",
            Name = "Test Development Server",
            Description = "The development server used by tests.",
        };

        public bool HandleRequest(APIRequest request, IAPIProvider api) => false;
    }
}
