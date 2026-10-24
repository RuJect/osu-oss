// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Graphics.Backdrops;
using osu.Game.Theming;

namespace osu.Game.Tests.Visual.Theming
{
    [TestFixture]
    public partial class TestSceneGlassSurfaces : OsuTestScene
    {
        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUpStorage()
        {
            storage = new TemporaryNativeStorage("osu-oss-glass-surfaces-test");
        }

        [TearDown]
        public void TearDownStorage()
        {
            ThemeManager.UseVanillaForTesting();
            storage.Dispose();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestTintIsOpaqueWhenBlurUnavailable(bool glassEffectsAllowed)
        {
            AddStep("initialise theme", () => ThemeManager.Initialise(storage, "torii-glass", glassEffectsAllowed));

            GlassBackdrop? backdrop = null;

            AddStep("create backdrop", () => Add(backdrop = new GlassBackdrop
            {
                TintColour = Colour4.White.Opacity(0.82f),
            }));

            AddUntilStep("backdrop loaded", () => backdrop!.IsLoaded);
            AddAssert("tint is opaque", () => backdrop!.ChildrenOfType<Box>().Single().Colour.TopLeft.SRGB.A, () => Is.EqualTo(1f));
        }
    }
}
