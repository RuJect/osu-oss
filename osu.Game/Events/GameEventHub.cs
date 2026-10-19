// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Events
{
    /// <summary>
    /// A lightweight transport for game-wide notifications raised at the points where the respective
    /// action occurs in <c>osu.Game</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>osu.Game</c> cannot reference <c>osu.Game.API</c> (the dependency direction is reversed), so this hub
    /// deliberately carries only native osu.Game types. External modules (such as the event bus in
    /// <c>osu.Game.API</c>) subscribe from outside and translate these signals into their own typed payloads.
    /// </para>
    /// <para>
    /// This type is intentionally independent from the scripting subsystem.
    /// </para>
    /// </remarks>
    public class GameEventHub
    {
        /// <summary>Raised when a beatmap is selected in the song select carousel (not launched).</summary>
        public event Action<BeatmapInfo, Drawable?>? BeatmapSelected;

        /// <summary>Raised when the current mod selection changes.</summary>
        public event Action<IReadOnlyList<Mod>, Drawable?>? SelectedModsChanged;

        /// <summary>Raised when the current ruleset changes.</summary>
        public event Action<RulesetInfo, Drawable?>? RulesetChanged;

        /// <summary>Raised when a beatmap set has finished downloading and importing.</summary>
        public event Action<IBeatmapSetInfo, Drawable?>? BeatmapDownloaded;

        /// <summary>Raised when a beatmap set is deleted.</summary>
        public event Action<IBeatmapSetInfo, Drawable?>? BeatmapDeleted;

        /// <summary>Raised when a gameplay session begins loading.</summary>
        public event Action<Player>? GameplayLoadingStarted;

        /// <summary>Raised when a gameplay session has finished loading and gameplay started.</summary>
        public event Action<Player>? GameplayLoaded;

        /// <summary>Raised when gameplay is paused.</summary>
        public event Action<Player>? GameplayPaused;

        /// <summary>Raised when gameplay is resumed.</summary>
        public event Action<Player>? GameplayResumed;

        /// <summary>Raised when gameplay ends by the user quitting.</summary>
        public event Action<Player>? GameplayExited;

        /// <summary>Raised when gameplay is restarted.</summary>
        public event Action<Player>? GameplayRestarted;

        /// <summary>Raised when gameplay is completed successfully.</summary>
        public event Action<Player, ScoreInfo>? GameplayPassed;

        /// <summary>Raised when gameplay ends in failure.</summary>
        public event Action<Player, ScoreInfo>? GameplayFailed;

        internal void RaiseBeatmapSelected(BeatmapInfo beatmap, Drawable? ui) => BeatmapSelected?.Invoke(beatmap, ui);

        internal void RaiseSelectedModsChanged(IReadOnlyList<Mod> mods, Drawable? ui) => SelectedModsChanged?.Invoke(mods, ui);

        internal void RaiseRulesetChanged(RulesetInfo ruleset, Drawable? ui) => RulesetChanged?.Invoke(ruleset, ui);

        internal void RaiseBeatmapDownloaded(IBeatmapSetInfo beatmapSet, Drawable? ui = null) => BeatmapDownloaded?.Invoke(beatmapSet, ui);

        internal void RaiseBeatmapDeleted(IBeatmapSetInfo beatmapSet, Drawable? ui = null) => BeatmapDeleted?.Invoke(beatmapSet, ui);

        internal void RaiseGameplayLoadingStarted(Player player) => GameplayLoadingStarted?.Invoke(player);

        internal void RaiseGameplayLoaded(Player player) => GameplayLoaded?.Invoke(player);

        internal void RaiseGameplayPaused(Player player) => GameplayPaused?.Invoke(player);

        internal void RaiseGameplayResumed(Player player) => GameplayResumed?.Invoke(player);

        internal void RaiseGameplayExited(Player player) => GameplayExited?.Invoke(player);

        internal void RaiseGameplayRestarted(Player player) => GameplayRestarted?.Invoke(player);

        internal void RaiseGameplayPassed(Player player, ScoreInfo score) => GameplayPassed?.Invoke(player, score);

        internal void RaiseGameplayFailed(Player player, ScoreInfo score) => GameplayFailed?.Invoke(player, score);
    }
}
