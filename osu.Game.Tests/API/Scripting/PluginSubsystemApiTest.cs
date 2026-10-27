// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint;
using Jint.Native;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Plugins;
using osu.Game.API.Scripting;
using osu.Game.API.Scripting.Api;
using osu.Game.API.Scripting.Api.Providers;
using osu.Game.API.Services;
using osu.Game.Events;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class PluginSubsystemApiTest
    {
        private TemporaryNativeStorage scriptRoot = null!;
        private TemporaryNativeStorage dataRoot = null!;
        private TemporaryNativeStorage resourceRoot = null!;
        private TemporaryNativeStorage configRoot = null!;
        private PluginContext plugin = null!;
        private ScriptManager manager = null!;
        private Engine engine = null!;
        private ScriptApiContext context = null!;

        [SetUp]
        public void SetUp()
        {
            scriptRoot = new TemporaryNativeStorage("plugin-subsystem-scripts");
            dataRoot = new TemporaryNativeStorage("plugin-subsystem-data");
            resourceRoot = new TemporaryNativeStorage("plugin-subsystem-resources");
            configRoot = new TemporaryNativeStorage("plugin-subsystem-config");

            var resourceFiles = new FileStore(resourceRoot);
            resourceFiles.Write("file.txt", "hello resources");
            resourceFiles.WriteBytes("nested/data.bin", new byte[] { 1, 2, 3, 4 });

            var output = new OutputLog();
            var manifest = new PluginManifest
            {
                Id = PluginManifest.SYSTEM_ID,
                Permissions = new PluginManifest.PluginPermissions { Trusted = true },
            };

            var services = new ApiServices(
                new OsuGame(),
                null!,
                output,
                storage: new StorageService(new FileStore(dataRoot)),
                resources: new ResourceStore(new FileStore(resourceRoot)));

            plugin = new PluginContext(
                services,
                new PluginEventService(new OsuEventBus(), manifest),
                new SchedulerService(),
                new PluginLogger(PluginManifest.SYSTEM_ID),
                manifest,
                configRoot);

            plugin.CreateConfig<TestSetting>(config =>
            {
                config.SetDefault(TestSetting.Enabled, true);
                config.SetDefault(TestSetting.Volume, 0.5f);
            });

            manager = new ScriptManager(plugin);
            engine = new Engine();
            context = new ScriptApiContext(engine, null!, plugin, manager, new JsObject(engine));

            new PluginConfigApiProvider().Register(context);
            new ResourceApiProvider().Register(context);
            new StorageApiProvider().Register(context);

            engine.SetValue("osu", context.Api);
        }

        [TearDown]
        public void TearDown()
        {
            manager.Runner.StopAll();
            plugin.Dispose();
            scriptRoot.Dispose();
            dataRoot.Dispose();
            resourceRoot.Dispose();
            configRoot.Dispose();
        }

        [Test]
        public void TestPluginConfigGetSetReset()
        {
            ClassicAssert.IsTrue(engine.Evaluate("osu.pluginConfig.get('Enabled')").AsBoolean());
            ClassicAssert.IsTrue(engine.Evaluate("osu.pluginConfig.get('Missing')").IsUndefined());

            engine.Evaluate("osu.pluginConfig.set('Volume', 0.75)");
            ClassicAssert.AreEqual(0.75, engine.Evaluate("osu.pluginConfig.get('Volume')").AsNumber(), 1e-6);

            engine.Evaluate("osu.pluginConfig.reset('Volume')");
            ClassicAssert.AreEqual(0.5, engine.Evaluate("osu.pluginConfig.get('Volume')").AsNumber(), 1e-6);

            engine.Evaluate("osu.pluginConfig.set('Volume', 0.9); osu.pluginConfig.set('Volume', undefined)");
            ClassicAssert.AreEqual(0.5, engine.Evaluate("osu.pluginConfig.get('Volume')").AsNumber(), 1e-6);
        }

        [Test]
        public void TestPluginConfigKeysAndSections()
        {
            ClassicAssert.AreEqual("Enabled,Volume", engine.Evaluate("osu.pluginConfig.keys('General').join(',')").AsString());
            ClassicAssert.AreEqual("General", engine.Evaluate("osu.pluginConfig.sections().join(',')").AsString());
        }

        [Test]
        public void TestResourceReadExistsListAndBytes()
        {
            ClassicAssert.AreEqual("hello resources", engine.Evaluate("osu.res.read('file.txt')").AsString());
            ClassicAssert.IsTrue(engine.Evaluate("osu.res.exists('file.txt')").AsBoolean());
            ClassicAssert.IsFalse(engine.Evaluate("osu.res.exists('missing.txt')").AsBoolean());
            ClassicAssert.IsTrue(engine.Evaluate("osu.res.read('missing.txt')").IsUndefined());

            ClassicAssert.AreEqual("file.txt,nested/data.bin", engine.Evaluate("osu.res.list().sort().join(',')").AsString());
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, toBytes(engine.Evaluate("osu.res.readBytes('nested/data.bin')")));
        }

        [Test]
        public void TestStorageBinaryRoundTrip()
        {
            engine.Evaluate("osu.storage.writeBytes('bin/data.bin', [1, 2, 3])");

            ClassicAssert.IsTrue(engine.Evaluate("osu.storage.exists('bin/data.bin')").AsBoolean());
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, toBytes(engine.Evaluate("osu.storage.readBytes('bin/data.bin')")));
        }

        private static byte[] toBytes(JsValue value)
        {
            JsArray array = value.AsArray();
            var result = new byte[array.Length];

            for (uint i = 0; i < array.Length; i++)
                result[i] = (byte)value.Get(i.ToString()).AsNumber();

            return result;
        }

        private enum TestSetting
        {
            [PluginConfigSection("General")]
            Enabled,

            [PluginConfigSection("General")]
            Volume,
        }
    }
}
