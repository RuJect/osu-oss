// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Holds the game's root input manager once it has been resolved by the plugin runtime component.
    /// </summary>
    public sealed class InputManagerProvider
    {
        public InputManager? Value { get; set; }
    }
}
