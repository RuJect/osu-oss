// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Game.IO;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Config manager for osu!oss-specific settings, persisted to <c>osu-oss/config.ini</c>.
    /// The manager should be constructed with a storage scoped to <see cref="SpecialStorage.GetRoot"/>.
    /// </summary>
    public class SpecialConfigManager : IniConfigManager<SpecialSetting>
    {
        protected override string Filename => SpecialStorage.CONFIG_FILENAME;

        public SpecialConfigManager(Storage storage)
            : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(SpecialSetting.ShowDevBuildBanner, false);
            SetDefault(SpecialSetting.AllowIncompatibleMods, false);
            SetDefault(SpecialSetting.ShowSystemMods, false);
            SetDefault(SpecialSetting.ShowFavouriteBeatmapsDownloadButton, false);
            SetDefault(SpecialSetting.AllowBreakSkip, false);
            SetDefault(SpecialSetting.AutoSkip, AutoSkipMode.None);
        }
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
    }
}
