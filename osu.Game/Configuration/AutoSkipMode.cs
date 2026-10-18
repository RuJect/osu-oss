// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Controls which parts of a beatmap's "dead time" are automatically skipped during gameplay.
    /// </summary>
    public enum AutoSkipMode
    {
        /// <summary>
        /// Nothing is skipped automatically.
        /// </summary>
        [LocalisableDescription(typeof(SpecialSettingsStrings), nameof(SpecialSettingsStrings.AutoSkipNothing))]
        None,

        /// <summary>
        /// Only break times are skipped automatically.
        /// </summary>
        [LocalisableDescription(typeof(SpecialSettingsStrings), nameof(SpecialSettingsStrings.AutoSkipBreaksOnly))]
        Breaks,

        /// <summary>
        /// Only the intro and outro are skipped automatically.
        /// </summary>
        [LocalisableDescription(typeof(SpecialSettingsStrings), nameof(SpecialSettingsStrings.AutoSkipIntroOutroOnly))]
        IntroOutro,

        /// <summary>
        /// Both break times and the intro/outro are skipped automatically.
        /// </summary>
        [LocalisableDescription(typeof(SpecialSettingsStrings), nameof(SpecialSettingsStrings.AutoSkipBreaksAndIntroOutro))]
        BreaksAndIntroOutro,
    }
}
