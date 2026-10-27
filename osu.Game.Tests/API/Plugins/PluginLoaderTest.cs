// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Plugins;
using osu.Game.API.Services;

namespace osu.Game.Tests.API.Plugins
{
    [TestFixture]
    public class PluginLoaderTest
    {
        private TemporaryNativeStorage storage = null!;
        private OutputLog output = null!;
        private PluginLoader loader = null!;
        private string root = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("plugin-loader");
            output = new OutputLog();
            loader = new PluginLoader(output);
            root = storage.GetFullPath(string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            storage.Dispose();
        }

        [Test]
        public void TestScriptOnlyPluginProducesNoResults()
        {
            const string id = "script_only";
            string directory = createPluginDirectory(id);
            string scripts = Path.Combine(directory, "scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, $"{id}.js"), "console.log('hi')");

            var manifest = new PluginManifest { Id = id, DirectoryPath = directory };

            PluginLoadResult[] results = loader.LoadFromManifest(manifest).ToArray();

            ClassicAssert.IsEmpty(results);
            ClassicAssert.IsNull(manifest.CSharpEntryPoint);
            ClassicAssert.AreEqual(Path.Combine(scripts, $"{id}.js"), manifest.JavaScriptEntryPoint);
            ClassicAssert.IsTrue(output.History.Any(o => o.Level == OutputLevel.Warn && o.Text.Contains("JS plugins are not supported yet")));
        }

        [Test]
        public void TestMissingCSharpEntryPointIsNotSet()
        {
            const string id = "missing_dll";
            string directory = createPluginDirectory(id);

            var manifest = new PluginManifest { Id = id, DirectoryPath = directory };

            PluginLoadResult[] results = loader.LoadFromManifest(manifest).ToArray();

            ClassicAssert.IsEmpty(results);
            ClassicAssert.IsNull(manifest.CSharpEntryPoint);
            ClassicAssert.IsTrue(output.History.Any(o => o.Level == OutputLevel.Warn && o.Text.Contains("no entry point found")));
        }

        [Test]
        public void TestExplicitMissingAssemblyLogsError()
        {
            const string id = "broken";
            string directory = createPluginDirectory(id);
            string bin = Path.Combine(directory, "bin");
            Directory.CreateDirectory(bin);
            string missing = Path.Combine(bin, $"{id}.dll");
            File.WriteAllBytes(missing, Array.Empty<byte>());

            var manifest = new PluginManifest { Id = id, DirectoryPath = directory };

            PluginLoadResult[] results = loader.LoadFromManifest(manifest).ToArray();

            ClassicAssert.IsEmpty(results);
            ClassicAssert.AreEqual(missing, manifest.CSharpEntryPoint);
            ClassicAssert.IsTrue(output.History.Any(o => o.Level == OutputLevel.Error && o.Text.Contains("failed to load assembly")));
        }

        private string createPluginDirectory(string id)
        {
            string directory = Path.Combine(root, id);
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
