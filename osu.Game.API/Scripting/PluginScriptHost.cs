// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Screens;
using osu.Game.API.Plugins;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Owns the scripting runtime for a single plugin. The built-in system plugin hosts the global
    /// scripts (including <c>on_launch.js</c>/<c>on_exit.js</c>); every other plugin with a JavaScript
    /// entry point gets a storage- and permission-scoped instance.
    /// </summary>
    public sealed class PluginScriptHost : IDisposable
    {
        private readonly PluginContext context;
        private readonly ScriptStorage storage;
        private readonly bool global;
        private readonly string? entryPoint;

        private ScreenEventBridge? screenBridge;
        private ScriptingSettings settings;
        private bool started;
        private bool shutDown;

        /// <summary>The scripting manager owned by this plugin.</summary>
        public ScriptManager Manager { get; }

        public PluginScriptHost(PluginContext context, ScriptStorage storage, ScreenStack? screenStack, bool global,
                                string? entryPoint, ScriptingSettings settings)
        {
            this.context = context;
            this.storage = storage;
            this.global = global;
            this.entryPoint = entryPoint;
            this.settings = settings;

            Manager = new ScriptManager(context);
            Manager.AttachStorage(storage);

            if (screenStack != null)
                screenBridge = new ScreenEventBridge(Manager.Events, screenStack);

            applySettings(settings);
        }

        /// <summary>Applies updated global preferences, starting the plugin's script if it has not run yet.</summary>
        public void ApplySettings(ScriptingSettings newSettings)
        {
            settings = newSettings;
            applySettings(settings);

            if (!settings.Enabled)
                Manager.Runner.StopAll();
            else if (!started)
                Start();
        }

        /// <summary>Starts the plugin's script, if scripting is enabled. Subsequent calls are no-ops.</summary>
        public void Start()
        {
            if (started)
                return;

            if (global)
                storage.EnsureInitialised();

            if (!settings.Enabled)
                return;

            started = true;

            if (global)
                runFile(ScriptStorage.LAUNCH_SCRIPT);
            else if (entryPoint != null)
                runFile(entryPoint);
        }

        /// <summary>Advances the plugin's running scripts. Called on the update thread.</summary>
        public void Tick(double currentTime)
        {
            if (!shutDown)
                Manager.Runner.Tick(currentTime);
        }

        /// <summary>Runs the exit script (if present), stops every script and detaches screen events.</summary>
        public void Shutdown()
        {
            if (shutDown)
                return;

            shutDown = true;

            if (settings.Enabled && storage.Exists(ScriptStorage.EXIT_SCRIPT))
                runFile(ScriptStorage.EXIT_SCRIPT);

            Manager.Runner.StopAll();

            screenBridge?.Dispose();
            screenBridge = null;
        }

        public void Dispose() => Shutdown();

        private void applySettings(ScriptingSettings current)
        {
            Manager.Runner.Enabled = current.Enabled;
            Manager.AllowClrAccess = current.AllowClrAccess && context.Manifest.Permissions.CanUseJs;
        }

        private void runFile(string fileName)
        {
            try
            {
                Manager.RunFile(fileName);
            }
            catch (Exception e)
            {
                context.Logger.Error($"{fileName} failed: {e.Message}");
            }
        }
    }
}
