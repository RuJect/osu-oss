// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.IO;

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

        public ScriptManager Manager { get; }

        /// <summary>
        /// Raised (on the update thread) once storage has been resolved and definitions loaded, before
        /// <c>on_launch.js</c> runs. UI composition hangs off this.
        /// </summary>
        public event Action<ScriptManager>? Initialised;

        public ScriptingLoader(GameHost host, OsuGame game)
        {
            this.game = game;

            Manager = new ScriptManager(host, game);

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

            game.Add(new ScriptRuntimeComponent(Manager.Runner));

            if (game.ScreenStack != null)
                screenBridge = new ScreenEventBridge(Manager.Runner, game.ScreenStack);

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

            Initialised?.Invoke(Manager);

            runLaunchScript();
        }

        private void runLaunchScript()
        {
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
