// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.API.Events
{
    /// <summary>A beatmap was selected in the song select carousel (selected, not launched).</summary>
    public sealed record BeatmapSelectedEvent(BeatmapInfo Beatmap) : OsuApiEvent;

    /// <summary>The currently selected mods changed.</summary>
    public sealed record SelectedModsChangedEvent(IReadOnlyList<Mod> Mods) : OsuApiEvent;

    /// <summary>The active ruleset changed.</summary>
    public sealed record RulesetChangedEvent(RulesetInfo Ruleset) : OsuApiEvent;

    /// <summary>A beatmap set finished downloading and importing.</summary>
    public sealed record BeatmapDownloadedEvent(IBeatmapSetInfo BeatmapSet) : OsuApiEvent;

    /// <summary>A beatmap set was deleted.</summary>
    public sealed record BeatmapDeletedEvent(IBeatmapSetInfo BeatmapSet) : OsuApiEvent;

    /// <summary>Gameplay has started loading.</summary>
    public sealed record GameplayLoadingEvent(Player Player) : OsuApiEvent;

    /// <summary>Gameplay finished loading and started.</summary>
    public sealed record GameplayLoadedEvent(Player Player) : OsuApiEvent;

    /// <summary>Gameplay was paused.</summary>
    public sealed record GameplayPausedEvent(Player Player) : OsuApiEvent;

    /// <summary>Gameplay was resumed.</summary>
    public sealed record GameplayResumedEvent(Player Player) : OsuApiEvent;

    /// <summary>Gameplay ended by the user quitting.</summary>
    public sealed record GameplayExitedEvent(Player Player) : OsuApiEvent;

    /// <summary>Gameplay was restarted.</summary>
    public sealed record GameplayRestartedEvent(Player Player) : OsuApiEvent;

    /// <summary>Gameplay was completed successfully.</summary>
    public sealed record GameplayPassedEvent(Player Player, ScoreInfo Score) : OsuApiEvent;

    /// <summary>Gameplay ended in failure.</summary>
    public sealed record GameplayFailedEvent(Player Player, ScoreInfo Score) : OsuApiEvent;
}
