// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Plugins;

namespace osu.Game.Tests.API.Plugins
{
    [TestFixture]
    public class PluginLoggerTest
    {
        [Test]
        public void TestWritesLinesToFile()
        {
            using var storage = new TemporaryNativeStorage("plugin-logger");
            var logger = new PluginLogger("test", storage);

            logger.Info("info message");
            logger.Warn("warn message");
            logger.Error("error message");
            logger.Debug("debug message");

            string path = storage.GetFullPath("plugin.log");

            ClassicAssert.IsTrue(File.Exists(path));

            string contents = File.ReadAllText(path);

            StringAssert.Contains("[INFO] info message", contents);
            StringAssert.Contains("[WARN] warn message", contents);
            StringAssert.Contains("[ERROR] error message", contents);
            StringAssert.Contains("[DEBUG] debug message", contents);
        }
    }
}
