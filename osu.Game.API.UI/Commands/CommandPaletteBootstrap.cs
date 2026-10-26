// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.API.Commands;

namespace osu.Game.API.UI.Commands
{
    /// <summary>
    /// Composition root for the command subsystem's user interface. Constructed from
    /// <c>osu.Desktop/Program.cs</c> around <c>host.Run(game)</c>; wires the
    /// <see cref="CommandPaletteOverlay"/> into the game once it has loaded.
    /// </summary>
    public class CommandPaletteBootstrap : IDisposable
    {
        private readonly OsuGame game;
        private readonly CommandBootstrap commands;

        private CommandPaletteOverlay? overlay;
        private IDisposable? overlayRegistration;

        public CommandRegistry Registry => commands.Registry;

        public CommandPaletteBootstrap(OsuGame game, CommandBootstrap commands)
        {
            this.game = game;
            this.commands = commands;

            if (game.IsLoaded)
                createOverlay();
            else
                game.OnLoadComplete += onGameLoadComplete;
        }

        private void onGameLoadComplete(Drawable _)
        {
            game.OnLoadComplete -= onGameLoadComplete;
            createOverlay();
        }

        private void createOverlay()
        {
            if (overlay != null)
                return;

            overlay = new CommandPaletteOverlay(commands.Registry);
            overlayRegistration = game.RegisterExternalOverlay(overlay);
            game.ToggleCommandPalette = toggle;
        }

        private void toggle() => overlay?.ToggleVisibility();

        public void Dispose()
        {
            game.OnLoadComplete -= onGameLoadComplete;
            game.ToggleCommandPalette = null;

            overlayRegistration?.Dispose();
            overlayRegistration = null;
            overlay = null;

            commands.Dispose();
        }
    }
}
