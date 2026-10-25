// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.IO;

namespace osu.Game.Recording
{
    /// <summary>
    /// Parsed recording options from command line arguments.
    /// </summary>
    public class RecordingOptions
    {
        public const string ArgumentName = "--recording";
        public const string OutputArgumentName = "--recording-output";
        public const string DurationArgumentName = "--recording-duration";
        public const string DryRunArgumentName = "--recording-dry-run";
        public const string CodecArgumentName = "--recording-codec";
        public const string PresetArgumentName = "--recording-preset";

        /// <summary>
        /// Whether recording mode is enabled.
        /// </summary>
        public bool Enabled { get; private set; }

        /// <summary>
        /// Recording width in pixels.
        /// </summary>
        public int Width { get; private set; } = 1920;

        /// <summary>
        /// Recording height in pixels.
        /// </summary>
        public int Height { get; private set; } = 1080;

        /// <summary>
        /// Target frames per second.
        /// </summary>
        public int Fps { get; private set; } = 60;

        /// <summary>
        /// Output file path. If null, auto-generated in data directory.
        /// </summary>
        public string? OutputPath { get; private set; }

        /// <summary>
        /// Maximum recording duration in seconds. 0 = unlimited.
        /// </summary>
        public int MaxDurationSeconds { get; private set; } = 0;

        /// <summary>
        /// Dry run mode - runs without writing output file (for CI testing).
        /// </summary>
        public bool DryRun { get; private set; }

        /// <summary>
        /// Video codec to use (h264, hevc, vp9, av1).
        /// </summary>
        public string Codec { get; private set; } = "h264";

        /// <summary>
        /// Encoder preset (ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow).
        /// </summary>
        public string Preset { get; private set; } = "fast";

        /// <summary>
        /// Parses recording arguments from command line.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Parsed RecordingOptions, or null if recording not requested.</returns>
        public static RecordingOptions? Parse(string[] args)
        {
            var options = new RecordingOptions();
            bool foundRecordingArg = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith(ArgumentName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(ArgumentName.Length + 1);
                    if (!TryParseResolutionAndFps(value, out int w, out int h, out int fps))
                        throw new ArgumentException($"Invalid {ArgumentName} format. Expected: WIDTHxHEIGHT@FPS (e.g. 1920x1080@60)");

                    options.Width = w;
                    options.Height = h;
                    options.Fps = fps;
                    options.Enabled = true;
                    foundRecordingArg = true;
                }
                else if (arg.StartsWith(OutputArgumentName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    options.OutputPath = arg.Substring(OutputArgumentName.Length + 1);
                }
                else if (arg.StartsWith(DurationArgumentName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(arg.Substring(DurationArgumentName.Length + 1), out int duration) || duration < 0)
                        throw new ArgumentException($"Invalid {DurationArgumentName} value. Must be non-negative integer.");
                    options.MaxDurationSeconds = duration;
                }
                else if (arg.Equals(DryRunArgumentName, StringComparison.OrdinalIgnoreCase))
                {
                    options.DryRun = true;
                }
                else if (arg.StartsWith(CodecArgumentName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    options.Codec = arg.Substring(CodecArgumentName.Length + 1).ToLowerInvariant();
                }
                else if (arg.StartsWith(PresetArgumentName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    options.Preset = arg.Substring(PresetArgumentName.Length + 1).ToLowerInvariant();
                }
            }

            if (!foundRecordingArg)
                return null;

            // Validation
            if (options.Width < 128 || options.Width > 7680)
                throw new ArgumentException($"Width must be between 128 and 7680, got {options.Width}");
            if (options.Height < 128 || options.Height > 4320)
                throw new ArgumentException($"Height must be between 128 and 4320, got {options.Height}");
            if (options.Fps < 1 || options.Fps > 240)
                throw new ArgumentException($"FPS must be between 1 and 240, got {options.Fps}");

            return options;
        }

        private static bool TryParseResolutionAndFps(string input, out int width, out int height, out int fps)
        {
            width = height = fps = 0;

            // Format: WIDTHxHEIGHT@FPS
            int atIndex = input.LastIndexOf('@');
            if (atIndex <= 0)
                return false;

            string resolutionPart = input.Substring(0, atIndex);
            string fpsPart = input.Substring(atIndex + 1);

            if (!int.TryParse(fpsPart, out fps))
                return false;

            int xIndex = resolutionPart.IndexOf('x');
            if (xIndex <= 0 || xIndex >= resolutionPart.Length - 1)
                return false;

            if (!int.TryParse(resolutionPart.Substring(0, xIndex), out width))
                return false;
            if (!int.TryParse(resolutionPart.Substring(xIndex + 1), out height))
                return false;

            return true;
        }

        /// <summary>
        /// Generates default output filename with timestamp.
        /// </summary>
        public string GetDefaultOutputPath(string dataPath)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"recording_{timestamp}.mp4";
            return Path.Combine(dataPath, "recordings", filename);
        }

        public override string ToString()
        {
            return $"RecordingOptions: {Width}x{Height}@{Fps}fps, Codec={Codec}, Preset={Preset}, Output={OutputPath ?? "auto"}, DryRun={DryRun}, MaxDuration={MaxDurationSeconds}s";
        }
    }
}