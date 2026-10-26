// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Handlers;
using osu.Framework.Testing;
using osu.Game.Events;
using osu.Game.API.Events;
using osu.Game.API.Scripting;
using osu.Game.API.Services;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public partial class ScriptingRuntimeTest
    {
        private ScriptManager manager = null!;
        private TemporaryNativeStorage scriptStorage = null!;
        private TemporaryNativeStorage dataStorage = null!;
        private TestInputManager inputManager = null!;
        private OsuEventBus eventBus = null!;

        [SetUp]
        public void SetUp()
        {
            scriptStorage = new TemporaryNativeStorage("scripts");
            dataStorage = new TemporaryNativeStorage("data");

            eventBus = new OsuEventBus();
            manager = new ScriptManager(null!, new OsuGame(), eventBus);
            manager.AttachStorage(new ScriptStorage(scriptStorage), dataStorage);

            inputManager = new TestInputManager();
            manager.Services.InputManager = inputManager;
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
        public void TestDisabledScriptingDoesNotRun()
        {
            manager.Runner.Enabled = false;
            manager.Repository.CreateAnonymous("osu.log('should not run');");

            ScriptInstance? instance = manager.RunByName("anonymous-1");

            ClassicAssert.IsNotNull(instance);
            ClassicAssert.AreEqual(ScriptExecutionState.Stopped, instance.State);
            ClassicAssert.IsFalse(manager.Runner.Running.Any(i => i.Name == "anonymous-1"));
            ClassicAssert.IsFalse(logged("should not run"));
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
        public void TestStorageTraversalIsRejected()
        {
            string outsideFile = Path.Combine(dataStorage.GetFullPath(string.Empty), "..", $"outside-{Guid.NewGuid():N}.txt");
            string name = Path.GetFileName(outsideFile);
            File.WriteAllText(outsideFile, "secret");

            try
            {
                run($"osu.storage.write('../{name}', 'overwritten'); osu.storage.append('../{name}', 'appended'); osu.storage.delete('../{name}'); osu.kv.set('../{name}', 'value'); osu.log('exists=' + osu.storage.exists('../{name}')); osu.log('read=' + osu.storage.read('../{name}')); osu.log('list=' + osu.storage.list('../').join(''));");

                ClassicAssert.IsTrue(logged("exists=false"));
                ClassicAssert.IsTrue(logged("read=undefined"));
                ClassicAssert.IsTrue(logged("list="));
                ClassicAssert.IsTrue(File.Exists(outsideFile));
                ClassicAssert.AreEqual("secret", File.ReadAllText(outsideFile));
            }
            finally
            {
                if (File.Exists(outsideFile))
                    File.Delete(outsideFile);
            }
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
            manager.Services.Config = new ConfigService(new OsuConfigManager(new TemporaryNativeStorage("config")));

            run("osu.config.set('Username', 'player'); osu.log('name=' + osu.config.get('Username')); osu.config.set('ShowConvertedBeatmaps', false); osu.log('conv=' + osu.config.get('ShowConvertedBeatmaps'));");

            ClassicAssert.IsTrue(logged("name=player"));
            ClassicAssert.IsTrue(logged("conv=false"));
        }

        [Test]
        public void TestNotifyProgress()
        {
            var overlay = new TestNotificationOverlay();
            manager.Services.Notifications = new NotificationService(overlay);

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

        [Test]
        public void TestCursorMoveToInstant()
        {
            run("osu.cursor.moveTo(100, 200); osu.log('pos=' + osu.cursor.position().x + ',' + osu.cursor.position().y);");

            ClassicAssert.IsTrue(logged("pos=100,200"));
            ClassicAssert.AreEqual(new Vector2(100, 200), inputManager.CurrentState.Mouse.Position);
        }

        [Test]
        public void TestCursorMoveOverDuration()
        {
            run("osu.cursor.moveTo(100, 0, 100);", "cursor");

            manager.Runner.Tick(0);
            ClassicAssert.AreEqual(0, inputManager.CurrentState.Mouse.Position.X);

            manager.Runner.Tick(50);
            ClassicAssert.AreEqual(50, inputManager.CurrentState.Mouse.Position.X, 1);

            manager.Runner.Tick(100);
            ClassicAssert.AreEqual(100, inputManager.CurrentState.Mouse.Position.X);
        }

        [Test]
        public void TestCursorMoveAtSpeed()
        {
            run("osu.cursor.moveAtSpeed(100, 0, 1000);", "cursor");

            manager.Runner.Tick(0);
            manager.Runner.Tick(50);

            ClassicAssert.AreEqual(50, inputManager.CurrentState.Mouse.Position.X, 1);

            manager.Runner.Tick(100);
            ClassicAssert.AreEqual(100, inputManager.CurrentState.Mouse.Position.X);
        }

        [Test]
        public void TestKeyboardDownAndUp()
        {
            run("osu.keyboard.down(['ctrl', 's']); osu.log('down=' + osu.keyboard.isDown('ctrl+s')); osu.keyboard.up('ctrl+s'); osu.log('up=' + osu.keyboard.isDown('ctrl+s'));");

            ClassicAssert.IsTrue(logged("down=true"));
            ClassicAssert.IsTrue(logged("up=false"));
            ClassicAssert.IsFalse(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.S));
            ClassicAssert.IsFalse(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.LControl));
        }

        [Test]
        public void TestKeyboardPressDuration()
        {
            run("osu.keyboard.press('a', 100);", "keyboard");

            manager.Runner.Tick(0);
            ClassicAssert.IsTrue(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));

            manager.Runner.Tick(50);
            ClassicAssert.IsTrue(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));

            manager.Runner.Tick(100);
            ClassicAssert.IsFalse(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));
        }

        [Test]
        public void TestKeyboardListeners()
        {
            run("osu.keyboard.onPress(k => osu.log('pressed:' + k)); osu.keyboard.onRelease(k => osu.log('released:' + k));", "keyboard");

            manager.Runner.Tick(0);

            inputManager.CurrentState.Keyboard.Keys.SetPressed(Key.A, true);
            manager.Runner.Tick(50);
            ClassicAssert.IsTrue(logged("pressed:A"));

            inputManager.CurrentState.Keyboard.Keys.SetPressed(Key.A, false);
            manager.Runner.Tick(100);
            ClassicAssert.IsTrue(logged("released:A"));
        }

        [Test]
        public void TestKeyboardReleasesOwnedKeysOnStop()
        {
            ScriptInstance instance = run("osu.keyboard.down('a'); osu.every(1000, () => {});", "keyboard");

            ClassicAssert.IsTrue(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));

            manager.Runner.Stop(instance);

            ClassicAssert.IsFalse(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));
        }

        [Test]
        public void TestKeyboardNoOpWhenUnavailable()
        {
            manager.Services.InputManager = null;

            run("osu.keyboard.down('a'); osu.log('down=' + osu.keyboard.isDown('a')); osu.log('pos=' + (osu.cursor.position() === undefined));");

            ClassicAssert.IsTrue(logged("down=false"));
            ClassicAssert.IsTrue(logged("pos=true"));
        }

        [Test]
        public void TestEmptyScriptCompletes()
        {
            ScriptInstance instance = run(string.Empty, "empty");

            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitSleep()
        {
            ScriptInstance instance = run("osu.log('before'); await osu.sleep(100); osu.log('after');", "async");

            ClassicAssert.AreEqual(ScriptExecutionState.Running, instance.State);
            ClassicAssert.IsTrue(logged("before"));

            manager.Runner.Tick(0);
            manager.Runner.Tick(200);

            ClassicAssert.IsTrue(logged("after"));
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitCursorMove()
        {
            ScriptInstance instance = run("await osu.cursor.moveTo(100, 0, 100); osu.log('done');", "cursor");

            ClassicAssert.AreEqual(ScriptExecutionState.Running, instance.State);

            manager.Runner.Tick(0);
            ClassicAssert.AreEqual(0, inputManager.CurrentState.Mouse.Position.X);

            manager.Runner.Tick(100);

            ClassicAssert.AreEqual(100, inputManager.CurrentState.Mouse.Position.X);
            ClassicAssert.IsTrue(logged("done"));
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitSequentialCursorMoves()
        {
            ScriptInstance instance = run("await osu.cursor.moveTo(100, 0, 100); await osu.cursor.moveTo(200, 0, 100); osu.log('done');", "cursor");

            manager.Runner.Tick(0);
            manager.Runner.Tick(100);
            ClassicAssert.AreEqual(100, inputManager.CurrentState.Mouse.Position.X);

            manager.Runner.Tick(200);
            ClassicAssert.AreEqual(200, inputManager.CurrentState.Mouse.Position.X);
            ClassicAssert.IsTrue(logged("done"));
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitMoveAtSpeed()
        {
            ScriptInstance instance = run("await osu.cursor.moveAtSpeed(100, 0, 1000); osu.log('done');", "cursor");

            manager.Runner.Tick(0);
            manager.Runner.Tick(50);
            ClassicAssert.AreEqual(50, inputManager.CurrentState.Mouse.Position.X, 1);

            manager.Runner.Tick(100);
            ClassicAssert.AreEqual(100, inputManager.CurrentState.Mouse.Position.X);
            ClassicAssert.IsTrue(logged("done"));
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestSupersededCursorMoveResolvesAwait()
        {
            ScriptInstance instance = run(
                "osu.after(50, () => osu.cursor.moveTo(200, 0, 100)); await osu.cursor.moveTo(100, 0, 1000); osu.log('first'); await osu.cursor.moveTo(300, 0, 100); osu.log('second');",
                "cursor");

            manager.Runner.Tick(0);
            manager.Runner.Tick(50);

            ClassicAssert.IsTrue(logged("first"));

            manager.Runner.Tick(150);

            ClassicAssert.IsTrue(logged("second"));
            ClassicAssert.AreEqual(300, inputManager.CurrentState.Mouse.Position.X);
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitKeyboardPress()
        {
            ScriptInstance instance = run("await osu.keyboard.press('a', 100); osu.log('done');", "keyboard");

            ClassicAssert.AreEqual(ScriptExecutionState.Running, instance.State);

            manager.Runner.Tick(0);
            ClassicAssert.IsTrue(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));

            manager.Runner.Tick(100);

            ClassicAssert.IsFalse(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));
            ClassicAssert.IsTrue(logged("done"));
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitKeyboardPressEarlyUp()
        {
            ScriptInstance instance = run("osu.after(50, () => osu.keyboard.up('a')); await osu.keyboard.press('a', 1000); osu.log('done');", "keyboard");

            manager.Runner.Tick(0);
            ClassicAssert.IsTrue(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));

            manager.Runner.Tick(50);

            ClassicAssert.IsFalse(inputManager.CurrentState.Keyboard.Keys.IsPressed(Key.A));
            ClassicAssert.IsTrue(logged("done"));
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitRejectionFailsScript()
        {
            ScriptInstance instance = run("await osu.sleep(100); throw new Error('boom');", "async");

            manager.Runner.Tick(0);
            manager.Runner.Tick(200);

            ClassicAssert.AreEqual(ScriptExecutionState.Error, instance.State);
            ClassicAssert.IsTrue(manager.Output.History.Any(o => o.Text.Contains("boom")));
        }

        [Test]
        public void TestModuleSyntaxErrorFailsScript()
        {
            ScriptInstance instance = run("await", "bad");

            ClassicAssert.AreEqual(ScriptExecutionState.Error, instance.State);
        }

        [Test]
        public void TestTopLevelAwaitNoInputCompletes()
        {
            manager.Services.InputManager = null;

            ScriptInstance instance = run("await osu.cursor.moveTo(1, 1); await osu.keyboard.press('a'); osu.log('done');", "async");

            ClassicAssert.IsTrue(logged("done"));
            ClassicAssert.AreEqual(ScriptExecutionState.Completed, instance.State);
        }

        [Test]
        public void TestScreenEventFromSharedBusReachesScript()
        {
            run("osu.on('screen', n => osu.log('screen:' + n));", "observer");

            manager.Events.PublishScreen("SongSelect");

            ClassicAssert.IsTrue(logged("screen:SongSelect"));
        }

        [Test]
        public void TestLifecycleEventIsPublishedOnSharedBus()
        {
            ScriptStartedEvent? received = null;
            using (eventBus.Subscribe<ScriptStartedEvent>(e => received = e))
                run("osu.every(1000, () => {});", "worker");

            ClassicAssert.IsNotNull(received);
            ClassicAssert.AreEqual("worker", received!.Name);
        }

        [Test]
        public void TestScriptErrorEventCarriesMessage()
        {
            ScriptErrorEvent? received = null;

            using (eventBus.Subscribe<ScriptErrorEvent>(e => received = e))
            {
                run("osu.every(50, () => { throw new Error('boom'); });", "faulty");

                manager.Runner.Tick(0);
                manager.Runner.Tick(100);
            }

            ClassicAssert.IsNotNull(received);
            ClassicAssert.AreEqual("faulty", received!.Name);
            ClassicAssert.AreEqual("boom", received.Message);
        }

        private ScriptInstance run(string source, string name = "test") => manager.Runner.Run(new ScriptDefinition(name, null, source));

        private bool logged(string text) => manager.Output.History.Any(o => o.Text == text);

        private partial class TestInputManager : InputManager
        {
            protected override ImmutableArray<InputHandler> InputHandlers => ImmutableArray<InputHandler>.Empty;
        }

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
