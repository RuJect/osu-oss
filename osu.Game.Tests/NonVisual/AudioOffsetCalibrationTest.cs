// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Overlays.Settings.Sections.Special;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class AudioOffsetCalibrationTest
    {
        [Test]
        public void TestLateTapsLowerOffset()
        {
            Assert.That(AudioOffsetCalibrationDialog.CalculateSuggestedOffset(0, new[] { 10.0, 12.0, 8.0 }, -500, 500), Is.EqualTo(-10));
        }

        [Test]
        public void TestEarlyTapsRaiseOffset()
        {
            Assert.That(AudioOffsetCalibrationDialog.CalculateSuggestedOffset(0, new[] { -10.0, -12.0, -8.0 }, -500, 500), Is.EqualTo(10));
        }

        [Test]
        public void TestMedianIgnoresOutlier()
        {
            Assert.That(AudioOffsetCalibrationDialog.CalculateSuggestedOffset(0, new[] { -10.0, -10.0, 200.0 }, -500, 500), Is.EqualTo(10));
        }

        [Test]
        public void TestNonZeroOffsetIsPreserved()
        {
            Assert.That(AudioOffsetCalibrationDialog.CalculateSuggestedOffset(25, new[] { -10.0, -10.0, -10.0 }, -500, 500), Is.EqualTo(35));
        }

        [Test]
        public void TestSuggestionIsClamped()
        {
            Assert.That(AudioOffsetCalibrationDialog.CalculateSuggestedOffset(499, new[] { -20.0, -20.0, -20.0 }, -500, 500), Is.EqualTo(500));
        }
    }
}
