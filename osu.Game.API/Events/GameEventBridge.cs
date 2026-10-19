// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Events;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.API.Events
{
    /// <summary>
    /// Bridges the native <see cref="GameEventHub"/> signals raised inside <c>osu.Game</c> into typed
    /// <see cref="OsuEventBus"/> events.
    /// </summary>
    public class GameEventBridge : IDisposable
    {
        private readonly GameEventHub hub;
        private readonly OsuEventBus bus;

        public GameEventBridge(GameEventHub hub, OsuEventBus bus)
        {
            this.hub = hub;
            this.bus = bus;

            hub.BeatmapSelected += onBeatmapSelected;
            hub.SelectedModsChanged += onSelectedModsChanged;
            hub.RulesetChanged += onRulesetChanged;
            hub.BeatmapDownloaded += onBeatmapDownloaded;
            hub.BeatmapDeleted += onBeatmapDeleted;
            hub.GameplayLoadingStarted += onGameplayLoadingStarted;
            hub.GameplayLoaded += onGameplayLoaded;
            hub.GameplayPaused += onGameplayPaused;
            hub.GameplayResumed += onGameplayResumed;
            hub.GameplayExited += onGameplayExited;
            hub.GameplayRestarted += onGameplayRestarted;
            hub.GameplayPassed += onGameplayPassed;
            hub.GameplayFailed += onGameplayFailed;
        }

        private void onBeatmapSelected(BeatmapInfo beatmap, Drawable? ui) => bus.Publish(new BeatmapSelectedEvent(beatmap) { UI = ui });

        private void onSelectedModsChanged(IReadOnlyList<Mod> mods, Drawable? ui) => bus.Publish(new SelectedModsChangedEvent(mods) { UI = ui });

        private void onRulesetChanged(RulesetInfo ruleset, Drawable? ui) => bus.Publish(new RulesetChangedEvent(ruleset) { UI = ui });

        private void onBeatmapDownloaded(IBeatmapSetInfo beatmapSet, Drawable? ui) => bus.Publish(new BeatmapDownloadedEvent(beatmapSet) { UI = ui });

        private void onBeatmapDeleted(IBeatmapSetInfo beatmapSet, Drawable? ui) => bus.Publish(new BeatmapDeletedEvent(beatmapSet) { UI = ui });

        private void onGameplayLoadingStarted(Player player) => bus.Publish(new GameplayLoadingEvent(player) { UI = player });

        private void onGameplayLoaded(Player player) => bus.Publish(new GameplayLoadedEvent(player) { UI = player });

        private void onGameplayPaused(Player player) => bus.Publish(new GameplayPausedEvent(player) { UI = player });

        private void onGameplayResumed(Player player) => bus.Publish(new GameplayResumedEvent(player) { UI = player });

        private void onGameplayExited(Player player) => bus.Publish(new GameplayExitedEvent(player) { UI = player });

        private void onGameplayRestarted(Player player) => bus.Publish(new GameplayRestartedEvent(player) { UI = player });

        private void onGameplayPassed(Player player, ScoreInfo score) => bus.Publish(new GameplayPassedEvent(player, score) { UI = player });

        private void onGameplayFailed(Player player, ScoreInfo score) => bus.Publish(new GameplayFailedEvent(player, score) { UI = player });

        public void Dispose()
        {
            hub.BeatmapSelected -= onBeatmapSelected;
            hub.SelectedModsChanged -= onSelectedModsChanged;
            hub.RulesetChanged -= onRulesetChanged;
            hub.BeatmapDownloaded -= onBeatmapDownloaded;
            hub.BeatmapDeleted -= onBeatmapDeleted;
            hub.GameplayLoadingStarted -= onGameplayLoadingStarted;
            hub.GameplayLoaded -= onGameplayLoaded;
            hub.GameplayPaused -= onGameplayPaused;
            hub.GameplayResumed -= onGameplayResumed;
            hub.GameplayExited -= onGameplayExited;
            hub.GameplayRestarted -= onGameplayRestarted;
            hub.GameplayPassed -= onGameplayPassed;
            hub.GameplayFailed -= onGameplayFailed;
        }
    }
}
