// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Configuration;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class SongSelectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Song Select";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config)
        {
            Add(new SettingsCheckbox
            {
                LabelText = "Re-sort difficulties by adjusted star rating",
                TooltipText = "Orders difficulties within an expanded beatmap set using star rating for the current ruleset and mods. Useful for converted beatmaps and difficulty-changing mods.",
                Current = config.GetBindable<bool>(SpecialSetting.UseAdjustedStarRatingForSorting),
            });
        }
    }
}
