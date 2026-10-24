// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings.Sections.Special;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// Holds osu!oss-specific settings.
    /// </summary>
    public partial class SpecialSection : SettingsSection
    {
        public override LocalisableString Header => @"Special";

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Star
        };

        public SpecialSection()
        {
            AddRange(new Drawable[]
            {
                new GeneralSettings(),
                new InterfaceSettings(),
                new NotificationSettings(),
                new ThemeSettingsSubsection(),
                new MaintenanceSettings(),
            });
        }
    }
}
