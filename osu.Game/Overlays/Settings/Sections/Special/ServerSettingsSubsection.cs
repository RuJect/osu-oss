// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.Server;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class ServerSettingsSubsection : SettingsSubsection
    {
        protected override LocalisableString Header => OnlineSettingsStrings.ServerHeader;

        private SettingsDropdown<IServer> dropdown;
        private readonly Bindable<IServer> selectedServer = new Bindable<IServer>();

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager config, IServerRegistry registry, IServerManager serverManager)
        {
            // The server subsystem is only wired on builds that reference osu.Game.API
            // (e.g. the desktop game). In other contexts (tests, tournament client) the
            // dependencies are not registered, so we simply don't render the dropdown.
            if (registry == null || serverManager == null)
                return;

            var availableServers = registry.AvailableServers
                .OrderBy(s => s.Name)
                .ToList();

            selectedServer.Value = serverManager.ActiveServer.Value;

            dropdown = new SettingsDropdown<IServer>
            {
                LabelText = OnlineSettingsStrings.ActiveServerLabel,
                TooltipText = OnlineSettingsStrings.ActiveServerTooltip,
                Items = availableServers,
                Current = selectedServer,
            };

            selectedServer.ValueChanged += e => serverManager.SelectServer(e.NewValue.Id);
            serverManager.ActiveServerChanged += server =>
            {
                if (!ReferenceEquals(selectedServer.Value, server))
                    selectedServer.Value = server;
            };

            Children = new Drawable[]
            {
                dropdown,
            };
        }
    }
}