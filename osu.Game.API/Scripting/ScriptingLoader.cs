// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.API.Plugins;
using osu.Game.API.Services;
using osu.Game.Configuration;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Entry point of the scripting subsystem. Runs the <see cref="ScriptManager"/> in the context of the
    /// built-in "system" plugin, resolves the scripts storage after the game loads and runs the magic
    /// <c>on_launch.js</c>/<c>on_exit.js</c> scripts.
    /// </summary>
    /// <remarks>
    /// Constructed from <c>osu.Desktop/Program.cs</c> around <c>host.Run(game)</c>, after the
    /// <see cref="PluginBootstrap"/> which loads the system plugin on game load. Disposal runs after the
    /// update thread has stopped, so <c>on_exit.js</c> must not touch drawables.
    /// </remarks>
    public class ScriptingLoader : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("scripts");

        private readonly OsuGame game;
        private readonly PluginBootstrap pluginBootstrap;
        private ScreenEventBridge? screenBridge;
        private bool initialised;
        private bool disposed;

        // GetBindable returns a weakly-bound copy; these must stay referenced for the bindings to fire.
        private Bindable<bool>? scriptingEnabledBindable;
        private Bindable<bool>? allowClrAccessBindable;

        /// <summary>The scripting manager, or <c>null</c> if scripting could not be initialised.</summary>
        public ScriptManager? Manager { get; private set; }

        /// <summary>
        /// Raised (on the update thread) once storage has been resolved and definitions loaded, before
        /// <c>on_launch.js</c> runs. UI composition hangs off this.
        /// </summary>
        public event Action<ScriptManager>? Initialised;

        public ScriptingLoader(OsuGame game, PluginBootstrap pluginBootstrap)
        {
            this.game = game;
            this.pluginBootstrap = pluginBootstrap;

            // Deferred until the game has finished loading: adding drawables to (or resolving
            // dependencies from) a not-yet-loaded OsuGame is not valid, and the game's scheduler
            // is not accessible from this assembly.
            if (game.IsLoaded)
                initialise();
            else
                game.OnLoadComplete += _ => initialise();
        }

        private void initialise()
        {
            if (initialised || disposed)
                return;

            initialised = true;

            var systemPlugin = pluginBootstrap.Host?.Loaded.FirstOrDefault(p => p.Manifest.Id == PluginManifest.SYSTEM_ID);

            if (systemPlugin == null)
            {
                logger.Add("System plugin is not loaded; JavaScript scripting is unavailable.");
                return;
            }

            Manager = new ScriptManager(systemPlugin.Context);

            var runtime = new ScriptRuntimeComponent(Manager.Runner);
            game.Add(runtime);

            if (game.ScreenStack != null)
                screenBridge = new ScreenEventBridge(Manager.Events, game.ScreenStack);

            try
            {
                GameServices? gameServices = pluginBootstrap.Services;

                if (gameServices == null)
                {
                    logger.Add("Game services are unavailable; JavaScript scripting is unavailable.");
                    return;
                }

                var scriptStorage = new ScriptStorage(gameServices.ScriptsStorage);
                scriptStorage.EnsureInitialised();

                Manager.AttachStorage(scriptStorage);
            }
            catch (Exception e)
            {
                logger.Add($"Failed to initialise scripting storage: {e.Message}");
            }

            SpecialConfigManager specialConfig = game.Dependencies.Get<SpecialConfigManager>();

            scriptingEnabledBindable = specialConfig.GetBindable<bool>(SpecialSetting.ScriptingEnabled);
            scriptingEnabledBindable.BindValueChanged(e =>
            {
                Manager.Runner.Enabled = e.NewValue;

                if (!e.NewValue)
                    Manager.Runner.StopAll();
            }, true);

            allowClrAccessBindable = specialConfig.GetBindable<bool>(SpecialSetting.AllowClrAccess);
            allowClrAccessBindable.BindValueChanged(e => Manager.AllowClrAccess = e.NewValue, true);

            Initialised?.Invoke(Manager);

            runLaunchScript();
        }

        private void runLaunchScript()
        {
            if (Manager == null || !Manager.Runner.Enabled)
                return;

            try
            {
                Manager.RunFile(ScriptStorage.LAUNCH_SCRIPT);
            }
            catch (Exception e)
            {
                logger.Add($"on_launch.js failed: {e.Message}");
            }
        }

        private void runExitScript()
        {
            if (Manager == null || !Manager.Runner.Enabled)
                return;

            try
            {
                Manager.RunFile(ScriptStorage.EXIT_SCRIPT);
            }
            catch (Exception e)
            {
                logger.Add($"on_exit.js failed: {e.Message}");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            runExitScript();
            Manager?.Runner.StopAll();
            screenBridge?.Dispose();
            screenBridge = null;
        }
    }
}
