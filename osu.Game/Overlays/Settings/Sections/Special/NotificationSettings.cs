// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class NotificationSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Notifications";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config)
        {
            AddRange(new Drawable[]
            {
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
            });
        }
    }
}
