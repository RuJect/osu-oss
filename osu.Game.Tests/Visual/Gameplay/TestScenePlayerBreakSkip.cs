// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestScenePlayerBreakSkip : PlayerTestScene
    {
        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        [Cached]
        private readonly SpecialConfigManager specialConfig = new SpecialConfigManager(new TemporaryNativeStorage(@"osu-oss-break-skip-test"));

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap
        {
            Difficulty = { DrainRate = 5 },
            HitObjects =
            {
                new HitCircle { Position = OsuPlayfield.BASE_SIZE / 2, StartTime = 0 },
                new HitCircle { Position = OsuPlayfield.BASE_SIZE / 2, StartTime = 2000 },
                new HitCircle { Position = OsuPlayfield.BASE_SIZE / 2, StartTime = 7000 },
            },
            Breaks =
            {
                new BreakPeriod(3000, 6000)
            }
        };

        [Test]
        public void TestAutoSkipBreakDoesNotChangeHealth()
        {
            double healthAtBreakStart = 0;

            AddStep("enable auto-skip breaks", () => specialConfig.GetBindable<AutoSkipMode>(SpecialSetting.AutoSkip).Value = AutoSkipMode.Breaks);
            CreateTest();

            AddUntilStep("reached break", () => Player.IsBreakTime.Value);
            AddStep("capture health", () => healthAtBreakStart = Player.HealthProcessor.Health.Value);
            AddUntilStep("break skipped", () => Player.GameplayClockContainer.CurrentTime >= 6000);
            AddAssert("health unchanged by skip", () => Precision.AlmostEquals(healthAtBreakStart, Player.HealthProcessor.Health.Value));
        }

        [Test]
        public void TestBreakNotSkippedWithoutSetting()
        {
            AddStep("disable auto-skip breaks", () => specialConfig.GetBindable<AutoSkipMode>(SpecialSetting.AutoSkip).Value = AutoSkipMode.None);
            CreateTest();

            AddUntilStep("reached break start", () => Player.GameplayClockContainer.CurrentTime >= 3000);
            AddAssert("not skipped yet", () => Player.GameplayClockContainer.CurrentTime < 5900);
            AddUntilStep("break passed naturally", () => Player.GameplayClockContainer.CurrentTime >= 6000);
        }
    }
}