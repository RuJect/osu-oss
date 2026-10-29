// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Models;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BeatmapSkinArchiveTest
    {
        [Test]
        public void TestRequiresSkinAsset()
        {
            BeatmapSetInfo beatmapSet = TestResources.CreateTestBeatmapSetInfo(1);
            addFile(beatmapSet, "beatmap.osu");
            addFile(beatmapSet, "hit0.wav");
            addFile(beatmapSet, "custom-image.png");

            Assert.That(BeatmapSkinArchive.HasCustomSkin(beatmapSet, new Storyboard(), beatmapSet.Files.Select(f => f.Filename)), Is.False);
            Assert.That(BeatmapSkinArchive.CreateArchive(beatmapSet, new Storyboard(), _ => new MemoryStream(), out _), Is.Null);

            addFile(beatmapSet, "hitcircle.png");

            Assert.That(BeatmapSkinArchive.HasCustomSkin(beatmapSet, new Storyboard(), beatmapSet.Files.Select(f => f.Filename)), Is.True);
        }

        [Test]
        public void TestStoryboardResourcesAreNotSkin()
        {
            BeatmapSetInfo beatmapSet = TestResources.CreateTestBeatmapSetInfo(1);
            addFile(beatmapSet, "hitcircle.png");

            var storyboard = new Storyboard();
            storyboard.GetLayer("Background").Add(new StoryboardSprite(StoryboardElementSource.Shared, "hitcircle.png", Anchor.TopLeft, Vector2.Zero));

            Assert.That(BeatmapSkinArchive.HasCustomSkin(beatmapSet, storyboard, beatmapSet.Files.Select(f => f.Filename)), Is.False);
        }

        [Test]
        public void TestNestedSkinIniIsDetectedAndFlattened()
        {
            BeatmapSetInfo beatmapSet = TestResources.CreateTestBeatmapSetInfo(1);
            addFile(beatmapSet, "nested/skin.ini");
            addFile(beatmapSet, "hitcircle.png");

            Assert.That(BeatmapSkinArchive.HasCustomSkin(beatmapSet, new Storyboard(), beatmapSet.Files.Select(f => f.Filename)), Is.True);

            using MemoryStream archive = BeatmapSkinArchive.CreateArchive(beatmapSet, new Storyboard(), _ => new MemoryStream(new byte[] { 1 }), out _)!;
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, true);

            Assert.That(zip.Entries.Select(e => e.FullName), Is.EquivalentTo(new[] { "skin.ini", "hitcircle.png" }));
        }

        [Test]
        public void TestArchiveIncludesSkinAndExcludesBeatmapResources()
        {
            BeatmapSetInfo beatmapSet = TestResources.CreateTestBeatmapSetInfo(1);
            BeatmapInfo beatmap = beatmapSet.Beatmaps.Single();

            beatmap.Metadata.AudioFile = "audio.mp3";
            beatmap.Metadata.BackgroundFile = "background.jpg";

            addFile(beatmapSet, "hitcircle.png");
            addFile(beatmapSet, "hit0.wav");
            addFile(beatmapSet, "audio.mp3");
            addFile(beatmapSet, "background.jpg");
            addFile(beatmapSet, "storyboard.png");
            addFile(beatmapSet, "video.mp4");
            addFile(beatmapSet, "beatmap.osu");
            addFile(beatmapSet, "storyboard.osb");

            var storyboard = new Storyboard();
            storyboard.GetLayer("Background").Add(new StoryboardSprite(StoryboardElementSource.Shared, "storyboard.png", Anchor.TopLeft, Vector2.Zero));

            Assert.That(BeatmapSkinArchive.HasCustomSkin(beatmapSet, storyboard, beatmapSet.Files.Select(f => f.Filename)), Is.True);

            using MemoryStream archive = BeatmapSkinArchive.CreateArchive(beatmapSet, storyboard, _ => new MemoryStream(new byte[] { 1 }), out string filename)!;
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, true);

            Assert.That(filename, Does.Contain("[beatmap skin]").And.EndWith(".osk"));
            Assert.That(zip.Entries.Select(e => e.FullName), Is.EquivalentTo(new[] { "skin.ini", "hitcircle.png", "hit0.wav" }));

            using var skinIni = new StreamReader(zip.GetEntry("skin.ini")!.Open());
            string skinIniContent = skinIni.ReadToEnd();
            Assert.That(skinIniContent, Does.Contain("[General]"));
            Assert.That(skinIniContent, Does.Contain("(Beatmap Skin)"));
        }

        private static void addFile(BeatmapSetInfo beatmapSet, string filename)
        {
            beatmapSet.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = Guid.NewGuid().ToString() }, filename));
        }
    }
}
