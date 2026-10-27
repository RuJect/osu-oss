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
    public class PluginRepositoryTest
    {
        private TemporaryNativeStorage storage = null!;
        private PluginRepository repository = null!;
        private string root = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("plugin-repository");
            repository = new PluginRepository(storage);
            root = storage.GetFullPath(string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            storage.Dispose();
        }

        [Test]
        public void TestEnumerateDiscoversValidManifests()
        {
            writeManifest("alpha", "{\"Id\":\"alpha\",\"Version\":\"1.0.0\"}");
            writeManifest("beta", "{\"Id\":\"beta\",\"Version\":\"1.0.0\",\"Permissions\":{\"Bus\":true,\"Data\":true}}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(2, manifests.Length);

            PluginManifest alpha = manifests.Single(m => m.Id == "alpha");
            ClassicAssert.AreEqual(Path.Combine(root, "alpha"), alpha.DirectoryPath);
            ClassicAssert.IsNull(alpha.CSharpEntryPoint);
            ClassicAssert.IsNull(alpha.JavaScriptEntryPoint);

            PluginManifest beta = manifests.Single(m => m.Id == "beta");
            ClassicAssert.IsTrue(beta.Permissions.CanUseBus);
            ClassicAssert.IsTrue(beta.Permissions.CanUseData);
        }

        [Test]
        public void TestInvalidIdIsSkipped()
        {
            writeManifest("Invalid-Id", "{\"Id\":\"Invalid-Id\",\"Version\":\"1.0.0\"}");
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("valid", manifests[0].Id);
        }

        [Test]
        public void TestDirectoryWithoutManifestIsSkipped()
        {
            Directory.CreateDirectory(Path.Combine(root, "no-manifest"));
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            ClassicAssert.AreEqual(1, repository.EnumeratePlugins().Count);
        }

        [Test]
        public void TestEmptyManifestIsSkipped()
        {
            writeManifest("empty", "   ");
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            ClassicAssert.AreEqual(1, repository.EnumeratePlugins().Count);
        }

        [Test]
        public void TestMalformedManifestDoesNotStopNextPlugin()
        {
            writeManifest("broken", "{ not valid json");
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("valid", manifests[0].Id);
        }

        [Test]
        public void TestMissingVersionIsSkipped()
        {
            writeManifest("no_version", "{\"Id\":\"no_version\"}");
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("valid", manifests[0].Id);
        }

        [Test]
        public void TestInvalidApiVersionIsSkipped()
        {
            writeManifest("bad_api", "{\"Id\":\"bad_api\",\"Version\":\"1.0.0\",\"ApiVersion\":\"not-a-version\"}");
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("valid", manifests[0].Id);
        }

        [Test]
        public void TestIncompatibleApiMajorIsSkipped()
        {
            writeManifest("old_api", $"{{\"Id\":\"old_api\",\"Version\":\"1.0.0\",\"ApiVersion\":\"{PluginApi.CurrentVersion.Major + 1}.0.0\"}}");
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("valid", manifests[0].Id);
        }

        [Test]
        public void TestMatchingApiMajorIsAccepted()
        {
            writeManifest("matching_api", $"{{\"Id\":\"matching_api\",\"Version\":\"1.0.0\",\"ApiVersion\":\"{PluginApi.CurrentVersion.Major}.5.0\"}}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("matching_api", manifests[0].Id);
        }

        [Test]
        public void TestMissingApiVersionIsAccepted()
        {
            writeManifest("no_api", "{\"Id\":\"no_api\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("no_api", manifests[0].Id);
        }

        [Test]
        public void TestSystemIdIsSkipped()
        {
            writeManifest("system", "{\"Id\":\"system\",\"Version\":\"1.0.0\"}");
            writeManifest("valid", "{\"Id\":\"valid\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("valid", manifests[0].Id);
        }

        [Test]
        public void TestDuplicateIdIsSkipped()
        {
            writeManifest("alpha", "{\"Id\":\"alpha\",\"Version\":\"1.0.0\"}");
            writeManifest("alpha_copy", "{\"Id\":\"alpha\",\"Version\":\"1.0.0\"}");

            PluginManifest[] manifests = repository.EnumeratePlugins().ToArray();

            ClassicAssert.AreEqual(1, manifests.Length);
            ClassicAssert.AreEqual("alpha", manifests[0].Id);
        }

        private void writeManifest(string id, string json)
        {
            string directory = Path.Combine(root, id);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), json);
        }
    }
}
