// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.Overlays.Dialog;
using osu.Game.Theming;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class MaintenanceSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Maintenance";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config, Storage storage, IDialogOverlay? dialogOverlay)
        {
            Add(new DangerousSettingsButtonV2
            {
                Text = "Reset osu!oss data",
                Action = () => dialogOverlay?.Push(new ConfirmDialog(
                    "This will delete all osu!oss data, including settings and downloaded authentication files. This cannot be undone. Are you sure?",
                    () => resetData(storage, config))),
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
                config.GetBindable<bool>(SpecialSetting.UseSkinModIcons).Value = true;
                config.GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton).Value = true;
                config.GetBindable<bool>(SpecialSetting.UseAdjustedStarRatingForSorting).Value = true;
                config.GetBindable<bool>(SpecialSetting.UnlockLocalSupporterFeatures).Value = false;
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
                config.GetBindable<bool>(SpecialSetting.ScriptingEnabled).Value = true;
                config.GetBindable<bool>(SpecialSetting.AllowClrAccess).Value = true;

                Logger.Log("osu!oss data reset.", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to reset osu!oss data");
            }
        }
    }
}
