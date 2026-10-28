// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Auth.Provisioning
{
    /// <summary>
    /// Fetches the official (measured) assemblies in the background so that a
    /// subsequent launch can enable transplanted signing. Signing stays disabled
    /// for the current session when the cache is incomplete.
    /// </summary>
    internal sealed class OfficialFilesDownloader : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool disposed;

        private OfficialFilesDownloader(GameHost host, OsuGameBase game, OfficialFileCache files)
        {
            if (game.IsLoaded)
                run(host, game, files);
            else
                game.OnLoadComplete += _ => run(host, game, files);
        }

        public static OfficialFilesDownloader Start(GameHost host, OsuGameBase game, OfficialFileCache files) =>
            new OfficialFilesDownloader(host, game, files);

        private void run(GameHost host, OsuGameBase game, OfficialFileCache files)
        {
            if (cancellation.IsCancellationRequested)
                return;

            INotificationOverlay? notifications = null;

            try
            {
                notifications = game.Dependencies.Get<INotificationOverlay>();
            }
            catch (Exception e)
            {
                logger.Add($"Could not retrieve notification overlay: {e.Message}");
            }

            ProgressNotification? notification = null;

            if (notifications != null)
            {
                notification = new ProgressNotification
                {
                    Text = "Downloading official authentication files…",
                    State = ProgressNotificationState.Active,
                    CompletionText = "Official authentication files downloaded. Restart the game to enable signing.",
                    CancelRequested = () =>
                    {
                        cancellation.Cancel();
                        return true;
                    },
                };

                notifications.Post(notification);
            }

            var progress = new Progress<double>(value => host.UpdateThread.Scheduler.Add(() =>
            {
                if (notification != null)
                    notification.Progress = (float)value;
            }));

            Task.Run(async () =>
            {
                try
                {
                    await files.DownloadAsync(progress, cancellation.Token).ConfigureAwait(false);

                    logger.Add("Official auth files downloaded; signing will be enabled on next launch.");

                    host.UpdateThread.Scheduler.Add(() =>
                    {
                        if (notification != null)
                            notification.State = ProgressNotificationState.Completed;
                    });
                }
                catch (OperationCanceledException)
                {
                    logger.Add("Official auth file download cancelled.");

                    host.UpdateThread.Scheduler.Add(() =>
                    {
                        if (notification != null)
                            notification.State = ProgressNotificationState.Cancelled;
                    });
                }
                catch (Exception e)
                {
                    logger.Add($"Failed to download official auth files: {e}");

                    host.UpdateThread.Scheduler.Add(() =>
                    {
                        notification?.CompleteSilently();
                        notifications?.Post(new SimpleNotification
                        {
                            Text = "Failed to download official authentication files. Signing will remain disabled.",
                        });
                    });
                }
            }, cancellation.Token);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }
}
