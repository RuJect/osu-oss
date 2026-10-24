// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class ThemeSettingsSubsection : SettingsSubsection
    {
        protected override LocalisableString Header => @"Theme";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config)
        {
            AddRange(new Drawable[]
            {
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
            });
        }
    }
}
