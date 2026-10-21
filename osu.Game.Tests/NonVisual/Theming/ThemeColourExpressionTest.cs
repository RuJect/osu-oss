// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Theming;

namespace osu.Game.Tests.NonVisual.Theming
{
    [TestFixture]
    public class ThemeColourExpressionTest
    {
        private readonly Dictionary<string, string> tokens = new Dictionary<string, string>
        {
            ["Accent"] = "#8040c0",
            ["LightAccent"] = "lighten(@Accent, 0.2)",
        };

        [Test]
        public void TestHex()
        {
            Assert.That(ThemeColourExpression.Resolve("#ff0000", tokens), Is.EqualTo(Colour4.FromHex("#ff0000")));
        }

        [Test]
        public void TestTokenReference()
        {
            Assert.That(ThemeColourExpression.Resolve("@Accent", tokens), Is.EqualTo(Colour4.FromHex("#8040c0")));
        }

        [Test]
        public void TestAlpha()
        {
            Assert.That(ThemeColourExpression.Resolve("alpha(@Accent, 0.5)", tokens).A, Is.EqualTo(0.5f));
        }

        [Test]
        public void TestLighten()
        {
            Colour4 raw = ThemeColourExpression.Resolve("@Accent", tokens);
            Colour4 lighter = ThemeColourExpression.Resolve("@LightAccent", tokens);
            Assert.That(lighter.ToHSL().Z, Is.GreaterThan(raw.ToHSL().Z));
        }

        [Test]
        public void TestHSL()
        {
            Colour4 colour = ThemeColourExpression.Resolve("hsl(120, 1, 0.5)", tokens);
            Assert.That(colour.G, Is.EqualTo(1).Within(0.001f));
            Assert.That(colour.R, Is.EqualTo(0).Within(0.001f));
            Assert.That(colour.B, Is.EqualTo(0).Within(0.001f));
        }

        [Test]
        public void TestMix()
        {
            Colour4 colour = ThemeColourExpression.Resolve("mix(#000000, #ffffff, 0.5)", tokens);
            Assert.That(colour.R, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(colour.G, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(colour.B, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void TestGrayscale()
        {
            Colour4 colour = ThemeColourExpression.Resolve("grayscale(#ff0000)", tokens);
            Assert.That(colour.R, Is.EqualTo(colour.G).Within(0.001f));
            Assert.That(colour.G, Is.EqualTo(colour.B).Within(0.001f));
        }

        [Test]
        public void TestUnknownTokenThrows()
        {
            Assert.Throws<ThemeLoadException>(() => ThemeColourExpression.Resolve("@Missing", tokens));
        }

        [Test]
        public void TestCircularReferenceThrows()
        {
            var circular = new Dictionary<string, string>
            {
                ["A"] = "@B",
                ["B"] = "@A",
            };

            Assert.Throws<ThemeLoadException>(() => ThemeColourExpression.Resolve("@A", circular));
        }
    }
}
