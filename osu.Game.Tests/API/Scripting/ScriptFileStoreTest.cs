// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Scripting;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class ScriptFileStoreTest
    {
        [TestCase("foo.txt", "foo.txt")]
        [TestCase("a/b/c.txt", "a/b/c.txt")]
        [TestCase(@"a\b\c.txt", "a/b/c.txt")]
        [TestCase("./a/./b", "a/b")]
        [TestCase("...", "...")]
        [TestCase("", null)]
        [TestCase("   ", null)]
        [TestCase("/abs/path.txt", null)]
        [TestCase("../escape.txt", null)]
        [TestCase("a/../b.txt", null)]
        [TestCase("a/../../escape.txt", null)]
        public void TestSanitizePath(string input, string? expected)
        {
            ClassicAssert.AreEqual(expected, ScriptFileStore.SanitizePath(input));
        }
    }
}
