// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Commands;

namespace osu.Game.Tests.API.Commands
{
    [TestFixture]
    public class FuzzyMatcherTest
    {
        [Test]
        public void TestEmptyPatternMatchesAnything()
        {
            var match = FuzzyMatcher.Match(string.Empty, "anything");

            ClassicAssert.IsNotNull(match);
            ClassicAssert.IsEmpty(match.Value.Indices);
        }

        [Test]
        public void TestSubsequenceMatch()
        {
            var match = FuzzyMatcher.Match("abc", "axbxc");

            ClassicAssert.IsNotNull(match);
            ClassicAssert.AreEqual(new[] { 0, 2, 4 }, match.Value.Indices.ToArray());
        }

        [Test]
        public void TestNonSubsequenceDoesNotMatch()
        {
            ClassicAssert.IsNull(FuzzyMatcher.Match("abc", "acb"));
        }

        [Test]
        public void TestCaseInsensitive()
        {
            ClassicAssert.IsNotNull(FuzzyMatcher.Match("EXIT", "Exit"));
        }

        [Test]
        public void TestContiguousMatchScoresHigher()
        {
            var contiguous = FuzzyMatcher.Match("abc", "abc");
            var scattered = FuzzyMatcher.Match("abc", "axxbxxc");

            ClassicAssert.IsNotNull(contiguous);
            ClassicAssert.IsNotNull(scattered);
            ClassicAssert.Greater(contiguous.Value.Score, scattered.Value.Score);
        }

        [Test]
        public void TestShorterTextScoresHigher()
        {
            var shorter = FuzzyMatcher.Match("mod", "mods");
            var longer = FuzzyMatcher.Match("mod", "moderation");

            ClassicAssert.IsNotNull(shorter);
            ClassicAssert.IsNotNull(longer);
            ClassicAssert.Greater(shorter.Value.Score, longer.Value.Score);
        }
    }
}
