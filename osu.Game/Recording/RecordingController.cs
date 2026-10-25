// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Recording
{
    /// <summary>
    /// Drives a recording: captures frames from the host and pipes them to ffmpeg.
    /// </summary>
    public class RecordingController : IDisposable
    {
        private readonly RecordingOptions options;
        private readonly GameHost host;
        private readonly string outputDirectory;

        private FFmpegProcessEncoder? encoder;

        public int ExitCode { get; private set; }

        public RecordingController(RecordingOptions options, GameHost host, string outputDirectory)
        {
            this.options = options;
            this.host = host;
            this.outputDirectory = outputDirectory;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!options.DryRun)
                {
                    string outputPath = options.OutputPath ?? options.GetDefaultOutputPath(outputDirectory);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

                    encoder = new FFmpegProcessEncoder(options, outputPath);
                    await encoder.InitializeAsync().ConfigureAwait(false);
                }

                var frameInterval = TimeSpan.FromSeconds(1.0 / options.Fps);
                var elapsed = Stopwatch.StartNew();

                while (!cancellationToken.IsCancellationRequested
                       && (options.MaxDurationSeconds <= 0 || elapsed.Elapsed.TotalSeconds < options.MaxDurationSeconds))
                {
                    TimeSpan frameStart = elapsed.Elapsed;

                    using (Image<Rgba32>? frame = await captureFrameAsync().ConfigureAwait(false))
                    {
                        if (frame != null && encoder != null)
                            await encoder.WriteVideoFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                    }

                    TimeSpan remaining = frameInterval - (elapsed.Elapsed - frameStart);

                    if (remaining > TimeSpan.Zero)
                        await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }

                if (encoder != null)
                {
                    await encoder.FinalizeAsync().ConfigureAwait(false);
                    Logger.Log($"Recording saved to {encoder.OutputPath}", LoggingTarget.Runtime, LogLevel.Important);
                }

                ExitCode = 0;
            }
            catch (OperationCanceledException)
            {
                ExitCode = 0;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Recording failed");
                ExitCode = 1;
            }
            finally
            {
                if (encoder != null)
                    await encoder.DisposeAsync().ConfigureAwait(false);

                encoder = null;
            }
        }

        private async Task<Image<Rgba32>?> captureFrameAsync()
        {
            try
            {
                Image<Rgba32> image = await host.TakeScreenshotAsync().ConfigureAwait(false);

                if (image.Width == options.Width && image.Height == options.Height)
                    return image;

                try
                {
                    return image.Clone(ctx => ctx.Resize(options.Width, options.Height));
                }
                finally
                {
                    image.Dispose();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to capture frame");
                return null;
            }
        }

        public void Dispose()
        {
            encoder?.Dispose();
            encoder = null;
        }
    }
}
