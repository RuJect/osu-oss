// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Profile.Sections.Beatmaps
{
    /// <summary>
    /// A button which downloads all beatmaps favourited by the profile's user, skipping any which are already available locally.
    /// </summary>
    public partial class FavouriteBeatmapsDownloadButton : RoundedButton
    {
        /// <summary>
        /// The number of times a failed (non-rate-limited) download is retried before being given up on.
        /// </summary>
        private const int download_retry_count = 3;

        /// <summary>
        /// The delay between retries of a failed (non-rate-limited) download.
        /// </summary>
        private static readonly TimeSpan general_retry_delay = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The delay before retrying a download which was rejected due to rate limiting.
        /// </summary>
        private static readonly TimeSpan rate_limit_delay = TimeSpan.FromSeconds(30);

        private const int page_size = 100;

        private const float button_height = 30;
        private const float bottom_padding = 10;

        private readonly Bindable<UserProfileData?> user = new Bindable<UserProfileData?>();

        private FavouriteBeatmapModelDownloader downloader = null!;
        private Bindable<bool> preferNoVideo = null!;
        private Bindable<bool> showDownloadButton = null!;

        private CancellationTokenSource? cancellationSource;
        private bool running;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public FavouriteBeatmapsDownloadButton(Bindable<UserProfileData?> user)
        {
            this.user.BindTo(user);

            RelativeSizeAxes = Axes.X;
            Alpha = 0;
            Text = "Download all favourites";
            Action = startDownload;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, BeatmapManager beatmapManager, SpecialConfigManager specialConfig)
        {
            preferNoVideo = config.GetBindable<bool>(OsuSetting.PreferNoVideo);
            showDownloadButton = specialConfig.GetBindable<bool>(SpecialSetting.ShowFavouriteBeatmapsDownloadButton);
            downloader = new FavouriteBeatmapModelDownloader(beatmapManager, api);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            user.BindValueChanged(_ => updateState(), true);
            showDownloadButton.BindValueChanged(_ => updateState());
        }

        private void updateState()
        {
            bool hasFavourites = (user.Value?.User.FavouriteBeatmapsetCount ?? 0) > 0;
            bool visible = showDownloadButton.Value && hasFavourites;

            Enabled.Value = visible && !running;
            this.FadeTo(visible ? 1 : 0, 200, Easing.OutQuint);

            Height = visible ? button_height : 0;
            Margin = new MarginPadding { Bottom = visible ? bottom_padding : 0 };
        }

        private void startDownload()
        {
            if (running || user.Value == null)
                return;

            running = true;
            Enabled.Value = false;

            cancellationSource = new CancellationTokenSource();
            CancellationToken token = cancellationSource.Token;

            var notification = new ProgressNotification
            {
                Text = "Retrieving favourite beatmaps...",
                State = ProgressNotificationState.Active,
                CancelRequested = () =>
                {
                    cancellationSource?.Cancel();
                    return true;
                }
            };

            notifications.Post(notification);

            Task.Run(() => runDownloadAsync(user.Value.User.Id, notification, token));
        }

        private async Task runDownloadAsync(long userId, ProgressNotification notification, CancellationToken token)
        {
            int downloaded = 0;
            int failed = 0;

            try
            {
                List<APIBeatmapSet> favourites = await retrieveFavouritesAsync(userId, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    return;

                HashSet<int> localIds = getLocallyAvailableOnlineIds();

                List<APIBeatmapSet> queue = favourites.Where(s => !localIds.Contains(s.OnlineID)).ToList();

                notification.Progress = 0;

                for (int i = 0; i < queue.Count; i++)
                {
                    if (token.IsCancellationRequested)
                        return;

                    notification.Text = $"Downloading favourite beatmaps ({i + 1}/{queue.Count})";

                    Exception? error = await downloadWithRetryAsync(queue[i], token).ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                        return;

                    if (error != null)
                        failed++;
                    else
                        downloaded++;

                    notification.Progress = (float)(i + 1) / queue.Count;
                }

                if (queue.Count == 0)
                    notification.CompletionText = "All favourite beatmaps are already available locally.";
                else if (failed > 0)
                    notification.CompletionText = $"Downloaded {downloaded} favourite beatmaps ({failed} failed).";
                else
                    notification.CompletionText = $"Downloaded {downloaded} favourite beatmaps!";

                notification.State = ProgressNotificationState.Completed;
            }
            catch (OperationCanceledException)
            {
                // cancelled by the user.
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to download favourite beatmaps.");
                notification.CompletionText = "Failed to download favourite beatmaps.";
                notification.State = ProgressNotificationState.Cancelled;
            }
            finally
            {
                if (token.IsCancellationRequested)
                    notification.State = ProgressNotificationState.Cancelled;

                Schedule(() =>
                {
                    running = false;
                    updateState();
                });
            }
        }

        private async Task<List<APIBeatmapSet>> retrieveFavouritesAsync(long userId, CancellationToken token)
        {
            var result = new List<APIBeatmapSet>();
            int offset = 0;

            while (!token.IsCancellationRequested)
            {
                var request = new GetUserBeatmapsRequest(userId, BeatmapSetType.Favourite, new PaginationParameters(offset, page_size));

                Exception? error = await performWithRetryAsync(request, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    return result;

                if (error != null)
                    throw error;

                List<APIBeatmapSet> page = request.Response ?? new List<APIBeatmapSet>();
                result.AddRange(page);

                if (page.Count < page_size)
                    break;

                offset += page_size;
            }

            return result;
        }

        private async Task<Exception?> downloadWithRetryAsync(APIBeatmapSet set, CancellationToken token)
        {
            Exception? lastError = null;
            int attempts = 0;

            while (!token.IsCancellationRequested)
            {
                Exception? error = await downloadOnceAsync(set, token).ConfigureAwait(false);

                if (error == null)
                    return null;

                lastError = error;

                if (isRateLimited(error))
                {
                    // wait a fixed period and retry the same beatmap indefinitely, until cancelled.
                    await Task.Delay(rate_limit_delay, token).ConfigureAwait(false);
                    continue;
                }

                attempts++;

                if (attempts >= download_retry_count)
                    return lastError;

                await Task.Delay(general_retry_delay, token).ConfigureAwait(false);
            }

            return lastError;
        }

        private async Task<Exception?> downloadOnceAsync(IBeatmapSetInfo set, CancellationToken token)
        {
            var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            ArchiveDownloadRequest<IBeatmapSetInfo>? request = null;

            void onRequestCreated(ArchiveDownloadRequest<IBeatmapSetInfo> created)
            {
                downloader.RequestCreated -= onRequestCreated;
                request = created;

                created.Success += _ => completion.TrySetResult(null);
                created.Failure += e => completion.TrySetResult(e);
            }

            downloader.RequestCreated += onRequestCreated;

            if (!downloader.Download(set, preferNoVideo.Value))
                completion.TrySetResult(new InvalidOperationException("Download could not be started."));

            using (token.Register(() => request?.Cancel()))
            {
                Exception? error = await completion.Task.ConfigureAwait(false);
                return error;
            }
        }

        private async Task<Exception?> performWithRetryAsync(APIRequest request, CancellationToken token)
        {
            Exception? lastError = null;
            int attempts = 0;

            while (!token.IsCancellationRequested)
            {
                var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

                request.Success += () => completion.TrySetResult(null);
                request.Failure += e => completion.TrySetResult(e);

                using (token.Register(() => request.Cancel()))
                {
                    await api.PerformAsync(request).ConfigureAwait(false);

                    Exception? error = await completion.Task.ConfigureAwait(false);

                    if (error == null)
                        return null;

                    lastError = error;
                }

                if (token.IsCancellationRequested)
                    return lastError;

                if (isRateLimited(lastError))
                {
                    await Task.Delay(rate_limit_delay, token).ConfigureAwait(false);
                    continue;
                }

                attempts++;

                if (attempts >= download_retry_count)
                    return lastError;

                await Task.Delay(general_retry_delay, token).ConfigureAwait(false);
            }

            return lastError;
        }

        private HashSet<int> getLocallyAvailableOnlineIds()
        {
            return realm.Run(r => r.All<BeatmapSetInfo>()
                                  .Where(s => !s.DeletePending)
                                  .AsEnumerable()
                                  .Select(s => s.OnlineID)
                                  .ToHashSet());
        }

        private static bool isRateLimited(Exception error)
        {
            if (error is APIException apiException)
                return apiException.StatusCode == HttpStatusCode.TooManyRequests;

            return error.Message == HttpStatusCode.TooManyRequests.ToString();
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationSource?.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class FavouriteBeatmapModelDownloader : BeatmapModelDownloader
        {
            /// <summary>
            /// Raised immediately before the download request for a beatmap is performed.
            /// </summary>
            public event Action<ArchiveDownloadRequest<IBeatmapSetInfo>>? RequestCreated;

            public FavouriteBeatmapModelDownloader(IModelImporter<BeatmapSetInfo> beatmapImporter, IAPIProvider api)
                : base(beatmapImporter, api)
            {
            }

            protected override ArchiveDownloadRequest<IBeatmapSetInfo> CreateDownloadRequest(IBeatmapSetInfo set, bool minimiseDownloadSize)
            {
                var request = base.CreateDownloadRequest(set, minimiseDownloadSize);
                RequestCreated?.Invoke(request);
                return request;
            }
        }
    }
}
