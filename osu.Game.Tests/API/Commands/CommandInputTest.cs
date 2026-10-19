// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Commands;

namespace osu.Game.Tests.API.Commands
{
    [TestFixture]
    public class CommandInputTest
    {
        [Test]
        public void TestNameOnly()
        {
            var input = CommandInput.Parse("/mods");

            ClassicAssert.AreEqual("mods", input.Name);
            ClassicAssert.AreEqual(string.Empty, input.Argument);
            ClassicAssert.IsFalse(input.HasArgument);
        }

        [Test]
        public void TestNameAndArgument()
        {
            var input = CommandInput.Parse("/s test");

            ClassicAssert.AreEqual("s", input.Name);
            ClassicAssert.AreEqual("test", input.Argument);
            ClassicAssert.IsTrue(input.HasArgument);
        }

        [Test]
        public void TestTrailingSpaceStartsArguments()
        {
            var input = CommandInput.Parse("/mods ");

            ClassicAssert.AreEqual("mods", input.Name);
            ClassicAssert.AreEqual(string.Empty, input.Argument);
            ClassicAssert.IsTrue(input.HasArgument);
        }

        [Test]
        public void TestLeadingSlashOptional()
        {
            var input = CommandInput.Parse("exit now");

            ClassicAssert.AreEqual("exit", input.Name);
            ClassicAssert.AreEqual("now", input.Argument);
        }

        [Test]
        public void TestCommaListArgument()
        {
            var input = CommandInput.Parse("/mods nm,hr");

            ClassicAssert.AreEqual("mods", input.Name);
            ClassicAssert.AreEqual("nm,hr", input.Argument);
        }

        [Test]
        public void TestEmpty()
        {
            var input = CommandInput.Parse(string.Empty);

            ClassicAssert.AreEqual(string.Empty, input.Name);
            ClassicAssert.IsFalse(input.HasArgument);
        }

        [Test]
        public void TestFuzzyResolutionOfPartialName()
        {
            var registry = new CommandRegistry();
            registry.Add("exit").Handler(() => { });
            registry.Add("search").Handler(() => { });
            registry.Add("mods").Handler(() => { });

            var input = CommandInput.Parse("/s test");

            ClassicAssert.AreEqual("search", registry.Search(input.Name)[0].Command.Name);
        }
    }
}
