// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Events
{
    /// <summary>A script pushed a custom named event via <c>osu.emit</c>.</summary>
    public sealed record ScriptEvent(string Name, object?[] Args) : OsuApiEvent;

    /// <summary>The current screen changed.</summary>
    public sealed record ScreenChangedEvent(string Name) : OsuApiEvent;

    /// <summary>A script started.</summary>
    public sealed record ScriptStartedEvent(string Name) : OsuApiEvent;

    /// <summary>A script stopped (stopped, completed or errored).</summary>
    public sealed record ScriptStoppedEvent(string Name) : OsuApiEvent;

    /// <summary>A script failed with an error.</summary>
    public sealed record ScriptErrorEvent(string Name, string Message) : OsuApiEvent;
}
