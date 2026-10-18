// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Game.IO;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Config manager for osu!oss-specific settings, persisted to <c>osu-oss/config.ini</c>.
    /// The manager should be constructed with a storage scoped to <see cref="SpecialStorage.GetRoot"/>.
    /// </summary>
    public class SpecialConfigManager : IniConfigManager<SpecialSetting>
    {
        protected override string Filename => SpecialStorage.CONFIG_FILENAME;

        public SpecialConfigManager(Storage storage)
            : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(SpecialSetting.ShowDevBuildBanner, false);
        }
    }


    public enum SpecialSetting
    {
        /// <summary>Whether the "DEVELOPER BUILD" banner should be displayed on the intro/main menu.</summary>
        ShowDevBuildBanner,
    }
}
