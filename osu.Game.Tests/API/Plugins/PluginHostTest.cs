// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Events;
using osu.Game.API.Plugins;
using osu.Game.API.Services;
using osu.Game.Events;

namespace osu.Game.Tests.API.Plugins
{
    [TestFixture]
    public class PluginHostTest
    {
        private OutputLog output = null!;
        private OsuEventBus bus = null!;
        private PluginHost host = null!;
        private TemporaryNativeStorage logsStorage = null!;
        private string logsRoot = null!;

        [SetUp]
        public void SetUp()
        {
            output = new OutputLog();
            bus = new OsuEventBus();
            logsStorage = new TemporaryNativeStorage("plugin-host-logs");
            logsRoot = logsStorage.GetFullPath(string.Empty);

            var game = new OsuGame();
            host = new PluginHost(bus, _ => new ApiServices(game, null!, output),
                manifest => new PluginLogger(manifest.Id, logsStorage));
        }

        [TearDown]
        public void TearDown()
        {
            logsStorage.Dispose();
        }

        [Test]
        public void TestLoadInvokesPluginWithContext()
        {
            var plugin = new RecordingPlugin();

            LoadedPlugin entry = load(plugin);

            ClassicAssert.AreSame(plugin, entry.Plugin);
            ClassicAssert.IsNotNull(plugin.Context);
            ClassicAssert.AreEqual(PluginState.Running, entry.State);
            ClassicAssert.IsTrue(host.Loaded.Contains(entry));
            ClassicAssert.AreEqual("test", entry.Name);
        }

        [Test]
        public void TestContextExposesServicesAndVersion()
        {
            var plugin = new RecordingPlugin();
            load(plugin);

            ClassicAssert.IsNotNull(plugin.Context);
            ClassicAssert.IsNotNull(plugin.Context!.Services.Output);
            ClassicAssert.IsNotNull(plugin.Context.Events);
            ClassicAssert.IsNotNull(plugin.Context.Scheduler);
            ClassicAssert.IsNotNull(plugin.Context.Logger);
            ClassicAssert.IsNotNull(plugin.Context.Manifest);
        }

        [Test]
        public void TestLoadPublishesLifecycleEvents()
        {
            var received = new System.Collections.Generic.List<OsuApiEvent>();
            using (bus.SubscribeAll(received.Add))
                load(new RecordingPlugin());

            ClassicAssert.AreEqual(2, received.Count);
            ClassicAssert.IsInstanceOf<PluginLoadingEvent>(received[0]);
            ClassicAssert.IsInstanceOf<PluginLoadedEvent>(received[1]);
        }

        [Test]
        public void TestUnloadPublishesLifecycleEvents()
        {
            LoadedPlugin entry = load(new RecordingPlugin());

            var received = new System.Collections.Generic.List<OsuApiEvent>();
            using (bus.SubscribeAll(received.Add))
                host.Unload(entry);

            ClassicAssert.AreEqual(2, received.Count);
            ClassicAssert.IsInstanceOf<PluginUnloadingEvent>(received[0]);
            ClassicAssert.IsInstanceOf<PluginUnloadedEvent>(received[1]);
        }

        [Test]
        public void TestTickAdvancesPluginScheduler()
        {
            var plugin = new RecordingPlugin();
            plugin.OnLoad = context => context.Scheduler.AddUpdateHandler(() => plugin.Ticks++);
            load(plugin);

            host.Tick(0);
            host.Tick(16);

            ClassicAssert.AreEqual(2, plugin.Ticks);
        }

        [Test]
        public void TestSchedulerIsPerPlugin()
        {
            var first = new RecordingPlugin();
            var second = new RecordingPlugin();
            first.OnLoad = c => c.Scheduler.AddUpdateHandler(() => first.Ticks++);
            second.OnLoad = c => c.Scheduler.AddUpdateHandler(() => second.Ticks++);

            load(first);
            load(second);

            host.Tick(0);

            ClassicAssert.AreEqual(1, first.Ticks);
            ClassicAssert.AreEqual(1, second.Ticks);
        }

        [Test]
        public void TestUnloadInvokesUnloadAndRemoves()
        {
            var plugin = new RecordingPlugin();
            LoadedPlugin entry = load(plugin);

            host.Unload(entry);

            ClassicAssert.IsTrue(plugin.Unloaded);
            ClassicAssert.AreEqual(PluginState.Stopped, entry.State);
            ClassicAssert.IsFalse(host.Loaded.Contains(entry));
        }

        [Test]
        public void TestLoadFailureIsRecordedAndLogged()
        {
            var plugin = new RecordingPlugin { OnLoad = _ => throw new InvalidOperationException("boom") };

            LoadedPlugin entry = load(plugin);

            ClassicAssert.AreEqual(PluginState.Error, entry.State);
            ClassicAssert.IsTrue(output.History.Any(o => o.Text.Contains("boom")));
        }

        [Test]
        public void TestTickFailureStopsSchedulingFurtherTicks()
        {
            var plugin = new RecordingPlugin();
            plugin.OnLoad = c => c.Scheduler.AddUpdateHandler(() => throw new InvalidOperationException("boom"));
            LoadedPlugin entry = load(plugin);

            host.Tick(0);

            ClassicAssert.AreEqual(PluginState.Error, entry.State);
        }

        [Test]
        public void TestUnloadAll()
        {
            var first = new RecordingPlugin();
            var second = new RecordingPlugin();

            load(first);
            load(second);
            host.UnloadAll();

            ClassicAssert.IsTrue(first.Unloaded);
            ClassicAssert.IsTrue(second.Unloaded);
            ClassicAssert.IsEmpty(host.Loaded);
        }

