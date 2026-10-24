// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class ScoreSubmissionSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Score Submission";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config)
        {
            AddRange(new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = "Disable score submission",
                    TooltipText = "Prevents any score from being submitted to the online store.",
                    Current = config.GetBindable<bool>(SpecialSetting.DisableScoreSubmission),
                },
                new SettingsCheckbox
                {
                    LabelText = "Block incompatible mod score submission",
                    TooltipText = "Submitting scores with incompatible mods on official servers is useless (servers reject them) and dangerous (potential custom client detection).",
                    Current = config.GetBindable<bool>(SpecialSetting.BlockIncompatibleModsSubmission),
                },
                new SettingsCheckbox
                {
                    LabelText = "Block unsuccessful score submission",
                    TooltipText = "Prevents submission of failed or incomplete scores (e.g. from quitting or failing mid-play).",
                    Current = config.GetBindable<bool>(SpecialSetting.BlockUnsuccessfulScoreSubmission),
                },
            });
        }
    }
}
