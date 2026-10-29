// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.Theming;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Config manager for osu!oss-specific settings, persisted to <c>osu-oss/config.ini</c>.
    /// The manager should be constructed with a storage scoped to <see cref="SpecialStorage.GetRoot"/>.
    /// </summary>
    public class SpecialConfigManager : IniConfigManager<SpecialSetting>
    {
        /// <summary>
        /// Environment variable used by tests to restore upstream-compatible defaults for fork features.
        /// </summary>
        public const string DISABLE_FORK_DEFAULTS_ENV = "OSU_OSS_DISABLE_FORK_DEFAULTS";

        protected override string Filename => SpecialStorage.CONFIG_FILENAME;

        public SpecialConfigManager(Storage storage)
            : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            bool forkDefaults = !isTruthy(Environment.GetEnvironmentVariable(DISABLE_FORK_DEFAULTS_ENV));

            SetDefault(SpecialSetting.ShowDevBuildBanner, false);
            SetDefault(SpecialSetting.AllowIncompatibleMods, false);
            SetDefault(SpecialSetting.ShowSystemMods, false);
            SetDefault(SpecialSetting.ShowFavouriteBeatmapsDownloadButton, false);
            SetDefault(SpecialSetting.AllowBreakSkip, false);
            SetDefault(SpecialSetting.AutoSkip, AutoSkipMode.None);
            SetDefault(SpecialSetting.UseSkinCursor, forkDefaults);
            SetDefault(SpecialSetting.ToolbarPosition, ToolbarPosition.Top);
            SetDefault(SpecialSetting.InvertToolbar, false);
            SetDefault(SpecialSetting.NotificationPanelPosition, NotificationPanelPosition.Right);
            SetDefault(SpecialSetting.NotificationDisplayPosition, NotificationDisplayPosition.TopRight);
            SetDefault(SpecialSetting.SettingsPanelPosition, SettingsPanelPosition.Left);
            SetDefault(SpecialSetting.IconOnlySettingsSidebar, false);
            SetDefault(SpecialSetting.NotificationTransparency, false);
            SetDefault(SpecialSetting.MuteToastNotifications, false);
            SetDefault(SpecialSetting.Theme, ThemeManager.DEFAULT_THEME);
            SetDefault(SpecialSetting.ThemeGlassEffects, forkDefaults);
            SetDefault(SpecialSetting.AggressiveTinting, forkDefaults);
            SetDefault(SpecialSetting.ScriptingEnabled, forkDefaults);
            SetDefault(SpecialSetting.AllowClrAccess, forkDefaults);
            SetDefault(SpecialSetting.UseSkinModIcons, forkDefaults);
            SetDefault(SpecialSetting.ShowBeatmapSkinDownloadButton, forkDefaults);
            SetDefault(SpecialSetting.UseAdjustedStarRatingForSorting, forkDefaults);
            SetDefault(SpecialSetting.HighlightFavouriteBeatmapSets, forkDefaults);
            SetDefault(SpecialSetting.UnlockLocalSupporterFeatures, false);
            SetDefault(SpecialSetting.DisableScoreSubmission, false);
            SetDefault(SpecialSetting.BlockIncompatibleModsSubmission, forkDefaults);
            SetDefault(SpecialSetting.BlockUnsuccessfulScoreSubmission, false);

            if (!forkDefaults)
            {
                GetBindable<bool>(SpecialSetting.UseSkinCursor).Value = false;
                GetBindable<bool>(SpecialSetting.ThemeGlassEffects).Value = false;
                GetBindable<bool>(SpecialSetting.AggressiveTinting).Value = false;
                GetBindable<bool>(SpecialSetting.ScriptingEnabled).Value = false;
                GetBindable<bool>(SpecialSetting.AllowClrAccess).Value = false;
                GetBindable<bool>(SpecialSetting.UseSkinModIcons).Value = false;
                GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton).Value = false;
                GetBindable<bool>(SpecialSetting.UseAdjustedStarRatingForSorting).Value = false;
                GetBindable<bool>(SpecialSetting.HighlightFavouriteBeatmapSets).Value = false;
                GetBindable<bool>(SpecialSetting.BlockIncompatibleModsSubmission).Value = false;
            }
        }

        private static bool isTruthy(string? value)
            => value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }


    public enum SpecialSetting
    {
        /// <summary>Whether the "DEVELOPER BUILD" banner should be displayed on the intro/main menu.</summary>
        ShowDevBuildBanner,

        /// <summary>Whether incompatible mods can be selected in user mod select overlays without being automatically deselected or highlighted.</summary>
        AllowIncompatibleMods,

        /// <summary>Whether system mods are displayed as a selectable column in mod select overlays.</summary>
        ShowSystemMods,

        /// <summary>Whether the "Download all favourites" button is displayed in the favourite beatmaps section of user profiles.</summary>
        ShowFavouriteBeatmapsDownloadButton,

        /// <summary>Whether break times can be skipped during gameplay (via the skip button or skip hotkey).</summary>
        AllowBreakSkip,

        /// <summary>Which parts of a beatmap's dead time are automatically skipped during gameplay.</summary>
        AutoSkip,

        /// <summary>Whether the menu cursor should use the active skin's cursor instead of the default menu cursor.</summary>
        UseSkinCursor,

        /// <summary>The screen edge the toolbar is docked to.</summary>
        ToolbarPosition,

        /// <summary>Whether the toolbar is mirrored along its main axis (groups swapped, contents and ruleset list reversed).</summary>
        InvertToolbar,

        /// <summary>The screen edge the notification history panel is docked to.</summary>
        NotificationPanelPosition,

        /// <summary>The screen corner in which temporary notifications are displayed.</summary>
        NotificationDisplayPosition,

        /// <summary>The screen edge the settings panel is docked to.</summary>
        SettingsPanelPosition,

        /// <summary>Whether the settings sidebar should display only section icons, without labels.</summary>
        IconOnlySettingsSidebar,

        /// <summary>Whether notifications gradually become transparent while disappearing.</summary>
        NotificationTransparency,

        /// <summary>Whether pop-up (toast) notifications should be suppressed and only shown in the notification panel.</summary>
        MuteToastNotifications,

        /// <summary>The identifier of the active UI theme. Changing this requires a restart.</summary>
        Theme,

        /// <summary>Whether themes which request glass surfaces are allowed to use backdrop blur effects.</summary>
        ThemeGlassEffects,

        /// <summary>Whether all theme colours should have their hue replaced by the theme's main hue.</summary>
        AggressiveTinting,

        /// <summary>Whether JavaScript scripts are allowed to run.</summary>
        ScriptingEnabled,

        /// <summary>Whether scripts may access arbitrary .NET types via the CLR bridge.</summary>
        AllowClrAccess,

        /// <summary>Whether any score submission to the online store should be disabled entirely.</summary>
        DisableScoreSubmission,

        /// <summary>Whether scores played with mutually incompatible mods should not be submitted.</summary>
        BlockIncompatibleModsSubmission,

        /// <summary>Whether failed or incomplete scores should not be submitted.</summary>
        BlockUnsuccessfulScoreSubmission,

        /// <summary>Whether mod icons should be resolved from the current skin.</summary>
        UseSkinModIcons,

        /// <summary>Whether beatmaps with custom skins should display a button to install the skin.</summary>
        ShowBeatmapSkinDownloadButton,

        /// <summary>
        /// Whether difficulties within an expanded beatmap set should be ordered using the star rating
        /// for the current ruleset and mods rather than the beatmap's stored star rating.
        /// </summary>
        UseAdjustedStarRatingForSorting,

        /// <summary>Whether favourited beatmap sets should be highlighted in song select.</summary>
        HighlightFavouriteBeatmapSets,

        /// <summary>
        /// Whether client-side cosmetic features normally requiring osu!supporter should be unlocked.
        /// Server-side supporter features are unaffected.
        /// </summary>
        UnlockLocalSupporterFeatures,
    }
}
