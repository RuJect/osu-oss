// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Per-plugin text logger persisting to <c>logs/plugin.log</c> inside the plugin's data directory.
    /// Without a storage (no "data" permission) the logger records nothing.
    /// </summary>
    public class PluginLogger
    {
        private const string log_file = "logs/plugin.log";
        private const long max_log_bytes = 1_000_000;
        private const int max_backups = 3;

        private readonly string pluginId;
        private readonly StorageService? storage;

        public PluginLogger(string pluginId, StorageService? storage)
        {
            this.pluginId = pluginId;
            this.storage = storage;

            Info("plugin started logging");
        }

        public void Info(string message) => WriteLine("INFO", message);

        public void Warn(string message) => WriteLine("WARN", message);

        public void Error(string message) => WriteLine("ERROR", message);

        public void Debug(string message) => WriteLine("DEBUG", message);

        private void WriteLine(string level, string line)
        {
            if (storage == null)
                return;

            rotate();

            storage.Append(log_file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{pluginId}] [{level}] {line}\n");
        }

        /// <summary>Shifts the log aside (plugin.1.log ... plugin.3.log) once it outgrows <see cref="max_log_bytes"/>.</summary>
        private void rotate()
        {
            string? current = storage!.Read(log_file);

            if (current == null || current.Length <= max_log_bytes)
                return;

            for (int i = max_backups; i >= 2; i--)
                shift($"logs/plugin.{i - 1}.log", $"logs/plugin.{i}.log");

            storage.Write("logs/plugin.1.log", current);
            storage.Write(log_file, string.Empty);
        }

        /// <summary>Replaces <paramref name="to"/> with the contents of <paramref name="from"/>, if it exists.</summary>
        private void shift(string from, string to)
        {
            string? content = storage!.Read(from);

            if (content != null)
                storage.Write(to, content);
        }
    }
}
