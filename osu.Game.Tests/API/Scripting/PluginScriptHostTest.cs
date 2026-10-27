// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Plugins;
using osu.Game.API.Scripting;
using osu.Game.API.Services;
using osu.Game.Events;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class PluginScriptHostTest
    {
        private TemporaryNativeStorage scriptRoot = null!;
        private TemporaryNativeStorage logsStorage = null!;
        private OutputLog output = null!;
        private OsuEventBus bus = null!;
        private ScriptingSettings settings = null!;

        [SetUp]
        public void SetUp()
        {
            scriptRoot = new TemporaryNativeStorage("plugin-script-host-scripts");
            logsStorage = new TemporaryNativeStorage("plugin-script-host-logs");
            output = new OutputLog();
            bus = new OsuEventBus();
            settings = new ScriptingSettings();
        }

        [TearDown]
        public void TearDown()
        {
            scriptRoot.Dispose();
            logsStorage.Dispose();
        }

        [Test]
        public void TestGlobalHostRunsLaunchScript()
        {
            var storage = new ScriptStorage(scriptRoot);
            storage.Write(ScriptStorage.LAUNCH_SCRIPT, "osu.log('global launch');");

            var context = createContext(createManifest(PluginManifest.SYSTEM_ID, true));
            using var host = new PluginScriptHost(context, storage, null, true, null, settings);

            host.Start();

            ClassicAssert.IsTrue(logged("global launch"));

            host.Shutdown();
            ClassicAssert.IsEmpty(host.Manager.Runner.Running);
        }

        [Test]
        public void TestExternalHostRunsEntryPoint()
        {
            var storage = new ScriptStorage(scriptRoot);
            storage.Write("test.js", "osu.log('plugin js');");

            var context = createContext(createManifest("test", true));
            using var host = new PluginScriptHost(context, storage, null, false, "test.js", settings);

            host.Start();

            ClassicAssert.IsTrue(logged("plugin js"));

            host.Shutdown();
            ClassicAssert.IsEmpty(host.Manager.Runner.Running);
        }

        [Test]
        public void TestDisabledSettingsPreventStartAndCanBeEnabledLater()
        {
            settings.Update(false, true);

            var storage = new ScriptStorage(scriptRoot);
            storage.Write("test.js", "osu.log('plugin js');");

            var context = createContext(createManifest("test", true));
            using var host = new PluginScriptHost(context, storage, null, false, "test.js", settings);

            host.Start();
            ClassicAssert.IsFalse(logged("plugin js"));

            settings.Update(true, true);
            host.ApplySettings(settings);

            ClassicAssert.IsTrue(logged("plugin js"));
        }

        [Test]
        public void TestClrAccessRequiresJsPermission()
        {
            var context = createContext(createManifest("test", false));
            using var host = new PluginScriptHost(context, new ScriptStorage(scriptRoot), null, false, null, settings);

            ClassicAssert.IsFalse(host.Manager.AllowClrAccess);
        }

        [Test]
        public void TestExternalPluginOutputIsScoped()
        {
            var services = new GameServices(new OsuGame(), null!, output, scriptRoot, scriptRoot, scriptRoot,
                null!, null!, null!, null!, null!, () => null);

            ApiServices system = services.CreatePluginServices(createManifest(PluginManifest.SYSTEM_ID, true));
            ApiServices external = services.CreatePluginServices(createManifest("test", false));

            ClassicAssert.AreSame(output, system.Output);
            ClassicAssert.AreNotSame(output, external.Output);
        }

        private static PluginManifest createManifest(string id, bool trusted) => new PluginManifest
        {
            Id = id,
            Version = "1.0.0",
            Permissions = new PluginManifest.PluginPermissions { Trusted = trusted },
        };

        private PluginContext createContext(PluginManifest manifest)
        {
            var logger = new PluginLogger(manifest.Id, logsStorage);
            var services = new ApiServices(new OsuGame(), null!, output);

            return new PluginContext(services, new PluginEventService(bus, manifest, logger), new SchedulerService(), logger, manifest);
        }

        private bool logged(string text) => output.History.Any(o => o.Text == text);
    }
}
