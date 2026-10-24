// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Framework.Input;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// A zero-sized component whose only purpose is to drive <see cref="ScriptRunner.Tick"/> on the update thread.
    /// </summary>
    internal partial class ScriptRuntimeComponent : Container
    {
        private readonly ScriptRunner runner;

        public ScriptRuntimeComponent(ScriptRunner runner)
        {
            this.runner = runner;
        }

        public InputManager? FindInputManager() => GetContainingInputManager();

        protected override void Update()
        {
            base.Update();
            runner.Tick(Clock.CurrentTime);
        }
    }
}
