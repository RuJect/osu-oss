// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Scripting;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class ScriptLocatorTest
    {
        private TemporaryNativeStorage storage = null!;
        private ScriptRepository repository = null!;
        private ScriptLocator locator = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("script-locator");
            repository = new ScriptRepository(new ScriptOutputLog());
            locator = new ScriptLocator(repository);
        }

        [TearDown]
        public void TearDown()
        {
            storage.Dispose();
        }

        [Test]
        public void TestFindByNameReturnsNullWhenMissing()
        {
            ClassicAssert.IsNull(locator.FindByName("missing"));
        }

        [Test]
        public void TestFindByNameReturnsAnonymousDefinition()
        {
            ScriptDefinition definition = repository.CreateAnonymous("console.log('hi')");

            ClassicAssert.AreSame(definition, locator.FindByName("anonymous-1"));
            ClassicAssert.IsNull(locator.FindByName("anonymous-2"));
        }

        [Test]
        public void TestLoadFromFileReturnsNullWithoutStorage()
        {
            ClassicAssert.IsNull(locator.LoadFromFile("foo.js"));
        }

        [Test]
        public void TestLoadFromFileReturnsNullWhenMissing()
        {
            repository.Storage = new ScriptStorage(storage);

            ClassicAssert.IsNull(locator.LoadFromFile("missing.js"));
        }

        [Test]
        public void TestLoadFromFileRejectsTraversal()
        {
            repository.Storage = new ScriptStorage(storage);

            ClassicAssert.IsNull(locator.LoadFromFile("../outside.js"));
        }

        [Test]
        public void TestLoadFromFileReadsSource()
        {
            var scriptStorage = new ScriptStorage(storage);
            scriptStorage.Write("hello.js", "osu.log('hi')");
            repository.Storage = scriptStorage;

            ScriptDefinition? definition = locator.LoadFromFile("hello.js");

            ClassicAssert.IsNotNull(definition);
            ClassicAssert.AreEqual("hello", definition!.Name);
            ClassicAssert.AreEqual("hello.js", definition.FileName);
            ClassicAssert.AreEqual("osu.log('hi')", definition.Source);
        }
    }
}
