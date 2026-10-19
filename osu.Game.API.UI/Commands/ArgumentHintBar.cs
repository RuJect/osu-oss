// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.API.Commands;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.API.UI.Commands
{
    /// <summary>
    /// Displays the selected command's usage, description and validation feedback.
    /// </summary>
    public partial class ArgumentHintBar : CompositeDrawable
    {
        private FillFlowContainer flow = null!;
        private OsuSpriteText usage = null!;
        private OsuSpriteText detail = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        /// <summary>The completion data computed for the most recent <see cref="Show"/> call.</summary>
        public CommandCompletion? Completion { get; private set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
            Padding = new MarginPadding { Horizontal = 16, Vertical = 10 };

            InternalChild = flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 3),
                Children = new Drawable[]
                {
                    usage = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                        Colour = colours.Blue1,
                        Shadow = false,
                    },
                    detail = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 13),
                        Colour = colours.Gray5,
                        Shadow = false,
                    },
                },
            };
        }

        /// <summary>
        /// Updates the bar for <paramref name="command"/> given the currently typed <paramref name="argument"/>.
        /// </summary>
        /// <param name="command">The command to describe, or <c>null</c> when no command is selected.</param>
        /// <param name="argument">The currently typed argument string.</param>
        /// <param name="status">An override message (e.g. a validation error); shown in place of the description.</param>
        public void Show(CommandDefinition? command, string argument = "", string? status = null)
        {
            Completion = command == null ? null : CommandCompletionService.GetCompletion(command, argument);

            if (command == null)
            {
                usage.Text = "Type a command";
                detail.Text = status ?? "Start typing to search the registered commands.";
                detail.Colour = status != null ? colours.Red1 : colours.Gray5;
                return;
            }

            usage.Text = command.Usage;

            detail.Text = status ?? Completion?.Argument?.Description ?? command.Description ?? string.Empty;
            detail.Colour = status != null ? colours.Red1 : colours.Gray5;
        }
    }
}
