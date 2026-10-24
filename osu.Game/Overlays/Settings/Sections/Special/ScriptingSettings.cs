// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.IO;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class ScriptingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Scripting";

        private OsuGame? game;
        private SettingsButtonV2 consoleButton = null!;

        [BackgroundDependencyLoader]
        private void load(SpecialConfigManager config, OsuGame? game, Storage storage)
        {
            this.game = game;

            AddRange(new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = "Enable JavaScript scripting",
                    TooltipText = "Allow scripts to run. Turning this off stops any running scripts.",
                    Current = config.GetBindable<bool>(SpecialSetting.ScriptingEnabled),
                },
                new SettingsCheckbox
                {
                    LabelText = "Allow scripts to use .NET (CLR)",
                    TooltipText = "Lets scripts call arbitrary .NET code. Only enable this if you trust every script.",
                    Current = config.GetBindable<bool>(SpecialSetting.AllowClrAccess),
                },
                consoleButton = new SettingsButtonV2
                {
                    Text = "Open scripting console",
                    TooltipText = "Open the script editor and output console. Also bound to Ctrl+F4 on desktop.",
                    Action = () => game?.ToggleScriptingConsole?.Invoke(),
                },
                new SettingsButtonV2
                {
                    Text = "Open scripts folder",
                    CanBeShown = { Value = RuntimeInfo.IsDesktop },
                    Action = () => SpecialStorage.GetRoot(storage).PresentFileExternally(SpecialStorage.SCRIPTS_FOLDER),
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (game == null)
                return;

            game.OnLoadComplete += _ => updateConsoleAvailability();
            updateConsoleAvailability();
        }

        private void updateConsoleAvailability() => consoleButton.Enabled.Value = game?.ToggleScriptingConsole != null;
    }
}
