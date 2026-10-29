// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Models;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneBeatmapTitleWedge : SongSelectComponentsTestScene
    {
        private RulesetStore rulesets = null!;

        [Resolved]
        private SpecialConfigManager specialConfig { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private BeatmapTitleWedge titleWedge = null!;
        private BeatmapTitleWedge.DifficultyDisplay difficultyDisplay => titleWedge.ChildrenOfType<BeatmapTitleWedge.DifficultyDisplay>().Single();

        [Cached(typeof(IBindable<Screens.Select.SongSelect.BeatmapSetLookupResult?>))]
        private Bindable<Screens.Select.SongSelect.BeatmapSetLookupResult?> onlineLookupResult = new Bindable<Screens.Select.SongSelect.BeatmapSetLookupResult?>();

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            this.rulesets = rulesets;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Shear = OsuGame.SHEAR,
                    Children = new Drawable[]
                    {
                        titleWedge = new BeatmapTitleWedge
                        {
                            State = { Value = Visibility.Visible },
                        },
                    },
                }
            });

            AddSliderStep("change star difficulty", 0, 11.9, 4.18, v =>
            {
                difficultyDisplay.ChildrenOfType<StarRatingDisplay>().Single().Current.Value = new StarDifficulty(v, 0);
            });
        }

        [Test]
        public void TestRulesetChange()
        {
            selectBeatmap(Beatmap.Value.Beatmap);

            AddWaitStep("wait for select", 3);

            foreach (var rulesetInfo in rulesets.AvailableRulesets)
            {
                var testBeatmap = createTestBeatmapFromRuleset(rulesetInfo);

                setRuleset(rulesetInfo);
                selectBeatmap(testBeatmap);
            }
        }

        [Test]
        public void TestNullBeatmap()
        {
            selectBeatmap(null);
            AddAssert("check default title", () => titleWedge.DisplayedTitle == Beatmap.Default.BeatmapInfo.Metadata.Title);
            AddAssert("check default artist", () => titleWedge.DisplayedArtist == Beatmap.Default.BeatmapInfo.Metadata.Artist);
            AddAssert("statistics not visible",
                () => difficultyDisplay.ChildrenOfType<BeatmapTitleWedge.DifficultyStatisticsDisplay>()
                                       .All(d => d.Alpha == 0 || d.ChildrenOfType<BeatmapTitleWedge.StatisticDifficulty>().All(s => s.Alpha == 0)));
        }

        [Test]
        public void TestBPMUpdates()
        {
            const double bpm = 120;
            IBeatmap beatmap = createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo);
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60 * 1000 / bpm });

            OsuModDoubleTime doubleTime = null!;

            selectBeatmap(beatmap);
            checkDisplayedBPM($"{bpm}");

            AddStep("select DT", () => SelectedMods.Value = new[] { doubleTime = new OsuModDoubleTime() });
            checkDisplayedBPM($"{bpm * 1.5f}");

            AddStep("change DT rate", () => doubleTime.SpeedChange.Value = 2);
            checkDisplayedBPM($"{bpm * 2}");

            AddStep("select HT", () => SelectedMods.Value = new[] { new OsuModHalfTime() });
            checkDisplayedBPM($"{bpm * 0.75f}");
        }

        [Test]
        public void TestWedgeVisibility()
        {
            AddStep("hide", () => { titleWedge.Hide(); });
            AddWaitStep("wait for hide", 3);
            AddAssert("check visibility", () => titleWedge.Alpha == 0);
            AddStep("show", () => { titleWedge.Show(); });
            AddWaitStep("wait for show", 1);
            AddAssert("check visibility", () => titleWedge.Alpha > 0);
        }

        [Test]
        public void TestOnlineAvailability()
        {
            AddStep("online beatmapset", () => (Beatmap.Value, onlineLookupResult.Value) = createTestBeatmap());

            AddUntilStep("play count is 10000", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("10,000"));
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));
            AddStep("online beatmapset with local diff", () =>
            {
                var (working, lookupResult) = createTestBeatmap();

                working.BeatmapInfo.ResetOnlineInfo();

                Beatmap.Value = working;
                onlineLookupResult.Value = lookupResult;
            });
            AddUntilStep("play count is -", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("-"));
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));
            AddStep("local beatmapset", () =>
            {
                var (working, _) = createTestBeatmap();

                Beatmap.Value = working;
                onlineLookupResult.Value = Screens.Select.SongSelect.BeatmapSetLookupResult.Completed(null);
            });
            AddUntilStep("play count is -", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("-"));
            AddUntilStep("favourites count is -", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("-"));
        }

        [Test]
        public void TestDownloadSkinButtonVisibility()
        {
            BeatmapTitleWedge.DownloadSkinButton? button = null;

            AddStep("enable download button", () => specialConfig.GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton).Value = true);
            AddAssert("button hidden without skin", () => this.ChildrenOfType<BeatmapTitleWedge.DownloadSkinButton>().All(b => b.State.Value == Visibility.Hidden));

            AddStep("select beatmap with skin", () =>
            {
                var (working, lookupResult) = createTestBeatmap();
                working.BeatmapSetInfo.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = Guid.NewGuid().ToString() }, "hitcircle.png"));

                Beatmap.Value = working;
                onlineLookupResult.Value = lookupResult;
            });
            AddUntilStep("button visible", () => (button = this.ChildrenOfType<BeatmapTitleWedge.DownloadSkinButton>().SingleOrDefault())?.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddAssert("button height matches adjacent statistics", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().DrawHeight == button!.DrawHeight
                                                                       && this.ChildrenOfType<BeatmapTitleWedge.StatisticPlayCount>().Single().DrawHeight == button.DrawHeight);

            AddStep("disable download button", () => specialConfig.GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton).Value = false);
            AddUntilStep("button hidden when disabled", () => button!.State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddStep("reset download button setting", () => specialConfig.GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton).SetDefault());
        }

        [Test]
        public void TestDownloadSkinButtonInstallsSkin()
        {
            AddStep("enable download button", () => specialConfig.GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton).Value = true);
            AddStep("select beatmap with skin", () =>
            {
                var (working, lookupResult) = createTestBeatmapWithSkin();

                Beatmap.Value = working;
                onlineLookupResult.Value = lookupResult;
            });

            AddUntilStep("button visible", () => this.ChildrenOfType<BeatmapTitleWedge.DownloadSkinButton>().Single().State.Value, () => Is.EqualTo(Visibility.Visible));
            AddStep("click download", () => this.ChildrenOfType<BeatmapTitleWedge.DownloadSkinButton>().Single()
                                                       .ChildrenOfType<OsuClickableContainer>().Single().TriggerClick());

            AddUntilStep("skin imported", () => skins.GetAllUsableSkins().Any(s => s.Value.Name.Contains("Beatmap Skin", StringComparison.Ordinal)));
            AddUntilStep("skin selected", () => skins.CurrentSkinInfo.Value.Value.Name, () => Does.Contain("Beatmap Skin"));

            AddStep("reset download button setting", () => specialConfig.GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton).SetDefault());
        }

        [Test]
        public void TestFavouriting()
        {
            var resetEvent = new ManualResetEventSlim(false);

            AddStep("set up request handler", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    switch (request)
                    {
                        case PostBeatmapFavouriteRequest favourite:
                            Task.Run(() =>
                            {
                                resetEvent.Wait(10000);
                                favourite.TriggerSuccess();
                            });
                            return true;

                        default:
                            return false;
                    }
                };
            });

            AddStep("online beatmapset", () => (Beatmap.Value, onlineLookupResult.Value) = createTestBeatmap());

            AddUntilStep("play count is 10000", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("10,000"));
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));

            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("favourites count is 2346", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,346"));

            AddStep("reset event", resetEvent.Reset);
            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));

            AddStep("reset event", resetEvent.Reset);
            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddStep("change to another beatmap", () =>
            {
                var (working, online) = createTestBeatmap();
                online.Result!.FavouriteCount = 9999;
                online.Result!.HasFavourited = true;
                working.BeatmapSetInfo.OnlineID = online.Result!.OnlineID = 99999;

                Beatmap.Value = working;
                onlineLookupResult.Value = online;
            });
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("favourites count is 9999", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("9,999"));

            AddStep("set up request handler to fail", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    switch (request)
                    {
                        case PostBeatmapFavouriteRequest favourite:
                            Task.Run(() =>
                            {
                                resetEvent.Wait(10000);
                                favourite.TriggerFailure(new APIException("You have too many favourited beatmaps! Please unfavourite some before trying again.", null));
                            });
                            return true;

                        default:
                            return false;
                    }
                };
            });
            AddStep("reset event", resetEvent.Reset);
            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddUntilStep("spinner visible", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single()
                                                      .ChildrenOfType<LoadingSpinner>().Single().State.Value, () => Is.EqualTo(Visibility.Visible));
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("spinner hidden", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single()
                                                     .ChildrenOfType<LoadingSpinner>().Single().State.Value, () => Is.EqualTo(Visibility.Hidden));
        }

        [TestCase(120, 125, null, "120-125 (mostly 120)")]
        [TestCase(120, 120.6, null, "120-121 (mostly 120)")]
        [TestCase(120, 120.4, null, "120")]
        [TestCase(120, 120.6, "DT", "180-181 (mostly 180)")]
        [TestCase(120, 120.4, "DT", "180-181 (mostly 180)")]
        public void TestVaryingBPM(double commonBpm, double otherBpm, string? mod, string expectedDisplay)
        {
            IBeatmap beatmap = createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo);
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60 * 1000 / commonBpm });
            beatmap.ControlPointInfo.Add(100, new TimingControlPoint { BeatLength = 60 * 1000 / otherBpm });
            beatmap.ControlPointInfo.Add(200, new TimingControlPoint { BeatLength = 60 * 1000 / commonBpm });

            if (mod != null)
                AddStep($"select {mod}", () => SelectedMods.Value = new[] { Ruleset.Value.CreateInstance().CreateModFromAcronym(mod) });

            selectBeatmap(beatmap);
            checkDisplayedBPM(expectedDisplay);
        }

        [Test]
        [Explicit]
        public void TestPerformanceWithLongBeatmap()
        {
            AddStep("select heavy beatmap", () => Beatmap.Value = new HeavyWorkingBeatmap(Audio));

            foreach (var rulesetInfo in rulesets.AvailableRulesets)
                setRuleset(rulesetInfo);
        }

        private void setRuleset(RulesetInfo rulesetInfo)
        {
            AddStep("set ruleset", () => Ruleset.Value = rulesetInfo);
        }

        private void selectBeatmap(IBeatmap? b)
        {
            AddStep($"select {b?.Metadata.Title ?? "null"} beatmap", () =>
            {
                Beatmap.Value = b == null ? Beatmap.Default : CreateWorkingBeatmap(b);
            });
        }

        private void checkDisplayedBPM(string target)
        {
            AddUntilStep($"displayed bpm is {target}", () =>
            {
                var label = titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().Single(l => l.TooltipText == BeatmapsetsStrings.ShowStatsBpm);
                return label.Text.ToString() == target;
            });
        }

        private (WorkingBeatmap, Screens.Select.SongSelect.BeatmapSetLookupResult) createTestBeatmap()
        {
            var working = CreateWorkingBeatmap(Ruleset.Value);
            var onlineSet = new APIBeatmapSet
            {
                OnlineID = working.BeatmapSetInfo.OnlineID,
                FavouriteCount = 2345,
                Beatmaps = new[]
                {
                    new APIBeatmap
                    {
                        OnlineID = working.BeatmapInfo.OnlineID,
                        PlayCount = 10000,
                        PassCount = 4567,
                        UserPlayCount = 123,
                    },
                }
            };

            working.BeatmapSetInfo.DateSubmitted = DateTimeOffset.Now;
            working.BeatmapSetInfo.DateRanked = DateTimeOffset.Now;
            return (working, Screens.Select.SongSelect.BeatmapSetLookupResult.Completed(onlineSet));
        }

        private (WorkingBeatmap, Screens.Select.SongSelect.BeatmapSetLookupResult) createTestBeatmapWithSkin()
        {
            var (working, lookupResult) = createTestBeatmap();
            var files = new Dictionary<string, byte[]>();

            addFile(working.BeatmapSetInfo, files, "hitcircle.png", new byte[] { 1 });
            addFile(working.BeatmapSetInfo, files, "approachcircle.png", new byte[] { 1 });

            return (new FileWorkingBeatmap(working.BeatmapInfo, Audio, files), lookupResult);

            static void addFile(BeatmapSetInfo beatmapSet, Dictionary<string, byte[]> contents, string filename, byte[] data)
            {
                var usage = new RealmNamedFileUsage(new RealmFile { Hash = Guid.NewGuid().ToString() }, filename);
                beatmapSet.Files.Add(usage);
                contents[usage.File.GetStoragePath()] = data;
            }
        }

        private static IBeatmap createTestBeatmapFromRuleset(RulesetInfo ruleset)
        {
            List<HitObject> objects = new List<HitObject>();
            for (double i = 0; i < 50000; i += 1000)
                objects.Add(new TestHitObject { StartTime = i });

            return new Beatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Metadata = new BeatmapMetadata
                    {
                        Author = { Username = $"{ruleset.ShortName}Author" },
                        Artist = $"{ruleset.ShortName}Artist",
                        Source = $"{ruleset.ShortName}Source",
                        Title = $"{ruleset.ShortName}Title"
                    },
                    Ruleset = ruleset,
                    StarRating = 6,
                    DifficultyName = $"{ruleset.ShortName}Version",
                    Difficulty = new BeatmapDifficulty()
                },
                HitObjects = objects
            };
        }

        private class TestHitObject : ConvertHitObject;

        private class FileWorkingBeatmap : WorkingBeatmap
        {
            private readonly Dictionary<string, byte[]> files;

            public FileWorkingBeatmap(BeatmapInfo beatmapInfo, AudioManager audioManager, Dictionary<string, byte[]> files)
                : base(beatmapInfo, audioManager)
            {
                this.files = files;
            }

            protected override IBeatmap GetBeatmap() => new Beatmap { BeatmapInfo = BeatmapInfo };
            public override Texture? GetBackground() => null;
            protected override Track? GetBeatmapTrack() => null;
            protected internal override ISkin? GetSkin() => null;
            public override Stream GetStream(string storagePath) => new MemoryStream(files[storagePath]);
        }

        private class HeavyWorkingBeatmap : WorkingBeatmap
        {
            private static readonly BeatmapInfo beatmap_info = new BeatmapInfo
            {
                Metadata = new BeatmapMetadata
                {
                    Author = { Username = "osuAuthor" },
                    Artist = "osuArtist",
                    Source = "osuSource",
                    Title = "osuTitle"
                },
                Ruleset = new OsuRuleset().RulesetInfo,
                StarRating = 6,
                DifficultyName = "osuVersion",
                Difficulty = new BeatmapDifficulty()
            };

            public HeavyWorkingBeatmap(AudioManager audioManager)
                : base(beatmap_info, audioManager)
            {
            }

            protected override IBeatmap GetBeatmap()
            {
                List<HitObject> objects = new List<HitObject>();

                for (int i = 0; i < 200_000; i++)
                    objects.Add(new TestHitObject { StartTime = i * 1000 });

                return new Beatmap
                {
                    BeatmapInfo = beatmap_info,
                    HitObjects = objects
                };
            }

            public override Texture? GetBackground() => null;
            public override Stream? GetStream(string storagePath) => null;
            protected override Track? GetBeatmapTrack() => null;
            protected internal override ISkin? GetSkin() => null;
        }
    }
}
