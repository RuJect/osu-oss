// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Input.States;
using osuTK;
using osuTK.Input;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Applies synthetic cursor and keyboard input to the game's input state.
    /// </summary>
    internal class ScriptInput
    {
        private readonly InputState state;
        private readonly IInputStateChangeHandler handler;

        public ScriptInput(InputState state, IInputStateChangeHandler handler)
        {
            this.state = state;
            this.handler = handler;
        }

        public Vector2 MousePosition => state.Mouse.Position;

        public ButtonStates<Key> Keys => state.Keyboard.Keys;

        public bool IsKeyPressed(Key key) => state.Keyboard.Keys.IsPressed(key);

        public void MoveMouseTo(Vector2 position) => new MousePositionAbsoluteInput { Position = position }.Apply(state, handler);

        public void SetKeyPressed(Key key, bool pressed) => new KeyboardKeyInput(key, pressed).Apply(state, handler);

        /// <summary>
        /// Resolves the input state for a script, or <see langword="null"/> when input is unavailable or the
        /// caller is not on the update thread.
        /// </summary>
        public static ScriptInput? FromServices(ScriptServices services)
        {
            if (services.Host != null && !services.Host.UpdateThread.IsCurrent)
                return null;

            InputManager? inputManager = services.InputManager;
            return inputManager == null ? null : new ScriptInput(inputManager.CurrentState, inputManager);
        }
    }
}
