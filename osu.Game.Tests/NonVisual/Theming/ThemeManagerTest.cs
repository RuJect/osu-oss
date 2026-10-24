// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Theming;

namespace osu.Game.Tests.NonVisual.Theming
{
    [TestFixture]
    [NonParallelizable]
    public class ThemeManagerTest
    {
        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("osu-oss-theme-manager-test");
        }

        [TearDown]
        public void TearDown()
        {
            ThemeManager.UseVanillaForTesting();
            storage.Dispose();
        }

        [Test]
        public void TestAllBuiltInThemesLoad()
        {
            ThemeManager.Initialise(storage, ThemeManager.DEFAULT_THEME);

            string[] expected =
            {
                "abyss",
                "ember",
                "grayscale-fsyori",
                "midnight-cerulean",
                "midnight-crimson",
                "midnight-mauve",
                "midnight-violet",
                "nord",
                "sakura",
                "torii-glass",
                "torii-legacy",
            };

            Assert.That(ThemeManager.LoadErrors, Is.Empty);
            Assert.That(expected.All(id => ThemeManager.AvailableThemes.Any(t => t.Id == id)), Is.True);
        }

        [Test]
        public void TestDefaultThemeIsMidnightViolet()
        {
            ThemeManager.Initialise(storage, ThemeManager.DEFAULT_THEME);

            Assert.That(ThemeManager.Current.Id, Is.EqualTo("midnight-violet"));
            Assert.That(ThemeManager.Current.GlassEnabled, Is.True);
            Assert.That(ThemeManager.Current.CornerRadius, Is.Zero);
        }

        [Test]
        public void TestColoursAndOverlayResolve()
        {
            ThemeManager.Initialise(storage, "nord");

            Assert.That(ThemeManager.Current.TryGetColour("Purple", out var purple), Is.True);
            Assert.That(purple.ToHex(), Is.EqualTo("#88C0D0").IgnoreCase);

            var overlay = new Overlays.OverlayColourProvider(Overlays.OverlayColourScheme.Purple);
            Assert.That(overlay.Colour0.ToHex(), Is.Not.EqualTo("#b299ff"));
        }

        [Test]
        public void TestMidnightSurfacesAreNearlyBlack()
        {
            ThemeManager.Initialise(storage, "midnight-mauve");

            var overlay = new Overlays.OverlayColourProvider(Overlays.OverlayColourScheme.Blue);
            Colour4 background = (Colour4)overlay.Background4;

            Assert.That(Math.Max(background.R, Math.Max(background.G, background.B)), Is.LessThan(0.05f));
        }

        [Test]
        public void TestMidnightVioletUsesVioletAccents()
        {
            ThemeManager.Initialise(storage, "midnight-violet");

            Assert.That(ThemeManager.Current.TryGetColour("Purple", out var purple), Is.True);
            Assert.That(purple.ToHSL().X * 360f, Is.InRange(260f, 300f));
        }

        [Test]
        public void TestAggressiveTintingAppliesThemeHueToAllColours()
        {
            ThemeManager.Initialise(storage, "midnight-violet", aggressiveTinting: true);

            Assert.That(ThemeManager.AggressiveTinting, Is.True);

            foreach (string colourName in new[] { "Purple", "Pink", "Red", "Green", "Yellow" })
            {
                Assert.That(ThemeManager.Current.TryGetColour(colourName, out var colour), Is.True, colourName);
                Assert.That(colour.ToHSL().X * 360f, Is.Not.InRange(279f, 281f), $"{colourName} should not be tinted in the raw palette");
            }

            var colours = new OsuColour();

            foreach (var colour in new[] { colours.Purple, colours.Pink, colours.Red, colours.Green, colours.Yellow })
                Assert.That(((Colour4)colour).ToHSL().X * 360f, Is.InRange(279f, 281f));

            var overlay = new Overlays.OverlayColourProvider(Overlays.OverlayColourScheme.Blue);
            Assert.That(((Colour4)overlay.Colour0).ToHSL().X * 360f, Is.InRange(279f, 281f));
        }

        [Test]
        public void TestAggressiveTintingIsEnabledByDefault()
        {
            ThemeManager.Initialise(storage, ThemeManager.DEFAULT_THEME);

            Assert.That(ThemeManager.AggressiveTinting, Is.True);
            Assert.That(((Colour4)new OsuColour().Red).ToHSL().X * 360f, Is.InRange(279f, 281f));
        }

        [Test]
        public void TestGlassEffectsCanBeDisabled()
        {
            ThemeManager.Initialise(storage, "torii-glass", glassEffectsAllowed: false);

            Assert.That(ThemeManager.Current.GlassEnabled, Is.True);
            Assert.That(ThemeManager.GlassEffectsAllowed, Is.False);
            Assert.That(ThemeManager.GlassActive, Is.False);

            ThemeManager.Initialise(storage, "torii-glass", glassEffectsAllowed: true);

            Assert.That(ThemeManager.GlassActive, Is.True);
        }

        [Test]
        public void TestSurfaceAlphaIsOpaqueWithoutGlass()
        {
            ThemeManager.Initialise(storage, "torii-glass");

            Assert.That(ThemeManager.Current.GetSurfaceAlpha("panel", 0.92f), Is.LessThan(1f));
            Assert.That(ThemeManager.Current.ApplySurfaceAlpha("panel", Colour4.White).A, Is.LessThan(1f));

            ThemeManager.Initialise(storage, "torii-glass", glassEffectsAllowed: false);

            Assert.That(ThemeManager.Current.GetSurfaceAlpha("panel", 0.92f), Is.EqualTo(1f));
            Assert.That(ThemeManager.Current.ApplySurfaceAlpha("panel", Colour4.White).A, Is.EqualTo(1f));

            ThemeManager.Initialise(storage, "torii-legacy");

            Assert.That(ThemeManager.Current.GetSurfaceAlpha("panel", 0.92f), Is.EqualTo(1f));
        }

        [Test]
        public void TestOsuColourUsesTheme()
        {
            ThemeManager.Initialise(storage, "nord", aggressiveTinting: false);

            var colours = new OsuColour();

            Assert.That(colours.Purple.ToHex(), Is.EqualTo("#88C0D0").IgnoreCase);
        }

        [Test]
        public void TestUserThemeInheritance()
        {
            string themesPath = Path.Combine(storage.GetFullPath(string.Empty), "osu-oss", "themes");
            Directory.CreateDirectory(themesPath);

            File.WriteAllText(Path.Combine(themesPath, "test-theme.json"), @"{
  ""name"": ""Test Theme"",
  ""inherits"": ""torii-legacy"",
  ""colors"": {
    ""Purple"": ""#123456"",
    ""PurpleLight"": ""lighten(@Purple, 0.2)""
  },
  ""glass"": { ""enabled"": false }
}");

            ThemeManager.Initialise(storage, "test-theme");

            Assert.That(ThemeManager.Current.Id, Is.EqualTo("test-theme"));
            Assert.That(ThemeManager.Current.Name, Is.EqualTo("Test Theme"));
            Assert.That(ThemeManager.Current.TryGetColour("Purple", out var purple), Is.True);
            Assert.That(purple.ToHex(), Is.EqualTo("#123456").IgnoreCase);
            Assert.That(ThemeManager.Current.GlassEnabled, Is.False);
        }

        [Test]
        public void TestUnknownThemeFallsBackToDefault()
        {
            ThemeManager.Initialise(storage, "definitely-not-a-theme");

            Assert.That(ThemeManager.Current.Id, Is.EqualTo(ThemeManager.DEFAULT_THEME));
        }
    }
}
