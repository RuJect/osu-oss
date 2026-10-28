// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Commands;

namespace osu.Game.Tests.API.Commands
{
    [TestFixture]
    public class CommandBinderTest
    {
        private CommandRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            registry = new CommandRegistry();
        }

        [Test]
        public void TestSingleStringTakesWholeRemainder()
        {
            string? captured = null;
            registry.Add("echo").Argument<string>("text").Handler<string>(text => captured = text);

            var result = registry.Execute("echo hello world");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.AreEqual("hello world", captured);
        }

        [Test]
        public void TestSingleStringNamedValue()
        {
            string? captured = null;
            registry.Add("echo").Argument<string>("text").Handler<string>(text => captured = text);

            var result = registry.Execute("echo text=hello world");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.AreEqual("hello world", captured);
        }

        [Test]
        public void TestArrayWithCommaAndWhitespace()
        {
            string[]? captured = null;
            registry.Add("mods").Argument<string[]>("mods").Handler<string[]>(mods => captured = mods);

            var result = registry.Execute("mods nm, hr dt");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.IsNotNull(captured);
            CollectionAssert.AreEqual(new[] { "nm", "hr", "dt" }, captured!);
        }

        [Test]
        public void TestArrayBracketsAndQuotedElements()
        {
            string[]? captured = null;
            registry.Add("mods").Argument<string[]>("mods").Handler<string[]>(mods => captured = mods);

            var result = registry.Execute("mods [nm, \"Hard Rock\"]");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.IsNotNull(captured);
            CollectionAssert.AreEqual(new[] { "nm", "Hard Rock" }, captured!);
        }

        [Test]
        public void TestMultipleArgumentsPositional()
        {
            string? name = null;
            int count = 0;

            registry.Add("add")
                    .Argument<string>("name")
                    .Argument<int>("count")
                    .Handler<string, int>((n, c) =>
                    {
                        name = n;
                        count = c;
                    });

            var result = registry.Execute("add foo 2");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.AreEqual("foo", name);
            ClassicAssert.AreEqual(2, count);
        }

        [Test]
        public void TestMultipleArgumentsNamed()
        {
            string? name = null;
            int count = 0;

            registry.Add("add")
                    .Argument<string>("name")
                    .Argument<int>("count")
                    .Handler<string, int>((n, c) =>
                    {
                        name = n;
                        count = c;
                    });

            var result = registry.Execute("add count=3 name=foo");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.AreEqual("foo", name);
            ClassicAssert.AreEqual(3, count);
        }

        [Test]
        public void TestUnknownNamedArgument()
        {
            int calls = 0;
            registry.Add("add").Argument<int>("count").Handler<int>(_ => calls++);

            var result = registry.Execute("add other=1");

            ClassicAssert.AreEqual(CommandStatus.InvalidArguments, result.Status);
            ClassicAssert.AreEqual(0, calls);
        }

        [Test]
        public void TestMissingRequiredArgument()
        {
            int calls = 0;
            registry.Add("add").Argument<int>("count").Handler<int>(_ => calls++);

            var result = registry.Execute("add");

            ClassicAssert.AreEqual(CommandStatus.InvalidArguments, result.Status);
            ClassicAssert.AreEqual(0, calls);
        }

        [Test]
        public void TestOptionalDefaultValue()
        {
            int captured = 0;
            registry.Add("opt").Argument<int>("count", argument => argument.Optional(5)).Handler<int>(count => captured = count);

            var result = registry.Execute("opt");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.AreEqual(5, captured);
        }

        [Test]
        public void TestOptionalStringDefaultIsNull()
        {
            string? captured = "sentinel";
            registry.Add("opt").Argument<string>("name", argument => argument.Optional()).Handler<string?>(name => captured = name);

            var result = registry.Execute("opt");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.IsNull(captured);
        }

        [Test]
        public void TestEnumIsCaseInsensitive()
        {
            Mode captured = default;
            registry.Add("mode").Argument<Mode>("mode").Handler<Mode>(mode => captured = mode);

            var result = registry.Execute("mode fast");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.AreEqual(Mode.Fast, captured);
        }

        [Test]
        public void TestBooleanAliases()
        {
            bool captured = true;
            registry.Add("flag").Argument<bool>("value").Handler<bool>(value => captured = value);

            ClassicAssert.IsTrue(registry.Execute("flag -").Succeeded);
            ClassicAssert.IsFalse(captured);

            ClassicAssert.IsTrue(registry.Execute("flag +").Succeeded);
            ClassicAssert.IsTrue(captured);
        }

        [Test]
        public void TestObjectWithColonSeparatedFields()
        {
            Options? captured = null;
            registry.Add("opt").Argument<Options>("options").Handler<Options>(options => captured = options);

            var result = registry.Execute("opt name: foo count: 2 enabled: true");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.IsNotNull(captured);
            ClassicAssert.AreEqual("foo", captured!.Name);
            ClassicAssert.AreEqual(2, captured.Count);
            ClassicAssert.IsTrue(captured.Enabled);
        }

        [Test]
        public void TestObjectUnknownFieldReportsError()
        {
            registry.Add("opt").Argument<Options>("options").Handler<Options>(_ => { });

            var result = registry.Execute("opt name: foo count: 2 enabled: true unknown: 1");

            ClassicAssert.AreEqual(CommandStatus.InvalidArguments, result.Status);
            ClassicAssert.IsTrue(result.Message!.Contains("unknown"));
        }

        [Test]
        public void TestDictionaryArgument()
        {
            Dictionary<string, int>? captured = null;
            registry.Add("map").Argument<Dictionary<string, int>>("values").Handler<Dictionary<string, int>>(values => captured = values);

            var result = registry.Execute("map a: 1, b: 2");

            ClassicAssert.IsTrue(result.Succeeded);
            ClassicAssert.IsNotNull(captured);
            ClassicAssert.AreEqual(1, captured!["a"]);
            ClassicAssert.AreEqual(2, captured["b"]);
        }

        [Test]
        public void TestTooManyArguments()
        {
            registry.Add("add").Argument<string>("name").Argument<string>("other").Handler<string, string>((_, _) => { });

            var result = registry.Execute("add one two three");

            ClassicAssert.AreEqual(CommandStatus.InvalidArguments, result.Status);
        }

        private enum Mode
        {
            Slow,
            Fast,
        }

        private class Options
        {
            public string Name { get; set; } = string.Empty;

            public int Count { get; set; }

            public bool Enabled { get; set; }
        }
    }
}
