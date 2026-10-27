// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Plugins;

namespace osu.Game.Tests.API.Plugins
{
    [TestFixture]
    public class PluginManifestTest
    {
        [TestCase("test")]
        [TestCase("system")]
        [TestCase("plugin_1")]
        [TestCase("a")]
        [TestCase("abc123")]
        public void TestValidId(string id)
        {
            var manifest = new PluginManifest { Id = id };

            ClassicAssert.IsTrue(manifest.IsValidId);
        }

        [TestCase("Test")]
        [TestCase("my-plugin")]
        [TestCase("1plugin")]
        [TestCase("")]
        [TestCase("plugin.name")]
        [TestCase("plugin/name")]
        [TestCase("plugin name")]
        public void TestInvalidId(string id)
        {
            var manifest = new PluginManifest { Id = id };

            ClassicAssert.IsFalse(manifest.IsValidId);
        }

        [Test]
        public void TestDefaultPermissionsDenyEverything()
        {
            var permissions = new PluginManifest.PluginPermissions();

            ClassicAssert.IsFalse(permissions.CanUseBus);
            ClassicAssert.IsFalse(permissions.CanUseData);
            ClassicAssert.IsFalse(permissions.CanUseJs);
        }

        [Test]
        public void TestBusPermission()
        {
            var permissions = new PluginManifest.PluginPermissions { Bus = true };

            ClassicAssert.IsTrue(permissions.CanUseBus);
            ClassicAssert.IsFalse(permissions.CanUseData);
            ClassicAssert.IsFalse(permissions.CanUseJs);
        }

        [Test]
        public void TestDataPermission()
        {
            var permissions = new PluginManifest.PluginPermissions { Data = true };

            ClassicAssert.IsFalse(permissions.CanUseBus);
            ClassicAssert.IsTrue(permissions.CanUseData);
            ClassicAssert.IsFalse(permissions.CanUseJs);
        }

        [Test]
        public void TestJsPermission()
        {
            var permissions = new PluginManifest.PluginPermissions { Js = true };

            ClassicAssert.IsFalse(permissions.CanUseBus);
            ClassicAssert.IsFalse(permissions.CanUseData);
            ClassicAssert.IsTrue(permissions.CanUseJs);
        }

        [Test]
        public void TestTrustedGrantsEverything()
        {
            var permissions = new PluginManifest.PluginPermissions { Trusted = true };

            ClassicAssert.IsTrue(permissions.CanUseBus);
            ClassicAssert.IsTrue(permissions.CanUseData);
            ClassicAssert.IsTrue(permissions.CanUseJs);
        }

        [Test]
        public void TestValidVersion()
        {
            var manifest = new PluginManifest { Id = "test", Version = "1.2.3" };

            ClassicAssert.IsTrue(manifest.TryValidate(out string? error));
            ClassicAssert.IsNull(error);
        }

        [Test]
        public void TestMissingVersionIsInvalid()
        {
            var manifest = new PluginManifest { Id = "test" };

            ClassicAssert.IsFalse(manifest.TryValidate(out string? error));
            ClassicAssert.IsNotNull(error);
        }

        [Test]
        public void TestInvalidVersionIsInvalid()
        {
            var manifest = new PluginManifest { Id = "test", Version = "not-a-version" };

            ClassicAssert.IsFalse(manifest.TryValidate(out string? error));
            ClassicAssert.IsNotNull(error);
        }

        [Test]
        public void TestMissingApiVersionIsAccepted()
        {
            var manifest = new PluginManifest { Id = "test", Version = "1.0.0" };

            ClassicAssert.IsTrue(manifest.TryValidate(out _));
        }

        [Test]
        public void TestMatchingApiMajorIsAccepted()
        {
            var manifest = new PluginManifest { Id = "test", Version = "1.0.0", ApiVersion = $"{PluginApi.CurrentVersion.Major}.0.0" };

            ClassicAssert.IsTrue(manifest.TryValidate(out _));
        }

        [Test]
        public void TestIncompatibleApiMajorIsInvalid()
        {
            var manifest = new PluginManifest { Id = "test", Version = "1.0.0", ApiVersion = $"{PluginApi.CurrentVersion.Major + 1}.0.0" };

            ClassicAssert.IsFalse(manifest.TryValidate(out string? error));
            ClassicAssert.IsNotNull(error);
        }
    }
}
