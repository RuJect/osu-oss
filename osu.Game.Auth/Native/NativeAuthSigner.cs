// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Connections.Client;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Metadata;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;
using osu.Game.Overlays;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Auth
{
    /// <summary>
    /// Installs <see cref="APIRequest.Sign"/> / <see cref="HubClientConnector.Sign"/>
    /// and wires game services. Clean-room reimplementation of the proprietary
    /// orchestrator (same observable behaviour, no VM).
    /// </summary>
    internal sealed class NativeAuthSigner : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        // Endpoints whose requests carry an integrity token.
        private static readonly Regex solo_scores = new Regex("/solo/scores(/\\d+)?$", RegexOptions.Compiled);
        private static readonly Regex playlist_scores = new Regex("/playlist/\\d+/scores(/\\d+)?$", RegexOptions.Compiled);
        private static readonly Regex achievements = new Regex("/me/achievements/\\d+$", RegexOptions.Compiled);
        private static readonly Regex users = new Regex("/users$", RegexOptions.Compiled);

        private readonly GameHost host;
        private readonly OsuGameBase game;

        private readonly NativeModule? native;

        private IAPIProvider? api;
        private MetadataClient? metadata;
        private SpectatorClient? spectator;
        private MultiplayerClient? multiplayer;
        private ILocalUserPlayInfo? playInfo;

        private readonly IBindable<APIState> apiState = new Bindable<APIState>(APIState.Offline);

        private bool initialised;
        private bool disposed;

        public NativeAuthSigner(GameHost host, OsuGameBase game)
        {
            this.host = host;
            this.game = game;

            if (typeof(WebRequest).GetField("rawContent",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null)
            {
                logger.Add("Could not initialize managed module.");
                return;
            }

            native = new NativeModule(host, (OsuGame)game, () => api?.AccessToken, IsTachyon);
            native.Initialized += onNativeInitialised;
            native.Reconnected += reconnectClients;

            game.OnLoadComplete += onLoadComplete;

            APIRequest.Sign = Sign;
            HubClientConnector.Sign = Sign;
        }

        private bool IsTachyon()
        {
            if (game.Version.Contains("tachyon", StringComparison.Ordinal))
                return true;

            var dependencies = game.Dependencies;
            return dependencies != null
                   && dependencies.Get<OsuConfigManager>()?.Get<ReleaseStream>(OsuSetting.ReleaseStream) == ReleaseStream.Tachyon;
        }

        private void onNativeInitialised() => ensureServices();

        private void onLoadComplete(Drawable _) => ensureServices();

        private void ensureServices()
        {
            if (initialised)
                return;

            var dependencies = game.Dependencies;
            if (dependencies == null)
                return;

            api = dependencies.Get<IAPIProvider>();
            NotificationRelay.Bind(dependencies.Get<INotificationOverlay>());
            metadata = dependencies.Get<MetadataClient>();
            spectator = dependencies.Get<SpectatorClient>();
            multiplayer = dependencies.Get<MultiplayerClient>();
            playInfo = dependencies.Get<ILocalUserPlayInfo>();

            if (api == null)
                return;

            initialised = true;

            apiState.BindTo(api.State);
            apiState.BindValueChanged(onApiStateChanged, true);

            playInfo?.PlayingState.BindValueChanged(onPlayingStateChanged);

            game.Add(new Medals.MedalAwarderContainer());
        }

        private void onApiStateChanged(ValueChangedEvent<APIState> e)
        {
            logger.Add($"API state changed to {e.NewValue}");

            if (e.NewValue == APIState.Online)
                reconnectClients();
        }

        private void reconnectClients()
        {
            try { _ = metadata?.Reconnect(); } catch (Exception e) { logger.Add($"Reconnect failed: {e}"); }
            try { _ = spectator?.Reconnect(); } catch (Exception e) { logger.Add($"Reconnect failed: {e}"); }
            try { _ = multiplayer?.Reconnect(); } catch (Exception e) { logger.Add($"Reconnect failed: {e}"); }
        }

        private void onPlayingStateChanged(ValueChangedEvent<LocalUserPlayingState> e)
        {
            try
            {
                native?.Report(e);
            }
            catch (Exception ex)
            {
                logger.Add($"Play state report failed: {ex}");
            }
        }

        private void Sign(WebRequest request)
        {
            string url = request.Url ?? string.Empty;

            if (!solo_scores.IsMatch(url) && !playlist_scores.IsMatch(url) && !achievements.IsMatch(url) && !users.IsMatch(url))
                return;

            native?.Sign(SignTarget.WebRequest, request);
        }

        private void Sign(HttpConnectionOptions options) => native?.Sign(SignTarget.HubConnection, options);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            native?.Dispose();
        }
    }
}
