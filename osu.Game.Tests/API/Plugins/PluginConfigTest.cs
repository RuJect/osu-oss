// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Plugins;

namespace osu.Game.Tests.API.Plugins
{
    [TestFixture]
    public class PluginConfigTest
    {
        private TemporaryNativeStorage storage = null!;
        private TemporaryNativeStorage logsStorage = null!;
        private PluginLogger logger = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("plugin-config");
            logsStorage = new TemporaryNativeStorage("plugin-config-logs");
            logger = new PluginLogger("test", logsStorage);
        }

        [TearDown]
        public void TearDown()
        {
            storage.Dispose();
            logsStorage.Dispose();
        }

        [Test]
        public void TestDefaultsAreApplied()
        {
            using var manager = createManager();

            ClassicAssert.AreEqual(0.5f, manager.GetValue("Volume"));
            ClassicAssert.IsFalse(manager.GetBindable<bool>(TestSetting.Muted).Value);
            ClassicAssert.IsTrue(manager.GetBindable<bool>(TestSetting.Enabled).Value);
            ClassicAssert.AreEqual(1.0, manager.GetBindable<double>(TestSetting.Scale).Value);
            ClassicAssert.AreEqual(3, manager.GetBindable<int>(TestSetting.Count).Value);
        }

        [Test]
        public void TestRangeDefaultsClampValues()
        {
            using var manager = createManager();

            manager.GetBindable<int>(TestSetting.Count).Value = 99;
            ClassicAssert.AreEqual(10, manager.GetBindable<int>(TestSetting.Count).Value);

            manager.GetBindable<int>(TestSetting.Count).Value = -1;
            ClassicAssert.AreEqual(0, manager.GetBindable<int>(TestSetting.Count).Value);
        }

        [Test]
        public void TestValuesPersistAcrossReload()
        {
            var manager = createManager();
            manager.GetBindable<float>(TestSetting.Volume).Value = 0.75f;
            manager.SetValue("Enabled", false);
            manager.Save();
            manager.Dispose();

            string contents = File.ReadAllText(storage.GetFullPath("config.ini"));
            StringAssert.Contains("[Audio]", contents);
            StringAssert.Contains("Volume = 0.75", contents);

            using var reloaded = createManager();
            ClassicAssert.AreEqual(0.75f, reloaded.GetValue("Volume"));
            ClassicAssert.IsFalse(reloaded.GetBindable<bool>(TestSetting.Enabled).Value);
        }

        [Test]
        public void TestSectionsAndKeys()
        {
            using var manager = createManager();

            CollectionAssert.AreEqual(new[] { "Audio", "General", "Display" }, manager.Sections.ToArray());
            CollectionAssert.AreEqual(new[] { "Volume", "Muted" }, manager.Keys("Audio").ToArray());
            CollectionAssert.AreEqual(new[] { "Enabled" }, manager.Keys("General").ToArray());
            CollectionAssert.AreEqual(new[] { "Volume", "Muted", "Enabled", "Scale", "Count" }, manager.Keys().ToArray());
        }

        [Test]
        public void TestGetSetReset()
        {
            using var manager = createManager();

            manager.SetValue("Volume", 0.8);
            ClassicAssert.AreEqual(0.8f, manager.GetValue("Volume"));

            manager.SetValue("Volume", "0.9");
            ClassicAssert.AreEqual(0.9f, manager.GetValue("Volume"));

            manager.SetValue("Muted", true);
            ClassicAssert.IsTrue(manager.GetBindable<bool>(TestSetting.Muted).Value);

            manager.Reset("Volume");
            ClassicAssert.AreEqual(0.5f, manager.GetValue("Volume"));

            manager.SetValue("Muted", null);
            ClassicAssert.IsFalse(manager.GetBindable<bool>(TestSetting.Muted).Value);
        }

        [Test]
        public void TestInvalidLinesAreIgnored()
        {
            File.WriteAllText(storage.GetFullPath("config.ini"), string.Join("\n", new[]
            {
                "[General]",
                "Enabled = false",
                "this is not a valid line",
                "UnknownSetting = 1",
                "[Broken",
                "[Audio]",
                "Volume = not-a-number",
                "Muted = true",
            }));

            using var manager = createManager();

            ClassicAssert.IsFalse(manager.GetBindable<bool>(TestSetting.Enabled).Value);
            ClassicAssert.IsTrue(manager.GetBindable<bool>(TestSetting.Muted).Value);
            ClassicAssert.AreEqual(0.5f, manager.GetBindable<float>(TestSetting.Volume).Value);
            ClassicAssert.AreEqual(3, manager.GetBindable<int>(TestSetting.Count).Value);
        }

        private PluginConfigManager<TestSetting> createManager()
        {
            return new PluginConfigManager<TestSetting>(storage, logger, manager =>
            {
                manager.SetDefault(TestSetting.Volume, 0.5f);
                manager.SetDefault(TestSetting.Muted, false);
                manager.SetDefault(TestSetting.Enabled, true);
                manager.SetDefault(TestSetting.Scale, 1.0, 0.5, 2.0, 0.1);
                manager.SetDefault(TestSetting.Count, 3, 0, 10);
            });
        }

        private enum TestSetting
        {
            [PluginConfigSection("Audio")]
            Volume,

            [PluginConfigSection("Audio")]
            Muted,

            Enabled,

            [PluginConfigSection("Display")]
            Scale,

            [PluginConfigSection("Display")]
            Count,
        }
    }
}
