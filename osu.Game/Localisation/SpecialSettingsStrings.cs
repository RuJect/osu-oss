// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SpecialSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SpecialSettings";

        /// <summary>
        /// "Nothing"
        /// </summary>
        public static LocalisableString AutoSkipNothing => new TranslatableString(getKey(@"auto_skip_nothing"), @"Nothing");

        /// <summary>
        /// "Break times only"
        /// </summary>
        public static LocalisableString AutoSkipBreaksOnly => new TranslatableString(getKey(@"auto_skip_breaks_only"), @"Break times only");

        /// <summary>
        /// "Intro/outro only"
        /// </summary>
        public static LocalisableString AutoSkipIntroOutroOnly => new TranslatableString(getKey(@"auto_skip_intro_outro_only"), @"Intro/outro only");

        /// <summary>
        /// "Break times and intro/outro"
        /// </summary>
        public static LocalisableString AutoSkipBreaksAndIntroOutro => new TranslatableString(getKey(@"auto_skip_breaks_and_intro_outro"), @"Break times and intro/outro");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
