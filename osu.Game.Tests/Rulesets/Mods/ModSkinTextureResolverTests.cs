// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.Rulesets.Mods
{
    [TestFixture]
    public class ModSkinTextureResolverTests
    {
        [TestCase(typeof(OsuModDoubleTime), "selection-mod-doubletime.png")]
        [TestCase(typeof(OsuModHardRock), "selection-mod-hardrock.png")]
        [TestCase(typeof(OsuModHidden), "selection-mod-hidden.png")]
        [TestCase(typeof(OsuModNoFail), "selection-mod-nofail.png")]
        [TestCase(typeof(OsuModNightcore), "selection-mod-nightcore.png")]
        [TestCase(typeof(OsuModSuddenDeath), "selection-mod-suddendeath.png")]
        [TestCase(typeof(OsuModPerfect), "selection-mod-perfect.png")]
        [TestCase(typeof(OsuModSpunOut), "selection-mod-spunout.png")]
        [TestCase(typeof(OsuModRandom), "selection-mod-rd.png")]
        [TestCase(typeof(OsuModRelax), "selection-mod-relax.png")]
        [TestCase(typeof(OsuModAutopilot), "selection-mod-relax2.png")]
        [TestCase(typeof(OsuModTargetPractice), "selection-mod-target.png")]
        [TestCase(typeof(OsuModMirror), "selection-mod-mirror.png")]
        [TestCase(typeof(OsuModCinema), "selection-mod-cn.png")]
        [TestCase(typeof(OsuModFlashlight), "selection-mod-flashlight.png")]
        [TestCase(typeof(ManiaModFadeIn), "selection-mod-fadein.png")]
        [TestCase(typeof(OsuModAutoplay), "selection-mod-autoplay.png")]
        [TestCase(typeof(ManiaModScoreV2), "selection-mod-sv2.png")]
        [TestCase(typeof(OsuModHalfTime), "selection-mod-halftime.png")]
        [TestCase(typeof(ManiaModKey1), "selection-mod-1k.png")]
        [TestCase(typeof(ManiaModKey2), "selection-mod-2k.png")]
        [TestCase(typeof(ManiaModKey3), "selection-mod-3k.png")]
        [TestCase(typeof(ManiaModKey4), "selection-mod-4k.png")]
        [TestCase(typeof(ManiaModKey5), "selection-mod-5k.png")]
        [TestCase(typeof(ManiaModKey6), "selection-mod-6k.png")]
        [TestCase(typeof(ManiaModKey7), "selection-mod-7k.png")]
        [TestCase(typeof(ManiaModKey8), "selection-mod-8k.png")]
        [TestCase(typeof(ManiaModKey9), "selection-mod-9k.png")]
        public void TestKnownMods(Type modType, string expectedTextureName)
        {
            var mod = (Mod)Activator.CreateInstance(modType)!;
            string result = ModSkinTextureResolver.GetTextureName(mod);
            Assert.That(result, Is.EqualTo(expectedTextureName));
        }

        [Test]
        public void TestUnknownModFallsBackToLowercaseAcronym()
        {
            // Use a mod that doesn't have a mapping
            var mod = new TestUnknownMod();
            string result = ModSkinTextureResolver.GetTextureName(mod);
            Assert.That(result, Is.EqualTo("selection-mod-xyz.png"));
        }

        [Test]
        public void TestNullModThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ModSkinTextureResolver.GetTextureName(null!));
        }

        // A mod with an unknown acronym for testing fallback
        private class TestUnknownMod : Mod
        {
            public override string Name => "Test Unknown Mod";
            public override string Acronym => "XYZ";
            public override ModType Type => ModType.DifficultyIncrease;
            public override LocalisableString Description => "Test mod for resolver";
        }
    }
}