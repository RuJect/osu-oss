// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Moq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Menus
{
    [TestFixture]
    public partial class TestSceneToolbar : OsuManualInputManagerTestScene
    {
        private TestToolbar toolbar;

        [Resolved]
        private IRulesetStore rulesets { get; set; }

        [Cached]
        private readonly NowPlayingOverlay nowPlayingOverlay = new NowPlayingOverlay
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = Toolbar.HEIGHT,
        };

        [Cached]
        private readonly VolumeOverlay volumeOverlay = new VolumeOverlay
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        private readonly Mock<TestNotificationOverlay> notifications = new Mock<TestNotificationOverlay>();

        private readonly BindableInt unreadNotificationCount = new BindableInt();

        [Cached]
        private readonly SpecialConfigManager specialConfig = new SpecialConfigManager(new TemporaryNativeStorage("osu-oss-toolbar-test"));

        private Bindable<ToolbarPosition> toolbarPosition => specialConfig.GetBindable<ToolbarPosition>(SpecialSetting.ToolbarPosition);

        private Bindable<bool> invertToolbar => specialConfig.GetBindable<bool>(SpecialSetting.InvertToolbar);

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.CacheAs<INotificationOverlay>(notifications.Object);
            notifications.SetupGet(n => n.UnreadCount).Returns(unreadNotificationCount);
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            toolbarPosition.Value = ToolbarPosition.Top;
            invertToolbar.Value = false;

            Remove(nowPlayingOverlay, false);
            Remove(volumeOverlay, false);

            Children = new Drawable[]
            {
                nowPlayingOverlay,
                volumeOverlay,
                toolbar = new TestToolbar { State = { Value = Visibility.Visible } },
            };
        });

        [TestCase(ToolbarPosition.Top)]
        [TestCase(ToolbarPosition.Bottom)]
        [TestCase(ToolbarPosition.Left)]
        [TestCase(ToolbarPosition.Right)]
        public void TestPlacement(ToolbarPosition position)
        {
            AddUntilStep("toolbar visible", () => toolbar.State.Value == Visibility.Visible);

            AddStep($"set placement to {position}", () => toolbarPosition.Value = position);
            AddUntilStep("placement applied", () => toolbar.Placement == position);

            bool vertical = position is ToolbarPosition.Left or ToolbarPosition.Right;

            AddAssert("ruleset selector orientation matches", () => toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().Vertical == vertical);
            AddAssert("toolbar orientation matches", () => Precision.AlmostEquals(toolbar.DrawWidth, vertical ? Toolbar.HEIGHT : toolbar.Parent.DrawWidth));

            AddWaitStep("wait for layout", 10);

            if (vertical)
            {
                AddAssert("all buttons match bar width", () => toolbar.ChildrenOfType<ToolbarButton>().All(b => Precision.AlmostEquals(b.DrawWidth, Toolbar.HEIGHT)));
                AddAssert("all buttons within bar", () => toolbar.ChildrenOfType<ToolbarButton>().All(b =>
                    b.ScreenSpaceDrawQuad.TopLeft.X >= toolbar.ScreenSpaceDrawQuad.TopLeft.X - 0.5f
                    && b.ScreenSpaceDrawQuad.TopRight.X <= toolbar.ScreenSpaceDrawQuad.TopRight.X + 0.5f));
            }
        }

        [Test]
        public void TestPlacementChangeAtRuntime()
        {
            AddUntilStep("toolbar visible", () => toolbar.State.Value == Visibility.Visible);

            AddStep("populate icons", () =>
            {
                foreach (var b in getBarButtons().Where(b => b is not ToolbarUserButton))
                    b.SetIcon(FontAwesome.Solid.Search);
            });

            foreach (var position in new[] { ToolbarPosition.Bottom, ToolbarPosition.Left, ToolbarPosition.Right, ToolbarPosition.Top })
            {
                AddStep($"set placement to {position}", () => toolbarPosition.Value = position);
                AddUntilStep("placement applied", () => toolbar.Placement == position);
                AddWaitStep("wait for layout", 10);

                bool vertical = position is ToolbarPosition.Left or ToolbarPosition.Right;

                AddAssert("all buttons sized correctly", () => toolbar.ChildrenOfType<ToolbarButton>().All(b => vertical
                    ? Precision.AlmostEquals(b.DrawWidth, Toolbar.HEIGHT)
                    : Precision.AlmostEquals(b.DrawHeight, Toolbar.HEIGHT)));

                AddAssert("all buttons within bar", () => toolbar.ChildrenOfType<ToolbarButton>().All(b =>
                    b.ScreenSpaceDrawQuad.TopLeft.X >= toolbar.ScreenSpaceDrawQuad.TopLeft.X - 0.5f
                    && b.ScreenSpaceDrawQuad.TopRight.X <= toolbar.ScreenSpaceDrawQuad.TopRight.X + 0.5f
                    && b.ScreenSpaceDrawQuad.TopLeft.Y >= toolbar.ScreenSpaceDrawQuad.TopLeft.Y - 0.5f
                    && b.ScreenSpaceDrawQuad.BottomLeft.Y <= toolbar.ScreenSpaceDrawQuad.BottomLeft.Y + 0.5f));

                AddAssert("button icons centred", () => getBarButtons().Where(b => b is not ToolbarUserButton).All(b =>
                {
                    var icon = b.ChildrenOfType<ConstrainedIconContainer>().FirstOrDefault();

                    if (icon == null)
                        return true;

                    return Precision.AlmostEquals(icon.ScreenSpaceDrawQuad.Centre.Y, b.ScreenSpaceDrawQuad.Centre.Y)
                           && (!vertical || Precision.AlmostEquals(icon.ScreenSpaceDrawQuad.Centre.X, b.ScreenSpaceDrawQuad.Centre.X));
                }));

                AddAssert("toolbar docked to edge", () =>
                {
                    var toolbarQuad = toolbar.ScreenSpaceDrawQuad;
                    var parentQuad = toolbar.Parent!.ScreenSpaceDrawQuad;

                    return position switch
                    {
                        ToolbarPosition.Bottom => Precision.AlmostEquals(toolbarQuad.BottomLeft.Y, parentQuad.BottomLeft.Y),
                        ToolbarPosition.Right => Precision.AlmostEquals(toolbarQuad.TopRight.X, parentQuad.TopRight.X),
                        ToolbarPosition.Left => Precision.AlmostEquals(toolbarQuad.TopLeft.X, parentQuad.TopLeft.X),
                        _ => Precision.AlmostEquals(toolbarQuad.TopLeft.Y, parentQuad.TopLeft.Y),
                    };
                });
            }

            ToolbarButton[] getBarButtons()
            {
                var leftGroup = (Container<Drawable>)toolbar.ChildrenOfType<ToolbarHomeButton>().Single().Parent!;
                var rightGroup = (Container<Drawable>)toolbar.ChildrenOfType<ToolbarClock>().Single().Parent!;

                return leftGroup.Children.Concat(rightGroup.Children).OfType<ToolbarButton>().ToArray();
            }
        }

        [Test]
        public void TestVerticalLayoutDeterminism()
        {
            AddUntilStep("toolbar visible", () => toolbar.State.Value == Visibility.Visible);

            AddStep("populate icons", () =>
            {
                foreach (var b in getRightGroupItems().OfType<ToolbarButton>().Where(b => b is not ToolbarUserButton))
                    b.SetIcon(FontAwesome.Solid.Search);
            });

            string[] runtimeLayout = null;

            AddStep("set placement to left", () => toolbarPosition.Value = ToolbarPosition.Left);
            AddUntilStep("placement applied", () => toolbar.Placement == ToolbarPosition.Left);
            AddWaitStep("wait for layout", 10);

            AddAssert("right group items do not overlap", () =>
            {
                var items = getRightGroupItems().OrderBy(d => d.ScreenSpaceDrawQuad.TopLeft.Y).ToArray();

                for (int i = 1; i < items.Length; i++)
                {
                    if (items[i].ScreenSpaceDrawQuad.TopLeft.Y < items[i - 1].ScreenSpaceDrawQuad.BottomLeft.Y - 0.5f)
                        return false;
                }

                return true;
            });

            AddAssert("right group items within toolbar", () => getRightGroupItems().All(d =>
                d.ScreenSpaceDrawQuad.TopLeft.Y >= toolbar.ScreenSpaceDrawQuad.TopLeft.Y - 0.5f
                && d.ScreenSpaceDrawQuad.BottomLeft.Y <= toolbar.ScreenSpaceDrawQuad.BottomLeft.Y + 0.5f
                && d.ScreenSpaceDrawQuad.TopLeft.X >= toolbar.ScreenSpaceDrawQuad.TopLeft.X - 0.5f
                && d.ScreenSpaceDrawQuad.TopRight.X <= toolbar.ScreenSpaceDrawQuad.TopRight.X + 0.5f));

            AddAssert("user avatar is centred", () =>
            {
                var userButton = toolbar.ChildrenOfType<ToolbarUserButton>().Single();
                var avatar = userButton.ChildrenOfType<UpdateableAvatar>().Single();

                return Precision.AlmostEquals(avatar.ScreenSpaceDrawQuad.Centre.X, userButton.ScreenSpaceDrawQuad.Centre.X)
                       && Precision.AlmostEquals(avatar.ScreenSpaceDrawQuad.Centre.Y, userButton.ScreenSpaceDrawQuad.Centre.Y);
            });

            AddAssert("button icons are centred", () => getRightGroupItems().OfType<ToolbarButton>()
                                                                          .Where(b => b is not ToolbarUserButton)
                                                                          .All(b =>
                                                                          {
                                                                              var icon = b.ChildrenOfType<ConstrainedIconContainer>().FirstOrDefault();

                                                                              return icon == null
                                                                                     || (Precision.AlmostEquals(icon.ScreenSpaceDrawQuad.Centre.X, b.ScreenSpaceDrawQuad.Centre.X)
                                                                                         && Precision.AlmostEquals(icon.ScreenSpaceDrawQuad.Centre.Y, b.ScreenSpaceDrawQuad.Centre.Y));
                                                                          }));

            AddAssert("all ruleset tabs are visible", () =>
            {
                var selector = toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single();
                var tabs = selector.ChildrenOfType<ToolbarRulesetTabButton>().OrderBy(t => t.ScreenSpaceDrawQuad.TopLeft.Y).ToArray();

                if (tabs.Length < 2 || tabs.Any(t => !t.IsPresent))
                    return false;

                for (int i = 1; i < tabs.Length; i++)
                {
                    if (tabs[i].ScreenSpaceDrawQuad.TopLeft.Y < tabs[i - 1].ScreenSpaceDrawQuad.BottomLeft.Y - 0.5f)
                        return false;
                }

                return tabs.All(t => t.ScreenSpaceDrawQuad.TopLeft.Y >= selector.ScreenSpaceDrawQuad.TopLeft.Y - 0.5f
                                     && t.ScreenSpaceDrawQuad.BottomLeft.Y <= selector.ScreenSpaceDrawQuad.BottomLeft.Y + 0.5f);
            });

            AddAssert("analog clock is centred", () =>
            {
                var clock = toolbar.ChildrenOfType<ToolbarClock>().Single();
                var analog = clock.ChildrenOfType<AnalogClockDisplay>().Single();

                return Precision.AlmostEquals(analog.ScreenSpaceDrawQuad.Centre.X, clock.ScreenSpaceDrawQuad.Centre.X)
                       && Precision.AlmostEquals(analog.ScreenSpaceDrawQuad.Centre.Y, clock.ScreenSpaceDrawQuad.Centre.Y);
            });

            AddStep("capture runtime layout", () => runtimeLayout = captureRightGroupLayout());

            AddStep("set placement to top", () => toolbarPosition.Value = ToolbarPosition.Top);
            AddUntilStep("placement applied", () => toolbar.Placement == ToolbarPosition.Top);
            AddWaitStep("wait for layout", 10);

            AddStep("set placement to left again", () => toolbarPosition.Value = ToolbarPosition.Left);
            AddUntilStep("placement applied", () => toolbar.Placement == ToolbarPosition.Left);
            AddWaitStep("wait for layout", 10);

            AddAssert("layout stable after switching", () => captureRightGroupLayout().SequenceEqual(runtimeLayout));

            AddStep("recreate toolbar with left preset", () =>
            {
                Remove(toolbar, true);
                toolbarPosition.Value = ToolbarPosition.Left;
                Add(toolbar = new TestToolbar { State = { Value = Visibility.Visible } });
            });
            AddUntilStep("toolbar visible", () => toolbar.State.Value == Visibility.Visible);
            AddWaitStep("wait for layout", 10);

            AddAssert("layout matches fresh start", () => captureRightGroupLayout().SequenceEqual(runtimeLayout));

            Drawable[] getRightGroupItems() => ((Container<Drawable>)toolbar.ChildrenOfType<ToolbarClock>().Single().Parent!).Children.ToArray();

            string[] captureRightGroupLayout() => getRightGroupItems()
                                                 .Select(d => $"{d.GetType().Name}:{d.ScreenSpaceDrawQuad.TopLeft.Y - toolbar.ScreenSpaceDrawQuad.TopLeft.Y:0.##}")
                                                 .ToArray();
        }

        [Test]
        public void TestInvert()
        {
            AddUntilStep("toolbar visible", () => toolbar.State.Value == Visibility.Visible);

            ToolbarSettingsButton settings = null;
            ToolbarHomeButton home = null;
            ToolbarNewsButton news = null;
            ToolbarNotificationButton notification = null;
            string[] naturalTabs = null;

            AddStep("retrieve buttons", () =>
            {
                settings = toolbar.ChildrenOfType<ToolbarSettingsButton>().Single();
                home = toolbar.ChildrenOfType<ToolbarHomeButton>().Single();
                news = toolbar.ChildrenOfType<ToolbarNewsButton>().Single();
                notification = toolbar.ChildrenOfType<ToolbarNotificationButton>().Single();
            });

            AddUntilStep("natural layout", () =>
            {
                naturalTabs = getTabOrder();
                return settings.ScreenSpaceDrawQuad.Centre.X < home.ScreenSpaceDrawQuad.Centre.X
                       && news.ScreenSpaceDrawQuad.Centre.X < notification.ScreenSpaceDrawQuad.Centre.X
                       && naturalTabs.Length > 1;
            });

            AddStep("invert toolbar", () => invertToolbar.Value = true);

            AddUntilStep("groups swapped", () => settings.ScreenSpaceDrawQuad.Centre.X > notification.ScreenSpaceDrawQuad.Centre.X);
            AddUntilStep("left group reversed", () => home.ScreenSpaceDrawQuad.Centre.X < settings.ScreenSpaceDrawQuad.Centre.X);
            AddUntilStep("right group reversed", () => notification.ScreenSpaceDrawQuad.Centre.X < news.ScreenSpaceDrawQuad.Centre.X);
            AddUntilStep("ruleset tabs reversed", () => getTabOrder().SequenceEqual(naturalTabs.Reverse()));

            AddStep("do not invert toolbar", () => invertToolbar.Value = false);

            AddUntilStep("groups restored", () => settings.ScreenSpaceDrawQuad.Centre.X < notification.ScreenSpaceDrawQuad.Centre.X);
            AddUntilStep("left group restored", () => settings.ScreenSpaceDrawQuad.Centre.X < home.ScreenSpaceDrawQuad.Centre.X);
            AddUntilStep("right group restored", () => news.ScreenSpaceDrawQuad.Centre.X < notification.ScreenSpaceDrawQuad.Centre.X);
            AddUntilStep("ruleset tabs restored", () => getTabOrder().SequenceEqual(naturalTabs));

            string[] getTabOrder() => toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single()
                                             .ChildrenOfType<ToolbarRulesetTabButton>()
                                             .OrderBy(t => t.ScreenSpaceDrawQuad.Centre.X)
                                             .Select(t => t.Value.ShortName)
                                             .ToArray();
        }

        [Test]
        public void TestNotificationCounter()
        {
            setNotifications(1);
            setNotifications(2);
            setNotifications(3);
            setNotifications(0);
            setNotifications(144);

            void setNotifications(int count)
                => AddStep($"set notification count to {count}",
                    () => unreadNotificationCount.Value = count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRulesetSwitchingShortcut(bool toolbarHidden)
        {
            ToolbarRulesetSelector rulesetSelector = null;

            if (toolbarHidden)
                AddStep("hide toolbar", () => toolbar.Hide());

            AddStep("retrieve ruleset selector", () => rulesetSelector = toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single());

            for (int i = 0; i < 4; i++)
            {
                var expected = rulesets.AvailableRulesets.ElementAt(i);
                var numberKey = Key.Number1 + i;

                AddStep($"switch to ruleset {i} via shortcut", () =>
                {
                    InputManager.PressKey(Key.ControlLeft);
                    InputManager.Key(numberKey);
                    InputManager.ReleaseKey(Key.ControlLeft);
                });

                AddUntilStep("ruleset switched", () => rulesetSelector.Current.Value.Equals(expected));
            }
        }

        [TestCase(OverlayActivation.All)]
        [TestCase(OverlayActivation.Disabled)]
        public void TestButtonKeyboardInputRespectsOverlayActivation(OverlayActivation mode)
        {
            AddStep($"set activation mode to {mode}", () => toolbar.OverlayActivationMode.Value = mode);
            AddStep("hide toolbar", () => toolbar.Hide());

            if (mode == OverlayActivation.Disabled)
                AddAssert("check buttons not accepting input", () => InputManager.NonPositionalInputQueue.OfType<ToolbarButton>().Count(), () => Is.Zero);
            else
                AddAssert("check buttons accepting input", () => InputManager.NonPositionalInputQueue.OfType<ToolbarButton>().Count(), () => Is.Not.Zero);
        }

        [TestCase(OverlayActivation.All)]
        [TestCase(OverlayActivation.Disabled)]
        public void TestRespectsOverlayActivation(OverlayActivation mode)
        {
            AddStep($"set activation mode to {mode}", () => toolbar.OverlayActivationMode.Value = mode);
            AddStep("hide toolbar", () => toolbar.Hide());
            AddStep("try to show toolbar", () => toolbar.Show());

            if (mode == OverlayActivation.Disabled)
                AddAssert("toolbar still hidden", () => toolbar.State.Value == Visibility.Hidden);
            else
                AddAssert("toolbar is visible", () => toolbar.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestScrollInput()
        {
            OsuScrollContainer scroll = null;

            AddStep("add scroll layer", () => Add(scroll = new OsuScrollContainer
            {
                Depth = 1f,
                RelativeSizeAxes = Axes.Both,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = DrawHeight * 2,
                    Colour = ColourInfo.GradientVertical(Color4.Gray, Color4.DarkGray),
                }
            }));

            AddStep("hover toolbar", () => InputManager.MoveMouseTo(toolbar));
            AddStep("perform scroll", () => InputManager.ScrollVerticalBy(500));
            AddAssert("not scrolled", () => scroll.Current == 0);
        }

        [Test]
        public void TestVolumeControlViaMusicButtonScroll()
        {
            AddStep("hover toolbar music button", () => InputManager.MoveMouseTo(this.ChildrenOfType<ToolbarMusicButton>().Single()));

            AddStep("reset volume", () => Audio.Volume.Value = 1);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("scroll down", () => InputManager.ScrollVerticalBy(-10), 5);
            AddAssert("volume lowered down", () => Audio.Volume.Value < 1);
            AddRepeatStep("scroll up", () => InputManager.ScrollVerticalBy(10), 5);
            AddAssert("volume raised up", () => Audio.Volume.Value == 1);

            AddStep("move mouse away", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("button not hovered", () => !this.ChildrenOfType<ToolbarMusicButton>().Single().IsHovered);

            AddStep("set volume to 0.5", () => Audio.Volume.Value = 0.5);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("scroll down", () => InputManager.ScrollVerticalBy(-10), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
            AddRepeatStep("scroll up", () => InputManager.ScrollVerticalBy(10), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
        }

        [Test]
        public void TestVolumeControlViaMusicButtonArrowKeys()
        {
            AddStep("hover toolbar music button", () => InputManager.MoveMouseTo(this.ChildrenOfType<ToolbarMusicButton>().Single()));

            AddStep("reset volume", () => Audio.Volume.Value = 1);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("arrow down", () => InputManager.Key(Key.Down), 5);
            AddAssert("volume lowered down", () => Audio.Volume.Value < 1);
            AddRepeatStep("arrow up", () => InputManager.Key(Key.Up), 5);
            AddAssert("volume raised up", () => Audio.Volume.Value == 1);

            AddStep("hide volume overlay", () => volumeOverlay.Hide());
            AddStep("move mouse away", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("button not hovered", () => !this.ChildrenOfType<ToolbarMusicButton>().Single().IsHovered);

            AddStep("set volume", () => Audio.Volume.Value = 0.5);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("arrow down", () => InputManager.Key(Key.Down), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
            AddRepeatStep("arrow up", () => InputManager.Key(Key.Up), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
        }

        [Test]
        public void TestRulesetSelectorOverflow()
        {
            AddStep("set toolbar width", () =>
            {
                toolbar.RelativeSizeAxes = Axes.None;
                toolbar.Width = 400;
            });
            AddStep("move mouse over news toggle button", () =>
            {
                var button = toolbar.ChildrenOfType<ToolbarNewsButton>().Single();
                InputManager.MoveMouseTo(button);
            });
            AddAssert("no ruleset toggle buttons hovered", () => !toolbar.ChildrenOfType<ToolbarRulesetTabButton>().Any(button => button.IsHovered));
            AddUntilStep("toolbar gradient visible", () => toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single().Children.All(d => d.Alpha > 0));
        }

        public partial class TestToolbar : Toolbar
        {
            public new Bindable<OverlayActivation> OverlayActivationMode => base.OverlayActivationMode as Bindable<OverlayActivation>;
        }

        // interface mocks break hot reload, mocking this stub implementation instead works around it.
        // see: https://github.com/moq/moq4/issues/1252
        [UsedImplicitly]
        public class TestNotificationOverlay : INotificationOverlay
        {
            public virtual void Post(Notification notification)
            {
            }

            public virtual void Hide()
            {
            }

            public virtual IBindable<int> UnreadCount { get; } = new Bindable<int>();

            public IEnumerable<Notification> AllNotifications => Enumerable.Empty<Notification>();
        }
    }
}
