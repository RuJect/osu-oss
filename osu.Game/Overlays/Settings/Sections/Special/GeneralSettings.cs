// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"General";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config, OsuConfigManager osuConfig, IDialogOverlay? dialogOverlay)
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
                    LabelText = "Unlock local supporter features",
                    TooltipText = "Unlocks client-side cosmetic features normally requiring osu!supporter. Server-side supporter features remain unavailable.",
                    Current = config.GetBindable<bool>(SpecialSetting.UnlockLocalSupporterFeatures),
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
                new SettingsButtonV2
                {
                    Text = "Audio offset calibration",
                    TooltipText = "Measure your audio and input latency by tapping along to a metronome.",
                    Action = () => dialogOverlay?.Push(new AudioOffsetCalibrationDialog(osuConfig.GetBindable<double>(OsuSetting.AudioOffset))),
                },
            });
        }
    }
}
