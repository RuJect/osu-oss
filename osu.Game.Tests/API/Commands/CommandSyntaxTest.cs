// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Commands.Syntax;

namespace osu.Game.Tests.API.Commands
{
    [TestFixture]
    public class CommandSyntaxTest
    {
        [Test]
        public void TestBareScalarTypes()
        {
            ClassicAssert.AreEqual(true, parse("true").Literal);
            ClassicAssert.AreEqual(false, parse("f").Literal);
            ClassicAssert.AreEqual(42L, parse("42").Literal);
            ClassicAssert.AreEqual(1.5d, parse("1.5").Literal);
            ClassicAssert.IsNull(parse("none").Literal);
            ClassicAssert.AreEqual(CommandScalarKind.String, parse("hello").Kind);
        }

        [Test]
        public void TestQuotedStringsAndEscapes()
        {
            var node = (CommandScalarNode)CommandValueParser.Parse("\"hello world\\n\"").Node!;

            ClassicAssert.AreEqual("hello world\n", node.Text);
            ClassicAssert.IsTrue(node.Quoted);
        }

        [Test]
        public void TestSingleQuotedStrings()
        {
            var node = (CommandScalarNode)CommandValueParser.Parse("'hello world'").Node!;

            ClassicAssert.AreEqual("hello world", node.Text);
            ClassicAssert.IsTrue(node.Quoted);
        }

        [Test]
        public void TestArrayWithCommaAndWhitespaceSeparators()
        {
            var node = (CommandArrayNode)CommandValueParser.Parse("[nm, hr dt]").Node!;

            ClassicAssert.AreEqual(3, node.Items.Count);
            CollectionAssert.AreEqual(new[] { "nm", "hr", "dt" }, node.Items.Select(item => ((CommandScalarNode)item).Text).ToArray());
        }

        [Test]
        public void TestUnwrappedArraySequence()
        {
            var node = (CommandArrayNode)CommandValueParser.ParseSequence("nm hr,dt").Node!;

            ClassicAssert.AreEqual(3, node.Items.Count);
        }

        [Test]
        public void TestObjectColonSeparator()
        {
            var node = (CommandObjectNode)CommandValueParser.ParseUnwrappedObject("name: foo count: 2").Node!;

            ClassicAssert.AreEqual(2, node.Fields.Count);
            ClassicAssert.AreEqual("name", node.Fields[0].Key);
            ClassicAssert.AreEqual("foo", ((CommandScalarNode)node.Fields[0].Value).Text);
        }

        [Test]
        public void TestObjectEqualsIsNotAKeySeparator()
        {
            var result = CommandValueParser.ParseUnwrappedObject("name=foo");

            ClassicAssert.IsFalse(result.Complete);
        }

        [Test]
        public void TestUnterminatedArrayReportsIncomplete()
        {
            var result = CommandValueParser.Parse("[nm, hr");

            ClassicAssert.IsFalse(result.Complete);
            ClassicAssert.IsTrue(result.Errors.Count > 0);
        }

        [Test]
        public void TestSplitterProtectsQuotedSeparators()
        {
            var segments = CommandArgumentSplitter.Split("one \"two three\" [four five]");

            ClassicAssert.AreEqual(3, segments.Count);
            ClassicAssert.AreEqual("\"two three\"", segments[1].ValueText);
            ClassicAssert.AreEqual("[four five]", segments[2].ValueText);
        }

        [Test]
        public void TestSplitterNamedValue()
        {
            var segments = CommandArgumentSplitter.Split("name=foo count=2");

            ClassicAssert.AreEqual(2, segments.Count);
            ClassicAssert.AreEqual("name", segments[0].Name);
            ClassicAssert.AreEqual("foo", segments[0].ValueText);
        }

        private static CommandScalarNode parse(string text)
            => (CommandScalarNode)CommandValueParser.Parse(text).Node!;
    }
}
