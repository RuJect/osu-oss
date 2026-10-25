// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osu.Game.Recording;

namespace osu.Game.Recording
{
    /// <summary>
    /// Encodes video using FFmpeg process with stdin pipe.
    /// </summary>
    public class FFmpegProcessEncoder : IAsyncDisposable, IDisposable
    {
        private readonly RecordingOptions options;
        private readonly string outputPath;
        private Process? ffmpegProcess;
        private Stream? stdinStream;
        private readonly SemaphoreSlim writeSemaphore = new(1, 1);

        public string OutputPath => outputPath;

        public FFmpegProcessEncoder(RecordingOptions options, string outputPath)
        {
            this.options = options;
            this.outputPath = outputPath;
        }

        /// <summary>
        /// Initializes and starts the FFmpeg process.
        /// </summary>
        public async Task InitializeAsync()
        {
            // Check if ffmpeg exists
            if (!IsFFmpegAvailable())
            {
                throw new InvalidOperationException(
                    "FFmpeg not found. Please install FFmpeg and ensure it's in PATH, " +
                    "or place ffmpeg.exe next to the game executable (Windows).");
            }

            // Build FFmpeg arguments
            string arguments = BuildFFmpegArguments();

            Logger.Log($"Starting FFmpeg: ffmpeg {arguments}", LoggingTarget.Runtime, LogLevel.Important);

            ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            // Set up error/output logging
            ffmpegProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Logger.Log($"FFmpeg: {e.Data}", LoggingTarget.Runtime, LogLevel.Debug);
            };
            ffmpegProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Logger.Log($"FFmpeg: {e.Data}", LoggingTarget.Runtime, LogLevel.Debug);
            };

            ffmpegProcess.Start();
            ffmpegProcess.BeginErrorReadLine();
            ffmpegProcess.BeginOutputReadLine();

            stdinStream = ffmpegProcess.StandardInput.BaseStream;
        }

        private string BuildFFmpegArguments()
        {
            string codec = options.Codec switch
            {
                "h264" => "libx264",
                "hevc" or "h265" => "libx265",
                "vp9" => "libvpx-vp9",
                "av1" => "libaom-av1",
                _ => "libx264"
            };

            string preset = options.Preset;

            // Video comes from stdin. Audio is a silent placeholder until a real audio tap exists.
            // -shortest is required, otherwise the endless anullsrc input keeps ffmpeg running forever.
            return $"-y " +
                   $"-f rawvideo -pix_fmt rgba -s {options.Width}x{options.Height} -r {options.Fps} -i pipe:0 " +
                   $"-f lavfi -i anullsrc=channel_layout=stereo:sample_rate=48000 " +
                   $"-c:v {codec} -preset {preset} -crf 20 -pix_fmt yuv420p " +
                   $"-c:a aac -b:a 128k " +
                   $"-shortest " +
                   $"-movflags +faststart " +
                   $"\"{outputPath}\"";
        }

        private bool IsFFmpegAvailable()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                });
                if (process == null || !process.WaitForExit(5000))
                    return false;

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a video frame to the encoder.
        /// </summary>
        public async Task WriteVideoFrameAsync(Image<Rgba32> frame, CancellationToken cancellationToken = default)
        {
            if (stdinStream == null)
                return;

            await writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Convert ImageSharp image to raw BGRA bytes
                byte[] frameData = new byte[frame.Width * frame.Height * 4];
                frame.CopyPixelDataTo(frameData);

                await stdinStream.WriteAsync(frameData, cancellationToken).ConfigureAwait(false);
                await stdinStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writeSemaphore.Release();
            }
        }

        /// <summary>
        /// Writes audio samples to the encoder.
        /// Note: With current FFmpeg setup, audio is generated by anullsrc filter.
        /// This method is kept for future extension when we implement real audio capture.
        /// </summary>
        public Task WriteAudioSamplesAsync(float[] samples, CancellationToken cancellationToken = default)
        {
            // Currently not used - audio is generated by FFmpeg's anullsrc filter
            // TODO: Implement proper audio piping when audio capture is ready
            return Task.CompletedTask;
        }

        /// <summary>
        /// Finalizes the encoding and waits for FFmpeg to finish.
        /// </summary>
        public async Task FinalizeAsync()
        {
            // Close stdin to signal EOF
            if (stdinStream != null)
            {
                await stdinStream.FlushAsync().ConfigureAwait(false);
                stdinStream.Close();
                stdinStream = null;
            }

            // Wait for FFmpeg to finish
            if (ffmpegProcess != null && !ffmpegProcess.HasExited)
            {
                await Task.Run(() => ffmpegProcess.WaitForExit(30000)).ConfigureAwait(false);

                if (!ffmpegProcess.HasExited)
                {
                    Logger.Log("FFmpeg did not exit in time, killing...", LoggingTarget.Runtime, LogLevel.Important);
                    ffmpegProcess.Kill(true);
                    await Task.Run(() => ffmpegProcess.WaitForExit(5000)).ConfigureAwait(false);
                }

                if (ffmpegProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException($"FFmpeg exited with code {ffmpegProcess.ExitCode}");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (ffmpegProcess != null && !ffmpegProcess.HasExited)
                {
                    ffmpegProcess.Kill(true);
                    await Task.Run(() => ffmpegProcess.WaitForExit(5000)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error disposing FFmpeg encoder");
            }
            finally
            {
                ffmpegProcess?.Dispose();
                ffmpegProcess = null;
            }
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}