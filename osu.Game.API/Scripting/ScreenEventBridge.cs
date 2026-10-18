// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Screens;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Bridges osu!'s screen navigation into the scripting event bus, emitting the <c>"screen"</c> event
    /// whenever the current screen changes.
    /// </summary>
    internal class ScreenEventBridge : IDisposable
    {
        private readonly ScriptRunner runner;
        private readonly ScreenStack screenStack;

        private string? lastScreen;

        public ScreenEventBridge(ScriptRunner runner, ScreenStack screenStack)
        {
            this.runner = runner;
            this.screenStack = screenStack;

            screenStack.ScreenPushed += onScreenChanged;
            screenStack.ScreenExited += onScreenChanged;

            emit(screenStack.CurrentScreen);
        }

        private void onScreenChanged(IScreen lastScreen, IScreen newScreen) => emit(newScreen);

        private void emit(IScreen? screen)
        {
            string? name = screen?.GetType().Name;

            if (name == lastScreen)
                return;

            lastScreen = name;

            if (name != null)
                runner.Emit("screen", new object?[] { name });
        }

        public void Dispose()
        {
            screenStack.ScreenPushed -= onScreenChanged;
            screenStack.ScreenExited -= onScreenChanged;
        }
    }
}
