// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Commands;

namespace osu.Game.Tests.API.Commands
{
    [TestFixture]
    public class CommandRegistryTest
    {
        private CommandRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            registry = new CommandRegistry();
        }

        [Test]
        public void TestTypedCommandReceivesArgument()
        {
            string? captured = null;
            registry.Add("echo").Argument<string>("text").Handler<string>(value => captured = value);

            var result = registry.Execute("echo hello world");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.AreEqual("hello world", captured);
        }

        [Test]
        public void TestCommandWithoutArgument()
        {
            bool called = false;
            registry.Add("ping").Handler(() => called = true);

            var result = registry.Execute("ping");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.IsTrue(called);
        }

        [Test]
        public void TestLeadingSlashAccepted()
        {
            bool called = false;
            registry.Add("ping").Handler(() => called = true);

            var result = registry.Execute("/ping");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.IsTrue(called);
        }

        [Test]
        public void TestUnknownCommand()
        {
            var result = registry.Execute("does-not-exist");

            ClassicAssert.AreEqual(CommandStatus.NotFound, result.Status);
        }

        [Test]
        public void TestInvalidArgumentsDoNotInvokeHandler()
        {
            int calls = 0;
            registry.Add("num").Argument<int>("value").Handler<int>(_ => calls++);

            var result = registry.Execute("num not-a-number");

            ClassicAssert.AreEqual(CommandStatus.InvalidArguments, result.Status);
            ClassicAssert.AreEqual(0, calls);
        }

        [Test]
        public void TestHandlerExceptionIsCaught()
        {
            registry.Add("boom").Handler(() => throw new InvalidOperationException("nope"));

            var result = registry.Execute("boom");

            ClassicAssert.AreEqual(CommandStatus.Error, result.Status);
        }

        [Test]
        public void TestAlias()
        {
            bool called = false;
            registry.Add("exit").Aliases("quit").Handler(() => called = true);

            registry.Execute("quit");

            ClassicAssert.IsTrue(called);
            ClassicAssert.IsNotNull(registry.Find("quit"));
            ClassicAssert.AreSame(registry.Find("exit"), registry.Find("quit"));
        }

        [Test]
        public void TestUnregister()
        {
            var token = registry.Add("temp").Handler(() => { });

            token.Dispose();

            ClassicAssert.IsNull(registry.Find("temp"));
            ClassicAssert.AreEqual(CommandStatus.NotFound, registry.Execute("temp").Status);
        }

        [Test]
        public void TestDuplicateNameThrows()
        {
            registry.Add("dup").Handler(() => { });

            ClassicAssert.Throws<InvalidOperationException>(() => registry.Add("dup").Handler(() => { }));
        }

        [Test]
        public void TestFuzzySearchRanksPrefixFirst()
        {
            registry.Add("search").Handler(() => { });
            registry.Add("exit").Handler(() => { });
            registry.Add("mods").Handler(() => { });

            var matches = registry.Search("sear");

            ClassicAssert.AreEqual("search", matches[0].Command.Name);
        }

        [Test]
        public void TestEmptySearchReturnsAll()
        {
            registry.Add("a").Handler(() => { });
            registry.Add("b").Handler(() => { });
            registry.Add("c").Handler(() => { });

            ClassicAssert.AreEqual(3, registry.Search(string.Empty).Count);
        }

        [Test]
        public void TestHandlerWithWrongArityThrows()
        {
            ClassicAssert.Throws<InvalidOperationException>(() => registry.Add("bad").Argument<string>("text").Handler(() => { }));
        }
    }
}
