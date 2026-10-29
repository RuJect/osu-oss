// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.IO;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SpecialStorageTest
    {
        [Test]
        public void TestDefaultRootIsCentral()
        {
            string path = SpecialStorage.GetDefaultRootPath();

            Assert.That(Path.GetFileName(path), Is.EqualTo(SpecialStorage.ROOT_FOLDER));
            Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        }

        [Test]
        public void TestOsuStorageUsesConfiguredSpecialRoot()
        {
            using var storageA = new TemporaryNativeStorage($"osu-storage-a-{Guid.NewGuid():N}");
            using var storageB = new TemporaryNativeStorage($"osu-storage-b-{Guid.NewGuid():N}");
            using var specialRoot = new TemporaryNativeStorage($"osu-special-{Guid.NewGuid():N}");

            var osuStorageA = new OsuStorage(null!, storageA, specialStorageRoot: specialRoot);
            var osuStorageB = new OsuStorage(null!, storageB, specialStorageRoot: specialRoot);

            Assert.That(SpecialStorage.GetRootPath(osuStorageA), Is.EqualTo(specialRoot.GetFullPath(string.Empty)));
            Assert.That(SpecialStorage.GetRootPath(osuStorageB), Is.EqualTo(specialRoot.GetFullPath(string.Empty)));

            Assert.That(osuStorageA.IgnoreDirectories, Does.Contain(SpecialStorage.AUTH_BLOBS_FOLDER));
            Assert.That(osuStorageA.IgnoreDirectories, Does.Contain(SpecialStorage.PLUGINS_FOLDER));
            Assert.That(osuStorageA.IgnoreDirectories, Does.Contain(SpecialStorage.SCRIPTS_FOLDER));
            Assert.That(osuStorageA.IgnoreDirectories, Does.Contain(SpecialStorage.THEMES_FOLDER));
            Assert.That(osuStorageA.IgnoreFiles, Does.Contain(SpecialStorage.CONFIG_FILENAME));
        }

        [Test]
        public void TestLegacyStorageIsMigrated()
        {
            using var specialRoot = new TemporaryNativeStorage($"osu-special-{Guid.NewGuid():N}");
            using var legacyRoot = new TemporaryNativeStorage($"osu-legacy-{Guid.NewGuid():N}");

            writeFile(legacyRoot, "config.ini", "legacy config");
            writeFile(legacyRoot, "auth-blobs/blob.bin", "auth blob");

            SpecialStorage.MigrateLegacyStorage(specialRoot, new[] { legacyRoot });

            Assert.That(readFile(specialRoot, "config.ini"), Is.EqualTo("legacy config"));
            Assert.That(readFile(specialRoot, "auth-blobs/blob.bin"), Is.EqualTo("auth blob"));
            Assert.That(Directory.Exists(legacyRoot.GetFullPath(string.Empty)), Is.False);
        }

        [Test]
        public void TestExistingCentralFilesAreNotOverwritten()
        {
            using var specialRoot = new TemporaryNativeStorage($"osu-special-{Guid.NewGuid():N}");
            using var legacyRoot = new TemporaryNativeStorage($"osu-legacy-{Guid.NewGuid():N}");

            writeFile(specialRoot, "config.ini", "central config");
            writeFile(legacyRoot, "config.ini", "legacy config");

            SpecialStorage.MigrateLegacyStorage(specialRoot, new[] { legacyRoot });

            Assert.That(readFile(specialRoot, "config.ini"), Is.EqualTo("central config"));
            Assert.That(Directory.Exists(legacyRoot.GetFullPath(string.Empty)), Is.False);
        }

        private static void writeFile(Storage storage, string filename, string contents)
        {
            string path = storage.GetFullPath(filename, true);

            if (Path.GetDirectoryName(path) is string directory)
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, contents);
        }

        private static string readFile(Storage storage, string filename)
            => File.ReadAllText(storage.GetFullPath(filename));
    }
}
