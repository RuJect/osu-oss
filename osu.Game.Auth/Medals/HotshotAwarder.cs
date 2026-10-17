// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Ranking;

namespace osu.Game.Auth.Medals
{
    /// <summary>Hotshot (353): sole full combo in a 4+ player multiplayer match.</summary>
    public class HotshotAwarder : IMedalAwarder
    {
        public int MedalId => 353;

        public bool Enabled { get; set; }

        public bool CheckMedalCriteria(OsuGame game)
        {
            if (game.ScreenStack?.CurrentScreen is not MultiplayerResultsScreen results)
                return false;

            ScoreInfo? score = results.Score;
            var api = (IAPIProvider)game.Dependencies.Get(typeof(IAPIProvider));

            if (score == null || score.OnlineID <= 0)
                return false;

            bool selfFullCombo = false;
            bool otherFullCombo = false;
            int count = 0;

            foreach (ScoreInfo item in results.ChildrenOfType<ScorePanel>().Select(p => p.Score))
            {
                if (item.UserID == api.LocalUser.Value.OnlineID)
                    selfFullCombo = item.MaxCombo == item.GetMaximumAchievableCombo();
                else
                    otherFullCombo |= item.MaxCombo == item.GetMaximumAchievableCombo();

                count++;
            }

            return selfFullCombo && !otherFullCombo && count >= 4;
        }
    }
}
