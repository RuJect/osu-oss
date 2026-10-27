// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class SkinningSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Skinning";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config)
        {
            AddRange(new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = "Use skin cursor",
                    TooltipText = "Replaces the menu cursor with the cursor from your skin.",
                    Current = config.GetBindable<bool>(SpecialSetting.UseSkinCursor),
                },
                new SettingsCheckbox
                {
                    LabelText = "Use skin mod icons",
                    TooltipText = "Use mod icons from the current skin when available.",
                    Current = config.GetBindable<bool>(SpecialSetting.UseSkinModIcons),
                },
            });
        }
    }
}
