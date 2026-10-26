// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Events;
using osu.Game.API.Plugins;
using osu.Game.API.Services;

namespace osu.Game.Tests.API.Plugins
{
    [TestFixture]
    public class PluginHostTest
    {
        private OutputLog output = null!;
        private PluginHost host = null!;

        [SetUp]
        public void SetUp()
        {
            output = new OutputLog();
            var services = new ApiServices(new OsuGame(), null!, output);
            host = new PluginHost(services, new OsuEventBus());
        }

        [Test]
        public void TestLoadInvokesPluginWithContext()
        {
            var plugin = new RecordingPlugin();

            LoadedPlugin entry = host.Load(plugin);

            ClassicAssert.AreSame(plugin, entry.Plugin);
            ClassicAssert.IsNotNull(plugin.Context);
            ClassicAssert.AreEqual(PluginState.Running, entry.State);
            ClassicAssert.IsTrue(host.Loaded.Contains(entry));
        }

        [Test]
        public void TestContextExposesServicesAndVersion()
        {
            var plugin = new RecordingPlugin();
            host.Load(plugin);

            ClassicAssert.IsNotNull(plugin.Context);
            ClassicAssert.IsNotNull(plugin.Context.Output);
            ClassicAssert.IsNotNull(plugin.Context.Events);
            ClassicAssert.IsNotNull(plugin.Context.Scheduler);
        }

        [Test]
        public void TestTickAdvancesPluginScheduler()
        {
            var plugin = new RecordingPlugin();
            plugin.OnLoad = context => context.Scheduler.AddUpdateHandler(() => plugin.Ticks++);
            host.Load(plugin);

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

            host.Load(first);
            host.Load(second);

            host.Tick(0);

            ClassicAssert.AreEqual(1, first.Ticks);
            ClassicAssert.AreEqual(1, second.Ticks);
        }

        [Test]
        public void TestUnloadInvokesUnloadAndRemoves()
        {
            var plugin = new RecordingPlugin();
            LoadedPlugin entry = host.Load(plugin);

            host.Unload(entry);

            ClassicAssert.IsTrue(plugin.Unloaded);
            ClassicAssert.AreEqual(PluginState.Stopped, entry.State);
            ClassicAssert.IsFalse(host.Loaded.Contains(entry));
        }

        [Test]
        public void TestLoadFailureIsRecordedAndLogged()
        {
            var plugin = new RecordingPlugin { OnLoad = _ => throw new InvalidOperationException("boom") };

            LoadedPlugin entry = host.Load(plugin);

            ClassicAssert.AreEqual(PluginState.Error, entry.State);
            ClassicAssert.IsTrue(output.History.Any(o => o.Text.Contains("boom")));
        }

        [Test]
        public void TestTickFailureStopsSchedulingFurtherTicks()
        {
            var plugin = new RecordingPlugin();
            plugin.OnLoad = c => c.Scheduler.AddUpdateHandler(() => throw new InvalidOperationException("boom"));
            LoadedPlugin entry = host.Load(plugin);

            host.Tick(0);

            ClassicAssert.AreEqual(PluginState.Error, entry.State);
        }

        [Test]
        public void TestUnloadAll()
        {
            var first = new RecordingPlugin();
            var second = new RecordingPlugin();

            host.Load(first);
            host.Load(second);
            host.UnloadAll();

            ClassicAssert.IsTrue(first.Unloaded);
            ClassicAssert.IsTrue(second.Unloaded);
            ClassicAssert.IsEmpty(host.Loaded);
        }

        private class RecordingPlugin : IPlugin
        {
            public IPluginContext? Context { get; private set; }
            public bool Unloaded { get; private set; }
            public int Ticks { get; set; }

            public Action<IPluginContext>? OnLoad { get; set; }

            public void Load(IPluginContext context)
            {
                Context = context;
                OnLoad?.Invoke(context);
            }

            public void Unload() => Unloaded = true;
        }
    }
}
