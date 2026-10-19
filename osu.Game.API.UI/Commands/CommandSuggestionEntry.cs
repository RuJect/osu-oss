// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.API.Commands;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.API.UI.Commands
{
    /// <summary>A single row in the argument suggestion popup.</summary>
    public partial class CommandSuggestionEntry : ClickableContainer
    {
        public const float HEIGHT = 34;
        public const float PADDING = 12;

        public CommandSuggestion Suggestion { get; }

        public Action? Clicked;

        private Box background = null!;
        private bool selected;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;

                selected = value;
                updateVisual();
            }
        }

        public CommandSuggestionEntry(CommandSuggestion suggestion)
        {
            Suggestion = suggestion;

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            Action = () => Clicked?.Invoke();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Pink1,
                    Alpha = 0,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Padding = new MarginPadding { Horizontal = PADDING },
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = Suggestion.Display,
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Colour4.White,
                            Shadow = false,
                        },
                        new OsuSpriteText
                        {
                            Text = Suggestion.Description ?? string.Empty,
                            Font = OsuFont.GetFont(size: 12),
                            Colour = colours.Gray5,
                            Shadow = false,
                        },
                    },
                },
            };

            updateVisual();
        }

        private void updateVisual()
        {
            if (background == null)
                return;

            background.FadeTo(selected ? 0.18f : 0, 80, Easing.OutQuint);
        }
    }
}
