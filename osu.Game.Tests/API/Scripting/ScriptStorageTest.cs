// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Scripting;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class ScriptStorageTest
    {
        [Test]
        public void TestEnsureInitialisedWritesMagicScriptsAndExamples()
        {
            using var storage = new TemporaryNativeStorage("scripts");
            var scriptStorage = new ScriptStorage(storage);

            scriptStorage.EnsureInitialised();

            string[] scripts = scriptStorage.EnumerateScripts().ToArray();

            ClassicAssert.IsTrue(scripts.Contains("on_launch.js"));
            ClassicAssert.IsTrue(scripts.Contains("on_exit.js"));

            foreach (string name in new[] { "hello", "storage", "timers", "events", "config", "music", "notifications", "ui", "ui-menu" })
                ClassicAssert.IsTrue(scripts.Contains($"examples/{name}.js"), $"missing examples/{name}.js");
        }

        [Test]
        public void TestEnumerateScriptsIsRecursive()
        {
            using var storage = new TemporaryNativeStorage("scripts");
            var scriptStorage = new ScriptStorage(storage);

            scriptStorage.Write("root.js", "// root");
            scriptStorage.Write("examples/nested.js", "// nested");

            string[] scripts = scriptStorage.EnumerateScripts().ToArray();

            ClassicAssert.IsTrue(scripts.Contains("root.js"));
            ClassicAssert.IsTrue(scripts.Contains("examples/nested.js"));
        }

        [Test]
        public void TestEnsureInitialisedDoesNotOverwrite()
        {
            using var storage = new TemporaryNativeStorage("scripts");
            var scriptStorage = new ScriptStorage(storage);

            scriptStorage.Write("examples/hello.js", "// custom");
            scriptStorage.EnsureInitialised();

            ClassicAssert.AreEqual("// custom", scriptStorage.Read("examples/hello.js"));
        }
    }
}
