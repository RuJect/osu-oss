// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Scripting;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class ScriptingRuntimeTest
    {
        private ScriptManager manager = null!;
        private TemporaryNativeStorage scriptStorage = null!;
        private TemporaryNativeStorage dataStorage = null!;

        [SetUp]
        public void SetUp()
        {
            scriptStorage = new TemporaryNativeStorage("scripts");
            dataStorage = new TemporaryNativeStorage("data");

            manager = new ScriptManager(null!, new OsuGame());
            manager.AttachStorage(new ScriptStorage(scriptStorage), dataStorage);
        }

        [TearDown]
        public void TearDown()
        {
            manager.Runner.StopAll();
            scriptStorage.Dispose();
            dataStorage.Dispose();
        }

        [Test]
        public void TestEveryReturnsIdAndCancel()
        {
            ScriptInstance instance = run("let id = osu.every(100, () => osu.log('tick')); osu.log('id=' + id); osu.cancel(id);");

            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
            ClassicAssert.IsTrue(logged("id=1"));
            ClassicAssert.IsFalse(logged("tick"));
        }

        [Test]
        public void TestOnceFiresOnlyOnce()
        {
            run("let n = 0; osu.once('x', () => { n++; osu.log('n=' + n); });");

            manager.Runner.Emit("x", Array.Empty<object?>());
            manager.Runner.Emit("x", Array.Empty<object?>());

            ClassicAssert.IsTrue(logged("n=1"));
            ClassicAssert.IsFalse(logged("n=2"));
        }

        [Test]
        public void TestRunReturnsHandleWithStop()
        {
            manager.Repository.CreateAnonymous("osu.every(1000, () => {});");

            run("let h = osu.run('anonymous-1'); osu.log('name=' + h.name); h.stop();");

            ClassicAssert.IsTrue(logged("name=anonymous-1"));
            ClassicAssert.IsFalse(manager.Runner.Running.Any(i => i.Name == "anonymous-1"));
        }

        [Test]
        public void TestStopByName()
        {
            manager.Repository.CreateAnonymous("osu.every(1000, () => {});");
            manager.Runner.RunByName("anonymous-1");

            run("osu.stop('anonymous-1');");

            ClassicAssert.IsFalse(manager.Runner.Running.Any(i => i.Name == "anonymous-1"));
        }

        [Test]
        public void TestWriteAndReadJson()
        {
            run("osu.storage.writeJson('data.json', { a: 1, b: 'x' }); let o = osu.storage.readJson('data.json'); osu.log('a=' + o.a + ',b=' + o.b);");

            ClassicAssert.IsTrue(logged("a=1,b=x"));
        }

        [Test]
        public void TestMkdirAndRecursiveDelete()
        {
            run("osu.storage.mkdir('dir/sub'); osu.storage.write('dir/sub/f.txt', 'hi'); osu.log('exists=' + osu.storage.exists('dir/sub/f.txt')); osu.storage.delete('dir'); osu.log('gone=' + !osu.storage.exists('dir/sub/f.txt'));");

            ClassicAssert.IsTrue(logged("exists=true"));
            ClassicAssert.IsTrue(logged("gone=true"));
        }

        [Test]
        public void TestScriptLifecycleEvents()
        {
            run("osu.on('scriptStarted', n => osu.log('started:' + n)); osu.on('scriptStopped', n => osu.log('stopped:' + n));", "observer");

            ScriptInstance worker = run("osu.every(1000, () => {});", "worker");
            manager.Runner.Stop(worker);

            ClassicAssert.IsTrue(logged("started:worker"));
            ClassicAssert.IsTrue(logged("stopped:worker"));
        }

        private ScriptInstance run(string source, string name = "test") => manager.Runner.Run(new ScriptDefinition(name, null, source));

        private bool logged(string text) => manager.Output.History.Any(o => o.Text == text);
    }
}
