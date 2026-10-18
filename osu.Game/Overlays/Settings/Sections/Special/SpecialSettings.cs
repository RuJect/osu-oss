// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.Configuration;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class SpecialSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"osu!oss";

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config, Storage storage, IDialogOverlay? dialogOverlay)
        {
            AddRange(new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = "Show developer build banner",
                    Current = config.GetBindable<bool>(SpecialSetting.ShowDevBuildBanner),
                },
                new DangerousSettingsButtonV2
                {
                    Text = "Reset osu!oss data",
                    Action = () => dialogOverlay?.Push(new ConfirmDialog(
                        "This will delete all osu!oss data, including settings and downloaded authentication files. This cannot be undone. Are you sure?",
                        () => resetData(storage, config))),
                },
            });
        }

        private static void resetData(Storage storage, SpecialConfigManager config)
        {
            try
            {
                string path = SpecialStorage.GetRootPath(storage);

                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                // restore in-memory defaults; the config file is recreated on next persist.
                config.GetBindable<bool>(SpecialSetting.ShowDevBuildBanner).Value = false;

                Logger.Log("osu!oss data reset.", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to reset osu!oss data");
            }
        }
    }
}
