// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class InterfaceSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Interface";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config)
        {
            AddRange(new Drawable[]
            {
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
            });
        }
    }
}
