// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Game.API.Plugins;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Exposes the scripting manager owned by the built-in "system" plugin. The manager's lifecycle,
    /// storage, settings and screen events are handled by <see cref="PluginScriptHost"/>; this loader only
    /// waits for the system plugin and surfaces its manager to UI consumers.
    /// </summary>
    /// <remarks>
    /// Constructed from <c>osu.Desktop/Program.cs</c> around <c>host.Run(game)</c>. It may be constructed
    /// before or after the <see cref="PluginBootstrap"/> has initialised, and never stops scripts itself;
    /// <see cref="PluginBootstrap.Shutdown"/> owns that lifecycle.
    /// </remarks>
    public class ScriptingLoader : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("scripts");

        private readonly PluginBootstrap pluginBootstrap;
        private bool disposed;

        /// <summary>The system plugin's scripting manager, or <c>null</c> if scripting is unavailable.</summary>
        public ScriptManager? Manager { get; private set; }

        /// <summary>Raised once the system plugin's scripting manager is available.</summary>
        public event Action<ScriptManager>? Initialised;

        public ScriptingLoader(PluginBootstrap pluginBootstrap)
        {
            this.pluginBootstrap = pluginBootstrap;

            pluginBootstrap.SystemPluginLoaded += onSystemPluginLoaded;
            pluginBootstrap.Initialised += onBootstrapInitialised;

            if (pluginBootstrap.IsInitialised)
                onBootstrapInitialised();
        }

        private void onSystemPluginLoaded(LoadedPlugin entry) => initialise(entry);

        private void onBootstrapInitialised()
        {
            if (pluginBootstrap.SystemPlugin is { } entry)
                initialise(entry);
            else
                logger.Add("System plugin is not loaded; JavaScript scripting is unavailable.");
        }

        private void initialise(LoadedPlugin entry)
        {
            if (Manager != null)
                return;

            Manager = entry.Scripts?.Manager;

            if (Manager == null)
                logger.Add("System plugin has no scripting host");
            else
                Initialised?.Invoke(Manager);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            pluginBootstrap.SystemPluginLoaded -= onSystemPluginLoaded;
            pluginBootstrap.Initialised -= onBootstrapInitialised;
        }
    }
}
