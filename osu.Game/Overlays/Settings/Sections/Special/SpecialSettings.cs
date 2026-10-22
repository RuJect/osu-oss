// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.Overlays.Dialog;
using osu.Game.Theming;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class SpecialSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"osu!oss";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config, Storage storage, IDialogOverlay? dialogOverlay)
        {
            AddRange(new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = "Show developer build banner",
                    Current = config.GetBindable<bool>(SpecialSetting.ShowDevBuildBanner),
                },
                new SettingsCheckbox
                {
                    LabelText = "Allow incompatible mods",
                    Current = config.GetBindable<bool>(SpecialSetting.AllowIncompatibleMods),
                },
                new SettingsCheckbox
                {
                    LabelText = "Show system mods",
                    Current = config.GetBindable<bool>(SpecialSetting.ShowSystemMods),
                },
                new SettingsCheckbox
                {
                    LabelText = "Show \"Download all favourites\" button",
                    Current = config.GetBindable<bool>(SpecialSetting.ShowFavouriteBeatmapsDownloadButton),
                },
                new SettingsCheckbox
                {
                    LabelText = "Allow skipping break times",
                    Current = config.GetBindable<bool>(SpecialSetting.AllowBreakSkip),
                },
                new SettingsEnumDropdown<AutoSkipMode>
                {
                    LabelText = "Automatically skip",
                    Current = config.GetBindable<AutoSkipMode>(SpecialSetting.AutoSkip),
                },
                new SettingsCheckbox
                {
                    LabelText = "Use skin cursor",
                    TooltipText = "Replaces the menu cursor with the cursor from your skin.",
                    Current = config.GetBindable<bool>(SpecialSetting.UseSkinCursor),
                },
                new SettingsEnumDropdown<ToolbarPosition>
                {
                    LabelText = "Toolbar position",
                    Current = config.GetBindable<ToolbarPosition>(SpecialSetting.ToolbarPosition),
                },
                new SettingsCheckbox
                {
                    LabelText = "Invert toolbar contents",
                    Current = config.GetBindable<bool>(SpecialSetting.InvertToolbar),
                },
                new SettingsEnumDropdown<NotificationPanelPosition>
                {
                    LabelText = "Notification panel position",
                    Current = config.GetBindable<NotificationPanelPosition>(SpecialSetting.NotificationPanelPosition),
                },
                new SettingsEnumDropdown<NotificationDisplayPosition>
                {
                    LabelText = "Notification display position",
                    Current = config.GetBindable<NotificationDisplayPosition>(SpecialSetting.NotificationDisplayPosition),
                },
                new SettingsEnumDropdown<SettingsPanelPosition>
                {
                    LabelText = "Settings panel position",
                    Current = config.GetBindable<SettingsPanelPosition>(SpecialSetting.SettingsPanelPosition),
                },
                new SettingsCheckbox
                {
                    LabelText = "Icon-only settings sidebar",
                    TooltipText = "Show only section icons in the settings sidebar.",
                    Current = config.GetBindable<bool>(SpecialSetting.IconOnlySettingsSidebar),
                },
                new SettingsCheckbox
                {
                    LabelText = "Mute pop-up notifications",
                    Current = config.GetBindable<bool>(SpecialSetting.MuteToastNotifications),
                },
                new SettingsCheckbox
                {
                    LabelText = "Notification transparency",
                    Current = config.GetBindable<bool>(SpecialSetting.NotificationTransparency),
                },
                new ThemeSettings(),
                new SettingsCheckbox
                {
                    LabelText = "Aggressive theme tinting",
                    TooltipText = "Forces every colour in the selected theme to the theme's main hue. The theme preview updates immediately; the rest of the game updates after a restart.",
                    Current = config.GetBindable<bool>(SpecialSetting.AggressiveTinting),
                },
                new SettingsCheckbox
                {
                    LabelText = "Enable glass effects",
                    TooltipText = "Allows themes to blur and tint the game content behind panels. Requires a restart.",
                    Current = config.GetBindable<bool>(SpecialSetting.ThemeGlassEffects),
                },
                new DangerousSettingsButtonV2
                {
                    Text = "Reset osu!oss data",
                    Action = () => dialogOverlay?.Push(new ConfirmDialog(
                        "This will delete all osu!oss data, including settings and downloaded authentication files. This cannot be undone. Are you sure?",
                        () => resetData(storage, config))),
                },
            });
        }

        private static void resetData(Storage storage, SpecialConfigManager config)
        {
            try
            {
                string path = SpecialStorage.GetRootPath(storage);

                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                // restore in-memory defaults; the config file is recreated on next persist.
                config.GetBindable<bool>(SpecialSetting.ShowDevBuildBanner).Value = false;
                config.GetBindable<bool>(SpecialSetting.AllowIncompatibleMods).Value = false;
                config.GetBindable<bool>(SpecialSetting.ShowSystemMods).Value = false;
                config.GetBindable<bool>(SpecialSetting.ShowFavouriteBeatmapsDownloadButton).Value = false;
                config.GetBindable<bool>(SpecialSetting.AllowBreakSkip).Value = false;
                config.GetBindable<AutoSkipMode>(SpecialSetting.AutoSkip).Value = AutoSkipMode.None;
                config.GetBindable<bool>(SpecialSetting.UseSkinCursor).Value = true;
                config.GetBindable<ToolbarPosition>(SpecialSetting.ToolbarPosition).Value = ToolbarPosition.Top;
                config.GetBindable<bool>(SpecialSetting.InvertToolbar).Value = false;
                config.GetBindable<NotificationPanelPosition>(SpecialSetting.NotificationPanelPosition).Value = NotificationPanelPosition.Right;
                config.GetBindable<NotificationDisplayPosition>(SpecialSetting.NotificationDisplayPosition).Value = NotificationDisplayPosition.TopRight;
                config.GetBindable<SettingsPanelPosition>(SpecialSetting.SettingsPanelPosition).Value = SettingsPanelPosition.Left;
                config.GetBindable<bool>(SpecialSetting.IconOnlySettingsSidebar).Value = false;
                config.GetBindable<bool>(SpecialSetting.NotificationTransparency).Value = false;
                config.GetBindable<bool>(SpecialSetting.MuteToastNotifications).Value = false;
                config.GetBindable<string>(SpecialSetting.Theme).Value = ThemeManager.DEFAULT_THEME;
                config.GetBindable<bool>(SpecialSetting.ThemeGlassEffects).Value = true;
                config.GetBindable<bool>(SpecialSetting.AggressiveTinting).Value = true;

                Logger.Log("osu!oss data reset.", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to reset osu!oss data");
            }
        }
    }
}
