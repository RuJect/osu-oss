// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Services;

namespace osu.Game.Tests.API.Plugins
{
    [TestFixture]
    public class ResourceStoreTest
    {
        private TemporaryNativeStorage storage = null!;
        private FileStore fileStore = null!;
        private ResourceStore resources = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("plugin-resources");
            fileStore = new FileStore(storage);
            resources = new ResourceStore(fileStore);

            fileStore.Write("readme.txt", "hello resources");
            fileStore.WriteBytes("nested/data.bin", new byte[] { 1, 2, 3, 4 });
        }

        [TearDown]
        public void TearDown()
        {
            storage.Dispose();
        }

        [Test]
        public void TestReadsTextAndBytes()
        {
            ClassicAssert.IsTrue(resources.Exists("readme.txt"));
            ClassicAssert.AreEqual("hello resources", resources.Read("readme.txt"));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, resources.ReadBytes("nested/data.bin")!);
            ClassicAssert.AreEqual(storage.GetFullPath(string.Empty), resources.Directory);

            using Stream stream = resources.OpenRead("readme.txt")!;
            using var reader = new StreamReader(stream);
            ClassicAssert.AreEqual("hello resources", reader.ReadToEnd());
        }

        [Test]
        public void TestList()
        {
            CollectionAssert.AreEquivalent(new[] { "readme.txt", "nested/data.bin" }, resources.List().ToArray());
        }

        [Test]
        public void TestTraversalRejected()
        {
            ClassicAssert.IsFalse(resources.Exists("../readme.txt"));
            ClassicAssert.IsNull(resources.Read("../readme.txt"));
            ClassicAssert.IsNull(resources.ReadBytes("../readme.txt"));
            ClassicAssert.IsNull(resources.OpenRead("../readme.txt"));
            ClassicAssert.IsEmpty(resources.List("../"));
        }
    }
}
