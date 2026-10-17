// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Auth.Medals
{
    /// <summary>Cyclone (354): 477+ spins per minute on an osu! spinner without rate/automation mods.</summary>
    public class CycloneAwarder : IMedalAwarder
    {
        private SubmittingPlayer? currentPlayer;
        private HitObjectContainer? hitObjects;

        public int MedalId => 354;

        public bool Enabled { get; set; }

        public bool CheckMedalCriteria(OsuGame game)
        {
            if (game.ScreenStack?.CurrentScreen is not SubmittingPlayer submittingPlayer)
                return false;

            if (currentPlayer != submittingPlayer)
            {
                currentPlayer = submittingPlayer;
                hitObjects = null;
            }

            GameplayState? state = currentPlayer.GameplayState;

            if (state != null && state.Mods.Any(isRateOrAutomation))
                return false;

            if (state != null && state.HasFailed)
                return false;

            hitObjects ??= currentPlayer.Ruleset.Value.Equals(new OsuRuleset().RulesetInfo)
                ? submittingPlayer.ChildrenOfType<HitObjectContainer>().SingleOrDefault()
                : null;

            if (hitObjects == null)
                return false;

            foreach (DrawableSpinner spinner in hitObjects.AliveObjects.OfType<DrawableSpinner>())
            {
                if (spinner.SpinsPerMinute.Value >= 477.0)
                    return true;
            }

            return false;
        }

        private static bool isRateOrAutomation(Mod mod)
            => mod.Type == ModType.Automation || mod is IApplicableToRate;
    }
}
