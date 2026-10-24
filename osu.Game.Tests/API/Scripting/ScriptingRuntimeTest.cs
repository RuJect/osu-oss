// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.API.Scripting;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

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

        [Test]
        public void TestSetTimeoutFires()
        {
            ScriptInstance instance = run("osu.setTimeout(() => osu.log('fired'), 100);", "timer");

            ClassicAssert.AreEqual(ScriptExecutionState.Running, instance.State);

            manager.Runner.Tick(0);
            manager.Runner.Tick(200);

            ClassicAssert.IsTrue(logged("fired"));
        }

        [Test]
        public void TestClearTimeout()
        {
            ScriptInstance instance = run("let id = osu.setTimeout(() => osu.log('never'), 100); osu.clearTimeout(id);", "timer");

            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);

            manager.Runner.Tick(0);
            manager.Runner.Tick(200);

            ClassicAssert.IsFalse(logged("never"));
        }

        [Test]
        public void TestSetIntervalRepeats()
        {
            run("let n = 0; osu.setInterval(() => { n++; osu.log('n=' + n); }, 100);", "timer");

            manager.Runner.Tick(0);
            manager.Runner.Tick(150);
            manager.Runner.Tick(250);

            ClassicAssert.IsTrue(logged("n=1"));
            ClassicAssert.IsTrue(logged("n=2"));
            ClassicAssert.IsFalse(logged("n=3"));
        }

        [Test]
        public void TestSleep()
        {
            run("(async () => { osu.log('before'); await osu.sleep(100); osu.log('after'); })();", "sleeper");

            ClassicAssert.IsTrue(logged("before"));

            manager.Runner.Tick(0);
            manager.Runner.Tick(200);

            ClassicAssert.IsTrue(logged("after"));
        }

        [Test]
        public void TestApiProviderStateIsPerScriptInstance()
        {
            run("osu.setTimeout(() => osu.log('from-first'), 100);", "first");
            run(string.Empty, "second");

            manager.Runner.Tick(0);
            manager.Runner.Tick(200);

            ClassicAssert.IsTrue(manager.Output.History.Any(o => o.Text == "from-first" && o.Source == "first"));
        }

        [Test]
        public void TestKvStore()
        {
            run("osu.kv.set('name', 'osu'); osu.kv.set('count', 42); osu.log('name=' + osu.kv.get('name')); osu.log('count=' + osu.kv.get('count')); osu.kv.delete('count'); osu.log('deleted=' + (osu.kv.get('count') === undefined));");

            ClassicAssert.IsTrue(logged("name=osu"));
            ClassicAssert.IsTrue(logged("count=42"));
            ClassicAssert.IsTrue(logged("deleted=true"));
        }

        [Test]
        public void TestEach()
        {
            run("let sum = 0; osu.each([1, 2, 3], n => sum += n); osu.log('sum=' + sum);");

            ClassicAssert.IsTrue(logged("sum=6"));
        }

        [Test]
        public void TestConfigGetAndSet()
        {
            manager.Services.Config = new OsuConfigManager(new TemporaryNativeStorage("config"));

            run("osu.config.set('Username', 'player'); osu.log('name=' + osu.config.get('Username')); osu.config.set('ShowConvertedBeatmaps', false); osu.log('conv=' + osu.config.get('ShowConvertedBeatmaps'));");

            ClassicAssert.IsTrue(logged("name=player"));
            ClassicAssert.IsTrue(logged("conv=false"));
        }

        [Test]
        public void TestNotifyProgress()
        {
            var overlay = new TestNotificationOverlay();
            manager.Services.Notifications = overlay;

            run("let p = osu.notify.progress('Loading'); osu.log('type=' + typeof p.setProgress); p.setProgress(0.5); p.complete();");

            ClassicAssert.IsTrue(logged("type=function"));
            ClassicAssert.AreEqual(1, overlay.Posted.Count);
        }

        [Test]
        public void TestMusicNoOpWhenUnavailable()
        {
            run("osu.log('playing=' + osu.music.isPlaying()); osu.music.play();");

            ClassicAssert.IsTrue(logged("playing=false"));
        }

        [Test]
        public void TestUiNoOpWhenUnavailable()
        {
            run("let o = osu.ui.overlay('Test'); osu.log('overlay=' + (o === undefined));");

            ClassicAssert.IsTrue(logged("overlay=true"));
        }

        private ScriptInstance run(string source, string name = "test") => manager.Runner.Run(new ScriptDefinition(name, null, source));

        private bool logged(string text) => manager.Output.History.Any(o => o.Text == text);

        private class TestNotificationOverlay : INotificationOverlay
        {
            public readonly List<Notification> Posted = new();

            public void Post(Notification notification) => Posted.Add(notification);

            public void Hide()
            {
            }

            public IBindable<int> UnreadCount { get; } = new Bindable<int>();

            public IEnumerable<Notification> AllNotifications => Posted;
        }
    }
}
