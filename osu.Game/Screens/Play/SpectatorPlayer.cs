// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Online.Spectator;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Screens.Play
{
    public class SpectatorPlayer : ReplayPlayer
    {
        public SpectatorPlayer(Score score)
            : base(score)
        {
        }

        protected override ResultsScreen CreateResults(ScoreInfo score)
        {
            return new SpectatorResultsScreen(score);
        }

        [Resolved]
        private SpectatorStreamingClient spectatorStreaming { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            spectatorStreaming.OnUserBeganPlaying += userBeganPlaying;
        }

        private void userBeganPlaying(int userId, SpectatorState state)
        {
            if (userId == Score.ScoreInfo.UserID)
                Schedule(this.Exit);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (spectatorStreaming != null)
                spectatorStreaming.OnUserBeganPlaying -= userBeganPlaying;
        }

        protected override GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart)
        {
            // if we already have frames, start gameplay at the point in time they exist, should they be too far into the beatmap.
            double? firstFrameTime = Score.Replay.Frames.FirstOrDefault()?.Time;

            if (firstFrameTime == null || firstFrameTime <= gameplayStart + 5000)
                return base.CreateGameplayClockContainer(beatmap, gameplayStart);

            return new GameplayClockContainer(beatmap, firstFrameTime.Value, true);
        }
    }
}
