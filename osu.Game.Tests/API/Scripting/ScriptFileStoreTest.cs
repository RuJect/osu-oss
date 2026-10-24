// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.API.Scripting;

namespace osu.Game.Tests.API.Scripting
{
    [TestFixture]
    public class ScriptFileStoreTest
    {
        private TemporaryNativeStorage storage = null!;
        private ScriptDataStorage store = null!;
        private string outsideFile = null!;
        private string outsideDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("script-file-store");
            store = new ScriptDataStorage(storage);

            string parent = Path.Combine(storage.GetFullPath(string.Empty), "..");
            outsideFile = Path.Combine(parent, $"outside-{Guid.NewGuid():N}.txt");
            outsideDirectory = Path.Combine(parent, $"outside-{Guid.NewGuid():N}");

            File.WriteAllText(outsideFile, "secret");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(outsideFile))
                File.Delete(outsideFile);

            if (Directory.Exists(outsideDirectory))
                Directory.Delete(outsideDirectory, true);

            storage.Dispose();
        }

        [TestCase("foo.txt", "foo.txt")]
        [TestCase("a/b/c.txt", "a/b/c.txt")]
        [TestCase(@"a\b\c.txt", "a/b/c.txt")]
        [TestCase("./a/./b", "a/b")]
        [TestCase("a//b", "a/b")]
        [TestCase("...", "...")]
        [TestCase("", null)]
        [TestCase("   ", null)]
        [TestCase(".", null)]
        [TestCase("./", null)]
        [TestCase("..", null)]
        [TestCase("../", null)]
        [TestCase("../escape.txt", null)]
        [TestCase(@"..\escape.txt", null)]
        [TestCase("a/../b.txt", null)]
        [TestCase("a/..", null)]
        [TestCase("a/../../escape.txt", null)]
        [TestCase(@"a\..\..\escape.txt", null)]
        [TestCase("/abs/path.txt", null)]
        public void TestSanitizePath(string input, string? expected)
        {
            ClassicAssert.AreEqual(expected, ScriptFileStore.SanitizePath(input));
        }

        [Test]
        public void TestSanitizePathRejectsCurrentPlatformAbsolutePath()
        {
            ClassicAssert.IsNull(ScriptFileStore.SanitizePath(Path.GetFullPath("escape.txt")));
        }

        [Test]
        public void TestTraversalRejectedForFileOperations()
        {
            string traversalFile = "../" + Path.GetFileName(outsideFile);
            string traversalDirectory = "../" + Path.GetFileName(outsideDirectory);

            ClassicAssert.IsFalse(store.Exists(traversalFile));
            ClassicAssert.IsNull(store.Read(traversalFile));

            store.Write(traversalFile, "overwritten");
            store.Append(traversalFile, "appended");
            store.Delete(traversalFile);
            store.CreateDirectory(traversalDirectory);

            ClassicAssert.IsTrue(File.Exists(outsideFile));
            ClassicAssert.AreEqual("secret", File.ReadAllText(outsideFile));
            ClassicAssert.IsFalse(Directory.Exists(outsideDirectory));
        }

        [Test]
        public void TestAbsolutePathRejectedForFileOperations()
        {
            ClassicAssert.IsFalse(store.Exists(outsideFile));
            ClassicAssert.IsNull(store.Read(outsideFile));

            store.Write(outsideFile, "overwritten");
            store.Append(outsideFile, "appended");
            store.Delete(outsideFile);

            ClassicAssert.IsTrue(File.Exists(outsideFile));
            ClassicAssert.AreEqual("secret", File.ReadAllText(outsideFile));
        }

        [Test]
        public void TestBackslashTraversalRejected()
        {
            string traversalFile = @"..\" + Path.GetFileName(outsideFile);

            ClassicAssert.IsFalse(store.Exists(traversalFile));
            ClassicAssert.IsNull(store.Read(traversalFile));

            store.Write(traversalFile, "overwritten");

            ClassicAssert.IsTrue(File.Exists(outsideFile));
            ClassicAssert.AreEqual("secret", File.ReadAllText(outsideFile));
        }

        [Test]
        public void TestListRejectsTraversal()
        {
            store.Write("inside.txt", "inside");

            ClassicAssert.IsEmpty(store.List("../"));
            ClassicAssert.IsEmpty(store.List(".."));
            ClassicAssert.IsEmpty(store.ListFiles("../"));
            ClassicAssert.IsEmpty(store.List(storage.GetFullPath(string.Empty)));

            ClassicAssert.IsTrue(store.List(string.Empty).Contains("inside.txt"));
        }

        [Test]
        public void TestValidPathsStillWork()
        {
            store.CreateDirectory("nested");
            store.Write("nested/file.txt", "hello");
            store.Append("nested/file.txt", " world");

            ClassicAssert.IsTrue(store.Exists("nested/file.txt"));
            ClassicAssert.AreEqual("hello world", store.Read("nested/file.txt"));
            ClassicAssert.IsTrue(store.List("nested").Contains("nested/file.txt"));
            ClassicAssert.AreEqual("hello world", File.ReadAllText(Path.Combine(store.Directory, "nested", "file.txt")));
        }
    }
}
