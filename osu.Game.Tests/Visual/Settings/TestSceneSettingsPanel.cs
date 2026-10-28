// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics.Backdrops;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Input;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneSettingsPanel : OsuManualInputManagerTestScene
    {
        private SettingsPanel settings;
        private DialogOverlay dialogOverlay;

        [Cached]
        private readonly SpecialConfigManager specialConfig = new SpecialConfigManager(new TemporaryNativeStorage("osu-oss-settings-panel-test"));

        private Bindable<SettingsPanelPosition> panelPosition => specialConfig.GetBindable<SettingsPanelPosition>(SpecialSetting.SettingsPanelPosition);

        private Bindable<bool> iconOnlySidebar => specialConfig.GetBindable<bool>(SpecialSetting.IconOnlySettingsSidebar);

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset panel position", () => panelPosition.Value = SettingsPanelPosition.Left);
            AddStep("reset sidebar mode", () => iconOnlySidebar.Value = false);

            AddStep("create settings", () =>
            {
                settings?.Expire();

                Add(settings = new SettingsOverlay
                {
                    State = { Value = Visibility.Visible }
                });
            });
        }

        [Test]
        public void TestBasic()
        {
            AddStep("do nothing", () => { });
            AddToggleStep("toggle visibility", visible => settings.State.Value = visible ? Visibility.Visible : Visibility.Hidden);
        }

        [Test]
        public void TestSubPanelPositioning()
        {
            AddUntilStep("wait for load", () => settings.ChildrenOfType<KeyBindingPanel>().Any());

            KeyBindingPanel panel = null!;
            SettingsSidebar panelSidebar = null!;
            SettingsPanel.SettingsSectionsContainer panelContent = null!;
            SettingsSidebar settingsSidebar = null!;

            AddStep("get sub panel", () =>
            {
                panel = settings.ChildrenOfType<KeyBindingPanel>().Single();
                panelSidebar = panel.ChildrenOfType<SettingsSidebar>().Single(s => s.Parent == panel);
                panelContent = panel.ChildrenOfType<SettingsPanel.SettingsSectionsContainer>().Single();
                settingsSidebar = settings.ChildrenOfType<SettingsSidebar>().Single(s => s.Parent == settings);
            });

            AddStep("open sub panel", () => panel.Show());
            AddWaitStep("wait for transition", 60);

            assertDocked(SettingsPanelPosition.Left);

            AddStep("dock right", () => panelPosition.Value = SettingsPanelPosition.Right);
            AddWaitStep("wait for transition", 60);

            assertDocked(SettingsPanelPosition.Right);

            AddStep("dock left", () => panelPosition.Value = SettingsPanelPosition.Left);
            AddWaitStep("wait for transition", 60);

            assertDocked(SettingsPanelPosition.Left);

            AddStep("close sub panel", () => panel.Hide());
            AddWaitStep("wait for transition", 60);

            AddAssert("sub panel hidden", () => panel.State.Value == Visibility.Hidden);
            AddAssert("sections visible again", () => settings.SectionsContainer.Alpha == 1);
            AddAssert("settings sidebar expanded again", () => Precision.AlmostEquals(settingsSidebar.DrawWidth, SettingsSidebar.EXPANDED_WIDTH));

            AddStep("dock right while closed", () => panelPosition.Value = SettingsPanelPosition.Right);
            AddWaitStep("wait for transition", 60);

            AddStep("open sub panel again", () => panel.Show());
            AddWaitStep("wait for transition", 60);

            assertDocked(SettingsPanelPosition.Right);

            AddStep("close sub panel", () => panel.Hide());
            AddWaitStep("wait for transition", 60);

            AddStep("dock left while closed", () => panelPosition.Value = SettingsPanelPosition.Left);
            AddWaitStep("wait for transition", 60);

            AddStep("open sub panel again", () => panel.Show());
            AddWaitStep("wait for transition", 60);

            assertDocked(SettingsPanelPosition.Left);

            AddStep("close sub panel", () => panel.Hide());
            AddWaitStep("wait for transition", 60);

            AddAssert("sub panel hidden again", () => panel.State.Value == Visibility.Hidden);
            AddAssert("settings sidebar expanded again", () => Precision.AlmostEquals(settingsSidebar.DrawWidth, SettingsSidebar.EXPANDED_WIDTH));

            void assertDocked(SettingsPanelPosition expectedPosition)
            {
                AddAssert($"sub panel docked {expectedPosition}", () => panel.HostPosition == expectedPosition);
                AddAssert("sub panel visible", () => panel.State.Value == Visibility.Visible && panel.Alpha == 1);
                AddAssert("sub panel has expected width", () => Precision.AlmostEquals(panel.DrawWidth, SettingsPanel.WIDTH));
                AddAssert("sub panel content has expected width", () => Precision.AlmostEquals(panelContent.DrawWidth, SettingsPanel.PANEL_WIDTH));

                AddAssert("settings sidebar collapsed", () => Precision.AlmostEquals(settingsSidebar.DrawWidth, SettingsSidebar.CONTRACTED_WIDTH));

                AddAssert("sub panel is attached to the settings sidebar", () =>
                    expectedPosition == SettingsPanelPosition.Left
                        ? Precision.AlmostEquals(panel.ScreenSpaceDrawQuad.TopLeft.X, settingsSidebar.ScreenSpaceDrawQuad.TopRight.X)
                        : Precision.AlmostEquals(panel.ScreenSpaceDrawQuad.TopRight.X, settingsSidebar.ScreenSpaceDrawQuad.TopLeft.X));

                AddAssert("sub panel content and sidebar do not overlap", () =>
                    expectedPosition == SettingsPanelPosition.Left
                        ? Precision.AlmostEquals(panelSidebar.ScreenSpaceDrawQuad.TopRight.X, panelContent.ScreenSpaceDrawQuad.TopLeft.X)
                        : Precision.AlmostEquals(panelContent.ScreenSpaceDrawQuad.TopRight.X, panelSidebar.ScreenSpaceDrawQuad.TopLeft.X));

                AddAssert("sub panel renders in front of the settings backdrop", () =>
                {
                    // higher depth is drawn behind (see Drawable.Depth).
                    var backdrop = settings.ChildrenOfType<GlassBackdrop>().First(b => b.Parent == panel.Parent);
                    return panel.Depth < backdrop.Depth;
                });

                AddAssert("sub panel is within the window", () =>
                {
                    var parentQuad = settings.Parent.ScreenSpaceDrawQuad;
                    var panelQuad = panel.ScreenSpaceDrawQuad;

                    return panelQuad.TopLeft.X >= parentQuad.TopLeft.X
                           && panelQuad.BottomRight.X <= parentQuad.BottomRight.X
                           && panelQuad.TopLeft.Y >= parentQuad.TopLeft.Y
                           && panelQuad.BottomRight.Y <= parentQuad.BottomRight.Y;
                });

                AddAssert("sub panel has expected vertical bounds", () =>
                    Precision.AlmostEquals(panel.ScreenSpaceDrawQuad.TopLeft.Y, settings.ScreenSpaceDrawQuad.TopLeft.Y)
                    && Precision.AlmostEquals(panel.ScreenSpaceDrawQuad.BottomRight.Y, settings.ScreenSpaceDrawQuad.BottomRight.Y));
            }
        }

        [Test]
        public void TestPanelPosition()
        {
            AddUntilStep("settings loaded", () => settings.IsLoaded);

            AddUntilStep("docked left", () => Precision.AlmostEquals(settings.ScreenSpaceDrawQuad.TopLeft.X, settings.Parent.ScreenSpaceDrawQuad.TopLeft.X));
            AddUntilStep("sidebar on left", () => Precision.AlmostEquals(settings.ChildrenOfType<SettingsSidebar>().Single(s => s.Parent == settings).ScreenSpaceDrawQuad.TopLeft.X, settings.ScreenSpaceDrawQuad.TopLeft.X));

            AddStep("dock right", () => panelPosition.Value = SettingsPanelPosition.Right);

            AddUntilStep("docked right", () => Precision.AlmostEquals(settings.ScreenSpaceDrawQuad.TopRight.X, settings.Parent.ScreenSpaceDrawQuad.TopRight.X));
            AddUntilStep("sidebar on right", () => Precision.AlmostEquals(settings.ChildrenOfType<SettingsSidebar>().Single(s => s.Parent == settings).ScreenSpaceDrawQuad.TopRight.X, settings.ScreenSpaceDrawQuad.TopRight.X));
            AddAssert("panel reports right position", () => settings.PanelPosition == SettingsPanelPosition.Right);
            AddAssert("panel has expected width", () => Precision.AlmostEquals(settings.DrawWidth, SettingsPanel.WIDTH));

            AddStep("dock left", () => panelPosition.Value = SettingsPanelPosition.Left);

            AddUntilStep("docked left again", () => Precision.AlmostEquals(settings.ScreenSpaceDrawQuad.TopLeft.X, settings.Parent.ScreenSpaceDrawQuad.TopLeft.X));
            AddUntilStep("sidebar on left again", () => Precision.AlmostEquals(settings.ChildrenOfType<SettingsSidebar>().Single(s => s.Parent == settings).ScreenSpaceDrawQuad.TopLeft.X, settings.ScreenSpaceDrawQuad.TopLeft.X));
            AddAssert("panel reports left position", () => settings.PanelPosition == SettingsPanelPosition.Left);
        }

        [Test]
        public void TestIconOnlySidebar()
        {
            AddUntilStep("wait for load", () => settings.ChildrenOfType<KeyBindingPanel>().Any());

            SettingsSidebar settingsSidebar = null!;
            KeyBindingPanel panel = null!;

            AddStep("get panels", () =>
            {
                settingsSidebar = settings.ChildrenOfType<SettingsSidebar>().Single(s => s.Parent == settings);
                panel = settings.ChildrenOfType<KeyBindingPanel>().Single();
            });

            AddUntilStep("sidebar expanded by default", () => Precision.AlmostEquals(settingsSidebar.DrawWidth, SettingsSidebar.EXPANDED_WIDTH));
            AddUntilStep("panel width expanded", () => Precision.AlmostEquals(settings.DrawWidth, SettingsPanel.WIDTH));

            AddUntilStep("ruleset sidebar buttons loaded", () => getSmallerSidebarButtons().Any());
            AddUntilStep("smaller buttons indented", () => getSmallerSidebarButtons().All(b => Precision.AlmostEquals(b.Padding.Left, 25)));

            AddStep("enable icon-only sidebar", () => iconOnlySidebar.Value = true);

            AddUntilStep("sidebar contracted", () => Precision.AlmostEquals(settingsSidebar.DrawWidth, SettingsSidebar.CONTRACTED_WIDTH));
            AddUntilStep("panel width contracted", () => Precision.AlmostEquals(settings.DrawWidth, SettingsPanel.COMPACT_WIDTH));
            AddUntilStep("smaller buttons use compact padding", () => getSmallerSidebarButtons().All(b => Precision.AlmostEquals(b.Padding.Left, 5)));
            AddAssert("all sidebar icons are visible", () => settingsSidebar.ChildrenOfType<SidebarIconButton>().All(button =>
            {
                float contentLeft = button.ScreenSpaceDrawQuad.TopLeft.X + button.Padding.Left;
                float contentRight = button.ScreenSpaceDrawQuad.TopRight.X - button.Padding.Right;

                return button.ChildrenOfType<ConstrainedIconContainer>().All(icon =>
                    icon.ScreenSpaceDrawQuad.TopLeft.X >= contentLeft
                    && icon.ScreenSpaceDrawQuad.BottomRight.X <= contentRight);
            }));
            AddAssert("content adjacent to sidebar", () => Precision.AlmostEquals(settings.SectionsContainer.ScreenSpaceDrawQuad.TopLeft.X, settingsSidebar.ScreenSpaceDrawQuad.TopRight.X));

            AddStep("dock right", () => panelPosition.Value = SettingsPanelPosition.Right);

            AddUntilStep("docked right", () => Precision.AlmostEquals(settings.ScreenSpaceDrawQuad.TopRight.X, settings.Parent.ScreenSpaceDrawQuad.TopRight.X));
            AddUntilStep("sidebar on right", () => Precision.AlmostEquals(settingsSidebar.ScreenSpaceDrawQuad.TopRight.X, settings.ScreenSpaceDrawQuad.TopRight.X));
            AddAssert("content adjacent to sidebar on right", () => Precision.AlmostEquals(settings.SectionsContainer.ScreenSpaceDrawQuad.TopRight.X, settingsSidebar.ScreenSpaceDrawQuad.TopLeft.X));
            AddAssert("panel width still contracted", () => Precision.AlmostEquals(settings.DrawWidth, SettingsPanel.COMPACT_WIDTH));

            AddStep("open sub panel", () => panel.Show());
            AddUntilStep("sub panel visible", () => panel.State.Value == Visibility.Visible);
            AddWaitStep("wait for transition", 60);

            AddAssert("root sidebar stays contracted", () => Precision.AlmostEquals(settingsSidebar.DrawWidth, SettingsSidebar.CONTRACTED_WIDTH));
            AddAssert("panel covers sub panel", () => Precision.AlmostEquals(settings.DrawWidth, SettingsPanel.WIDTH));

            AddStep("close sub panel", () => panel.Hide());
            AddWaitStep("wait for transition", 60);

            AddUntilStep("panel width contracted again", () => Precision.AlmostEquals(settings.DrawWidth, SettingsPanel.COMPACT_WIDTH));

            AddStep("disable icon-only sidebar", () => iconOnlySidebar.Value = false);
            AddUntilStep("sidebar expanded again", () => Precision.AlmostEquals(settingsSidebar.DrawWidth, SettingsSidebar.EXPANDED_WIDTH));
            AddUntilStep("panel width expanded again", () => Precision.AlmostEquals(settings.DrawWidth, SettingsPanel.WIDTH));
            AddUntilStep("smaller buttons indented again", () => getSmallerSidebarButtons().All(b => Precision.AlmostEquals(b.Padding.Left, 25)));

            SidebarIconButton[] getSmallerSidebarButtons() => settingsSidebar.ChildrenOfType<SidebarIconButton>()
                                                                           .Where(b => b.Section.UseSmallerSidebarButton)
                                                                           .ToArray();
        }

        [Test]
        public void TestFiltering([Values] bool beforeLoad)
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            if (beforeLoad)
                AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");

            AddUntilStep("wait for items to load", () => settings.SectionsContainer.ChildrenOfType<IFilterable>().Any());

            if (!beforeLoad)
                AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");

            AddAssert("ensure all items match filter", () => settings.SectionsContainer
                                                                     .ChildrenOfType<SettingsSection>().Where(f => f.IsPresent)
                                                                     .All(section =>
                                                                         section.Children.Where(f => f.IsPresent)
                                                                                .OfType<ISettingsItem>()
                                                                                .OfType<IFilterable>()
                                                                                .All(f => f.FilterTerms.Any(t => t.ToString().Contains("scaling")))
                                                                     ));

            AddAssert("ensure section is current", () => settings.CurrentSection.Value is GraphicsSection);
            AddAssert("ensure section is placed first", () => settings.CurrentSection.Value.Y == 0);
        }

        [Test]
        public void TestFilterAfterLoad()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("wait for items to load", () => settings.SectionsContainer.ChildrenOfType<IFilterable>().Any());

            AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");
        }

        [Test]
        public void ToggleVisibility()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddWaitStep("wait some", 5);
            AddToggleStep("toggle visibility", _ => settings.ToggleVisibility());
        }

        [Test]
        public void TestTextboxFocusAfterNestedPanelBackButton()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("sections loaded", () => settings.SectionsContainer.Children.Count > 0);
            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("open key binding subpanel", () =>
            {
                settings.SectionsContainer
                        .ChildrenOfType<InputSection>().FirstOrDefault()?
                        .ChildrenOfType<OsuButton>().FirstOrDefault()?
                        .TriggerClick();
            });

            AddUntilStep("binding panel textbox focused", () => settings
                                                                .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                                                .ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("Press back", () => settings
                                        .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                        .ChildrenOfType<SettingsSidebar.BackButton>().FirstOrDefault()?.TriggerClick());

            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);
        }

        [Test]
        public void TestTextboxFocusAfterNestedPanelEscape()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("sections loaded", () => settings.SectionsContainer.Children.Count > 0);
            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("open key binding subpanel", () =>
            {
                settings.SectionsContainer
                        .ChildrenOfType<InputSection>().FirstOrDefault()?
                        .ChildrenOfType<OsuButton>().FirstOrDefault()?
                        .TriggerClick();
            });

            AddUntilStep("binding panel textbox focused", () => settings
                                                                .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                                                .ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("Escape", () => InputManager.Key(Key.Escape));

            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);
        }

        [Test]
        public void TestSearchTextBoxSelectedOnShow()
        {
            SettingsSearchTextBox searchTextBox = null!;

            AddStep("set text", () => (searchTextBox = settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First()).Current.Value = "some text");
            AddAssert("no text selected", () => searchTextBox.SelectedText == string.Empty);
            AddRepeatStep("toggle visibility", () => settings.ToggleVisibility(), 2);
            AddAssert("search text selected", () => searchTextBox.SelectedText == searchTextBox.Current.Value);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(dialogOverlay = new DialogOverlay
            {
                Depth = -1
            });

            Dependencies.CacheAs<IDialogOverlay>(dialogOverlay);
        }
    }
}
