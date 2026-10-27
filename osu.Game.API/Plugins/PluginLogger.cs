// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Per-plugin logger writing to the framework log under the <c>plugins/&lt;id&gt;</c> category.
    /// </summary>
    public class PluginLogger
    {
        private readonly Logger logger;

        public PluginLogger(string pluginId) => logger = Logger.GetLogger($"plugins/{pluginId}");

        public void Info(string message) => logger.Add(message, LogLevel.Verbose);

        public void Warn(string message) => logger.Add(message, LogLevel.Important);

        public void Error(string message) => logger.Add(message, LogLevel.Error);

        public void Debug(string message) => logger.Add(message, LogLevel.Debug);
    }
}
