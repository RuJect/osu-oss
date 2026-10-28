// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Commands;

namespace osu.Game.Tests.API.Commands
{
    [TestFixture]
    public class CommandSuggestionTest
    {
        private CommandRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            registry = new CommandRegistry();
        }

        [Test]
        public void TestArrayElementSuggestionsFilterByCurrentText()
        {
            var command = registry.Add("mods")
                                  .Argument<string[]>("mods", argument => argument.SuggestElements(_ => new[]
                                  {
                                      new CommandSuggestion("nm", "No Fail"),
                                      new CommandSuggestion("hr", "Hard Rock"),
                                      new CommandSuggestion("dt", "Double Time"),
                                  }))
                                  .Build(_ => { });

            var completion = CommandCompletionService.GetCompletion(command, "nm h");

            ClassicAssert.AreEqual("h", completion.CurrentText);
            ClassicAssert.AreEqual(1, completion.Suggestions.Count);
            ClassicAssert.AreEqual("hr", completion.Suggestions[0].InsertText);
            ClassicAssert.AreEqual("nm hr", insert(completion, "nm h"));
        }

        [Test]
        public void TestSuggestionsReplaceWholeSingleStringArgument()
        {
            var command = registry.Add("skin")
                                  .Argument<string>("name", argument => argument.Suggest(_ => new[]
                                  {
                                      CommandSuggestions.String("Argon"),
                                      CommandSuggestions.String("Argon Pro"),
                                  }))
                                  .Build(_ => { });

            var completion = CommandCompletionService.GetCompletion(command, "Ar");

            ClassicAssert.AreEqual(2, completion.Suggestions.Count);
            ClassicAssert.AreEqual("Argon", insert(completion, "Ar"));
        }

        [Test]
        public void TestObjectKeySuggestionsReplaceCurrentKey()
        {
            var command = registry.Add("opt")
                                  .Argument<Dictionary<string, int>>("values", argument => argument.SuggestFields(context => context.IsKey
                                      ? context.Suggest("alpha", "beta")
                                      : Enumerable.Empty<CommandSuggestion>()))
                                  .Build(_ => { });

            var completion = CommandCompletionService.GetCompletion(command, "al");

            ClassicAssert.AreEqual("al", completion.CurrentText);
            ClassicAssert.AreEqual(1, completion.Suggestions.Count);
            ClassicAssert.AreEqual("alpha", insert(completion, "al"));
        }

        [Test]
        public void TestObjectValueSuggestions()
        {
            var command = registry.Add("opt")
                                  .Argument<Dictionary<string, int>>("values", argument => argument.SuggestFields(context => context.IsKey
                                      ? Enumerable.Empty<CommandSuggestion>()
                                      : context.Suggest("1", "2")))
                                  .Build(_ => { });

            var completion = CommandCompletionService.GetCompletion(command, "a: 2");

            ClassicAssert.AreEqual("2", completion.CurrentText);
            ClassicAssert.AreEqual(1, completion.Suggestions.Count);
            ClassicAssert.AreEqual("a: 2", insert(completion, "a: 2"));
        }

        [Test]
        public void TestBoolAndEnumSuggestionsAreAutomatic()
        {
            var boolCommand = registry.Add("flag")
                                      .Argument<bool>("value")
                                      .Build(_ => { });

            var boolCompletion = CommandCompletionService.GetCompletion(boolCommand, string.Empty);

            CollectionAssert.Contains(boolCompletion.Suggestions.Select(s => s.InsertText).ToList(), "true");

            var enumCommand = registry.Add("mode")
                                      .Argument<Mode>("mode")
                                      .Build(_ => { });

            var enumCompletion = CommandCompletionService.GetCompletion(enumCommand, string.Empty);

            CollectionAssert.Contains(enumCompletion.Suggestions.Select(s => s.InsertText).ToList(), nameof(Mode.Fast));
        }

        [Test]
        public void TestObjectFieldsAreSuggestedAutomatically()
        {
            var command = registry.Add("opt")
                                  .Argument<Options>("options")
                                  .Build(_ => { });

            var keyCompletion = CommandCompletionService.GetCompletion(command, "na");

            ClassicAssert.AreEqual("na", keyCompletion.CurrentText);
            CollectionAssert.Contains(keyCompletion.Suggestions.Select(s => s.InsertText).ToList(), "name");

            var valueCompletion = CommandCompletionService.GetCompletion(command, "enabled: ");

            CollectionAssert.Contains(valueCompletion.Suggestions.Select(s => s.InsertText).ToList(), "true");
            ClassicAssert.AreEqual("enabled: true", insert(valueCompletion, "enabled: "));
        }

        [Test]
        public void TestEmptyInputSuggestsFirstArgument()
        {
            var command = registry.Add("mods")
                                  .Argument<string[]>("mods", argument => argument.SuggestElements(_ => new[]
                                  {
                                      new CommandSuggestion("nm"),
                                  }))
                                  .Build(_ => { });

            var completion = CommandCompletionService.GetCompletion(command, string.Empty);

            ClassicAssert.IsNotNull(completion.Argument);
            ClassicAssert.AreEqual("mods", completion.Argument!.Name);
            ClassicAssert.AreEqual(1, completion.Suggestions.Count);
        }

        [Test]
        public void TestNamedArgumentSuggestionsUseValueSpan()
        {
            var command = registry.Add("add")
                                  .Argument<string>("name")
                                  .Argument<string[]>("mods", argument => argument.SuggestElements(_ => new[]
                                  {
                                      new CommandSuggestion("nm"),
                                  }))
                                  .Build(_ => { });

            var completion = CommandCompletionService.GetCompletion(command, "name=foo mods=n");

            ClassicAssert.AreEqual("n", completion.CurrentText);
            ClassicAssert.AreEqual("name=foo mods=nm", insert(completion, "name=foo mods=n"));
        }

        private static string insert(CommandCompletion completion, string argument)
            => argument[..completion.ReplacementSpan.Start]
               + completion.Suggestions[0].InsertText
               + argument[completion.ReplacementSpan.End..];

        private enum Mode
        {
            Slow,
            Fast,
        }

        private class Options
        {
            public string Name { get; set; } = string.Empty;

            public bool Enabled { get; set; }
        }
    }
}
