// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.API.Events;
using osu.Game.API.Services;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.Overlays;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Entry point of the scripting subsystem. Owns the <see cref="ScriptManager"/>, resolves the scripts
    /// storage after the game loads and runs the magic <c>on_launch.js</c>/<c>on_exit.js</c> scripts.
    /// </summary>
    /// <remarks>
    /// Constructed from <c>osu.Desktop/Program.cs</c> around <c>host.Run(game)</c>, mirroring the auth module's
    /// <c>Loader</c>. Disposal runs after the update thread has stopped, so <c>on_exit.js</c> must not touch drawables.
    /// </remarks>
    public class ScriptingLoader : IDisposable
    {
        private static readonly Logger logger = Logger.GetLogger("scripts");

        private readonly OsuGame game;
        private ScreenEventBridge? screenBridge;
        private bool initialised;
        private bool disposed;

        // GetBindable returns a weakly-bound copy; these must stay referenced for the bindings to fire.
        private Bindable<bool>? scriptingEnabledBindable;
        private Bindable<bool>? allowClrAccessBindable;

        public ScriptManager Manager { get; }

        /// <summary>
        /// Raised (on the update thread) once storage has been resolved and definitions loaded, before
        /// <c>on_launch.js</c> runs. UI composition hangs off this.
        /// </summary>
        public event Action<ScriptManager>? Initialised;

        public ScriptingLoader(GameHost host, OsuGame game, OsuEventBus eventBus)
        {
            this.game = game;

            Manager = new ScriptManager(host, game, eventBus);

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
            if (initialised)
                return;

            initialised = true;

            var runtime = new ScriptRuntimeComponent(Manager.Runner);
            game.Add(runtime);

            Manager.Services.InputManager = runtime.FindInputManager();

            if (game.ScreenStack != null)
                screenBridge = new ScreenEventBridge(Manager.Events, game.ScreenStack);

            try
            {
                Storage storage = game.Dependencies.Get<Storage>();
                var scriptStorage = new ScriptStorage(SpecialStorage.GetScripts(storage));
                scriptStorage.EnsureInitialised();

                Manager.AttachStorage(scriptStorage, SpecialStorage.GetScriptData(storage));
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

            Manager.Services.Music = new MusicService(game.Dependencies.Get<MusicController>(), Manager.Services.Host);
            Manager.Services.Config = new ConfigService(game.Dependencies.Get<OsuConfigManager>());
            Manager.Services.Notifications = new NotificationService(game.Dependencies.Get<INotificationOverlay>());

            Initialised?.Invoke(Manager);

            runLaunchScript();
        }

        private void runLaunchScript()
        {
            if (!Manager.Runner.Enabled)
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
            if (!Manager.Runner.Enabled)
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
            Manager.Runner.StopAll();
            screenBridge?.Dispose();
            screenBridge = null;
        }
    }
}