        [Test]
        public void TestShutdownUnloadsInReverseOrder()
        {
            var order = new System.Collections.Generic.List<string>();

            using (bus.Subscribe<PluginUnloadingEvent>(e => order.Add(e.Manifest.Id)))
            {
                host.LoadSystemPlugin(new PluginManifest
                {
                    Id = PluginManifest.SYSTEM_ID,
                    Version = PluginApi.VERSION,
                    ApiVersion = PluginApi.VERSION,
                    Permissions = new PluginManifest.PluginPermissions { Trusted = true },
                });

                load(new RecordingPlugin());

                host.Shutdown();
            }

            ClassicAssert.AreEqual(new[] { "test", PluginManifest.SYSTEM_ID }, order);
            ClassicAssert.IsEmpty(host.Loaded);
        }

        [Test]
        public void TestShutdownIsIdempotent()
        {
            var plugin = new RecordingPlugin();
            load(plugin);

            host.Shutdown();
            host.Shutdown();

            ClassicAssert.AreEqual(1, plugin.UnloadCount);
            ClassicAssert.IsTrue(plugin.Unloaded);
            ClassicAssert.IsEmpty(host.Loaded);
        }

        [Test]
        public void TestSubscriptionRequiresBusPermission()
        {
            var receiver = new RecordingPlugin();
            receiver.OnLoad = context => context.Events.Subscribe<TestEvent>(_ => receiver.EventsReceived++);

            load(receiver);

            bus.Publish(new TestEvent());

            ClassicAssert.AreEqual(0, receiver.EventsReceived);
        }

        [Test]
        public void TestPublishRequiresBusPermission()
        {
            var receiver = new RecordingPlugin();
            LoadedPlugin entry = load(receiver);

            using (bus.Subscribe<TestEvent>(_ => receiver.EventsReceived++))
                entry.Context.Events.Publish(new TestEvent());

            ClassicAssert.AreEqual(0, receiver.EventsReceived);
        }

        [Test]
        public void TestBusPermissionGrantsSubscriptionsAndPublishing()
        {
            var receiver = new RecordingPlugin();

            receiver.OnLoad = context =>
            {
                context.Events.Subscribe<TestEvent>(_ => receiver.EventsReceived++);
                context.Events.Publish(new TestEvent());
            };

            load(receiver, new PluginManifest.PluginPermissions { Bus = true });

            ClassicAssert.AreEqual(1, receiver.EventsReceived);
        }

        [Test]
        public void TestSubscriptionsAreDetachedOnUnload()
        {
            var plugin = new RecordingPlugin();
            plugin.OnLoad = context => context.Events.Subscribe<TestEvent>(_ => plugin.EventsReceived++);

            LoadedPlugin entry = load(plugin, new PluginManifest.PluginPermissions { Trusted = true });

            bus.Publish(new TestEvent());
            ClassicAssert.AreEqual(1, plugin.EventsReceived);

            host.Unload(entry);
            bus.Publish(new TestEvent());

            ClassicAssert.AreEqual(1, plugin.EventsReceived);
        }

        [Test]
        public void TestEventHandlerExceptionIsLoggedAndPublished()
        {
            var plugin = new RecordingPlugin();
            plugin.OnLoad = context => context.Events.Subscribe<TestEvent>(_ => throw new InvalidOperationException("handler boom"));
            LoadedPlugin entry = load(plugin, new PluginManifest.PluginPermissions { Bus = true });

            var errors = new System.Collections.Generic.List<PluginErrorEvent>();

            using (bus.Subscribe<PluginErrorEvent>(errors.Add))
                bus.Publish(new TestEvent());

            ClassicAssert.AreEqual(PluginState.Running, entry.State);
            ClassicAssert.AreEqual(1, errors.Count);
            ClassicAssert.AreEqual("test", errors[0].Manifest.Id);
            ClassicAssert.IsTrue(errors[0].Message.Contains("handler boom"));
            ClassicAssert.IsTrue(File.ReadAllText(Path.Combine(logsRoot, "plugin.log")).Contains("handler boom"));
        }

        [Test]
        public void TestJsOnlyResultYieldsRunningEntry()
        {
            var manifest = new PluginManifest
            {
                Id = "test",
                Version = "1.0.0",
            };

            LoadedPlugin? entry = host.Load(new PluginLoadResult(manifest, null, "/tmp/test.js"));

            ClassicAssert.IsNotNull(entry);
            ClassicAssert.AreEqual(PluginState.Running, entry!.State);
            ClassicAssert.IsNull(entry.Plugin);
            ClassicAssert.IsTrue(host.Loaded.Contains(entry));
            ClassicAssert.IsTrue(File.ReadAllText(Path.Combine(logsRoot, "plugin.log")).Contains("javascript entry registered"));
        }

        private LoadedPlugin load(PluginBase plugin, PluginManifest.PluginPermissions? permissions = null)
        {
            var manifest = new PluginManifest
            {
                Id = "test",
                Version = "1.0.0",
                Permissions = permissions ?? new PluginManifest.PluginPermissions(),
            };

            return host.Load(new PluginLoadResult(manifest, plugin, null))!;
        }

        private sealed record TestEvent : OsuApiEvent;

        private class RecordingPlugin : PluginBase
        {
            public PluginContext? Context { get; private set; }
            public bool Unloaded { get; private set; }
            public int UnloadCount { get; private set; }
            public int Ticks { get; set; }
            public int EventsReceived { get; set; }

            public Action<PluginContext>? OnLoad { get; set; }

            public override void Load(PluginContext context)
            {
                Context = context;
                OnLoad?.Invoke(context);
            }

            public override void Unload()
            {
                Unloaded = true;
                UnloadCount++;
            }
        }
    }
}
