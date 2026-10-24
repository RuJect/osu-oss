// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Services
{
    /// <summary>
    /// Severity of a single line of output emitted by a script or plugin.
    /// </summary>
    public enum OutputLevel
    {
        Log,
        Info,
        Warn,
        Error,
    }
}
