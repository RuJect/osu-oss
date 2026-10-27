// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Per-plugin logger writing to the framework log and to a rotating <c>plugin.log</c> file under the
    /// supplied logs storage.
    /// </summary>
    public class PluginLogger
    {
        private const string log_file = "plugin.log";
        private const int max_log_length = 1_000_000;
        private const int backup_count = 3;

        private readonly Logger logger;
        private readonly FileStore? fileStore;
        private readonly object fileLock = new object();

        /// <summary>Creates a logger that writes to the framework log and to <paramref name="logsStorage"/>.</summary>
        public PluginLogger(string pluginId, Storage logsStorage)
            : this(pluginId, new FileStore(logsStorage))
        {
        }

        /// <summary>Creates a logger that writes only to the framework log.</summary>
        public PluginLogger(string pluginId)
            : this(pluginId, (FileStore?)null)
        {
        }

        private PluginLogger(string pluginId, FileStore? fileStore)
        {
            this.fileStore = fileStore;
            logger = Logger.GetLogger($"plugins/{pluginId}");
        }

        public void Info(string message) => log(LogLevel.Verbose, "INFO", message);

        public void Warn(string message) => log(LogLevel.Important, "WARN", message);

        public void Error(string message) => log(LogLevel.Error, "ERROR", message);

        public void Debug(string message) => log(LogLevel.Debug, "DEBUG", message);

        private void log(LogLevel level, string levelName, string message)
        {
            logger.Add(message, level);

            FileStore? store = fileStore;

            if (store == null)
                return;

            lock (fileLock)
            {
                try
                {
                    string? current = store.Exists(log_file) ? store.Read(log_file) : null;

                    if (current != null && current.Length > max_log_length)
                        rotate(store);

                    store.Append(log_file, $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}] [{levelName}] {message}\n");
                }
                catch (Exception e)
                {
                    logger.Add($"failed to write plugin log file: {e.Message}", LogLevel.Error);
                }
            }
        }

        private static void rotate(FileStore store)
        {
            for (int i = backup_count; i >= 1; i--)
            {
                string source = backupName(i - 1);
                string target = backupName(i);

                if (store.Exists(target))
                    store.Delete(target);

                if (!store.Exists(source))
                    continue;

                string? content = store.Read(source);

                if (content != null)
                    store.Write(target, content);

                store.Delete(source);
            }

            store.Write(log_file, string.Empty);
        }

        private static string backupName(int index)
            => index == 0 ? log_file : $"plugin.{index}.log";
    }
}
