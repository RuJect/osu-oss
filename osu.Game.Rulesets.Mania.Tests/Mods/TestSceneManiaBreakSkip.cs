// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Mods
{
    public partial class TestSceneManiaBreakSkip : PlayerTestScene
    {
        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        [Cached]
        private readonly SpecialConfigManager specialConfig = new SpecialConfigManager(new TemporaryNativeStorage(@"osu-oss-mania-break-skip-test"));

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = base.CreateBeatmap(ruleset);
            beatmap.Breaks.Clear();
            beatmap.Breaks.Add(new BreakPeriod(3000, 6000));
            return beatmap;
        }

        [Test]
        public void TestBreakSkipNotAppliedWhenInvertClearsBreaks()
        {
            AddStep("enable auto-skip breaks", () => specialConfig.GetBindable<AutoSkipMode>(SpecialSetting.AutoSkip).Value = AutoSkipMode.Breaks);
            AddStep("load player with invert", () => LoadPlayer(new Mod[] { new ManiaModInvert() }));
            AddUntilStep("player loaded", () => Player.IsLoaded && Player.Alpha == 1);
            AddUntilStep("passed break start", () => Player.GameplayClockContainer.CurrentTime >= 3500);
            AddAssert("break was not skipped", () => Player.GameplayClockContainer.CurrentTime < 6000);
        }
    }
}