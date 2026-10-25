// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Recording;

namespace osu.Desktop.Recording
{
    /// <summary>
    /// Host used to run the game while a recording is in progress.
    /// </summary>
    public class RecordingHost : IDisposable
    {
        private readonly RecordingOptions options;
        private readonly string dataPath;
        private readonly DesktopGameHost host;
        private readonly TaskCompletionSource gameLoaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingHost(RecordingOptions options, string dataPath)
        {
            this.options = options;
            this.dataPath = dataPath;

            host = Host.GetSuitableDesktopHost($"osu-recording-{Guid.NewGuid()}", new HostOptions
            {
                IPCPipeName = null,
                FriendlyGameName = OsuGameBase.GAME_NAME,
            });
        }

        /// <summary>
        /// Runs the game and records it. Blocks until the recording (or the game) finishes.
        /// </summary>
        public int RunAndRecord()
        {
            var game = new RecordingGame(options, () => gameLoaded.TrySetResult())
            {
                DataPath = dataPath,
            };

            var controller = new RecordingController(options, host, dataPath);

            _ = Task.Run(async () =>
            {
                await gameLoaded.Task.ConfigureAwait(false);
                await controller.RunAsync(CancellationToken.None).ConfigureAwait(false);
                host.Exit();
            });

            try
            {
                host.Run(game);
            }
            finally
            {
                controller.Dispose();
            }

            return controller.ExitCode;
        }

        public void Dispose() => host.Dispose();
    }
}
