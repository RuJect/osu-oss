// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Plugins
{
    /// <summary>Lifecycle state of a loaded plugin.</summary>
    public enum PluginState
    {
        Running,
        Stopped,
        Error,
    }
}
