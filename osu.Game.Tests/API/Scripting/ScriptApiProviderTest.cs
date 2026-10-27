// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint;
using Jint.Native;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Plugins;
using osu.Game.API.Scripting;
using osu.Game.API.Scripting.Api;
using osu.Game.API.Services;
using osu.Game.Events;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class ScriptApiProviderTest
    {
        private Engine engine = null!;
        private ScriptApiContext context = null!;

        [SetUp]
        public void SetUp()
        {
            engine = new Engine();

            var output = new OutputLog();
            var manifest = new PluginManifest
            {
                Id = PluginManifest.SYSTEM_ID,
                Permissions = new PluginManifest.PluginPermissions { Trusted = true },
            };

            var apiServices = new ApiServices(new OsuGame(), null!, output);
            var plugin = new PluginContext(
                apiServices,
                new PluginEventService(new OsuEventBus(), manifest),
                new SchedulerService(),
                new PluginLogger(PluginManifest.SYSTEM_ID),
                manifest);
            var manager = new ScriptManager(plugin);

            context = new ScriptApiContext(engine, null!, plugin, manager, new JsObject(engine));
        }

        [Test]
        public void TestMethodsAreRegisteredAndCallable()
        {
            new TestProvider().Register(context);

            engine.SetValue("osu", context.Api);

            ClassicAssert.AreEqual(42, engine.Evaluate("osu.double(21)").AsNumber());
            ClassicAssert.AreEqual("hello world", engine.Evaluate("osu.greet('world')").AsString());
        }

        [Test]
        public void TestMethodNamesAreCamelCased()
        {
            new TestProvider().Register(context);

            engine.SetValue("osu", context.Api);

            ClassicAssert.AreEqual(7, engine.Evaluate("osu.addNumbers(3, 4)").AsNumber());
        }

        [Test]
        public void TestExplicitNameOverridesCamelCasing()
        {
            new TestProvider().Register(context);

            engine.SetValue("osu", context.Api);

            ClassicAssert.AreEqual(8, engine.Evaluate("osu.multiply(2, 4)").AsNumber());
        }

        [Test]
        public void TestSubObjectTarget()
        {
            new SubObjectProvider().Register(context);

            engine.SetValue("osu", context.Api);

            ClassicAssert.AreEqual("data", engine.Evaluate("osu.storage.read()").AsString());
        }

        [Test]
        public void TestPropertyIsRegistered()
        {
            new PropertyProvider().Register(context);

            engine.SetValue("osu", context.Api);

            ClassicAssert.AreEqual("1.0.0", engine.Evaluate("osu.version").AsString());
        }

        private class TestProvider : ScriptApiProvider
        {
            [ScriptApi]
            public double Double(double value) => value * 2;

            [ScriptApi]
            public string Greet(string name) => $"hello {name}";

            [ScriptApi]
            public int AddNumbers(int a, int b) => a + b;

            [ScriptApi(Name = "multiply")]
            public int Multiply(int a, int b) => a * b;
        }

        private class SubObjectProvider : ScriptApiProvider
        {
            [ScriptApi(Target = "storage")]
            public string Read() => "data";
        }

        private class PropertyProvider : ScriptApiProvider
        {
            [ScriptApi]
            public string Version => "1.0.0";
        }
    }
}
