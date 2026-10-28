// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
    /// <summary>
    /// A single row in the command palette's result list. Matched characters are highlighted.
    /// </summary>
    public partial class CommandListEntry : ClickableContainer
    {
        public const float HEIGHT = 46;
        public const float PADDING = 16;

        public CommandDefinition Command { get; }

        public Action? Clicked;

        private readonly IReadOnlyList<int> matchedIndices;

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

        public CommandListEntry(CommandDefinition command, IReadOnlyList<int> matchedIndices)
        {
            Command = command;
            this.matchedIndices = matchedIndices;

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
                    Colour = Colour4.White,
                    Alpha = 0,
                },
                new OsuSpriteText
                {
                    Text = "/",
                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(PADDING, 0),
                    Colour = colours.Blue1,
                    Shadow = false,
                },
                createNameFlow(),
                new OsuSpriteText
                {
                    Text = Command.Description ?? string.Empty,
                    Font = OsuFont.GetFont(size: 13),
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Position = new Vector2(-PADDING, 0),
                    Colour = colours.Gray5,
                    Shadow = false,
                },
            };

            updateVisual();
        }

        private Drawable createNameFlow()
        {
            var flow = new TextFlowContainer(s =>
            {
                s.Font = OsuFont.GetFont(size: 18);
                s.Shadow = false;
            })
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(PADDING + 22, 0),
                AutoSizeAxes = Axes.Both,
            };

            addHighlightedText(flow, Command.Name, matchedIndices, Colour4.White, colours.Pink1);

            string? syntax = Command.ArgumentUsage;

            if (!string.IsNullOrEmpty(syntax))
            {
                flow.AddText($"  {syntax}", s =>
                {
                    s.Colour = colours.Gray5;
                    s.Font = OsuFont.GetFont(size: 13);
                });
            }

            return flow;
        }

        private static void addHighlightedText(TextFlowContainer flow, string text, IReadOnlyList<int> indices, Colour4 normal, Colour4 highlight)
        {
            if (indices.Count == 0)
            {
                flow.AddText(text, s => s.Colour = normal);
                return;
            }

            int cursor = 0;

            for (int i = 0; i < indices.Count; i++)
            {
                int start = indices[i];

                if (start > cursor)
                    flow.AddText(text.Substring(cursor, start - cursor), s => s.Colour = normal);

                int end = start;

                while (i + 1 < indices.Count && indices[i + 1] == end + 1)
                {
                    end++;
                    i++;
                }

                flow.AddText(text.Substring(start, end - start + 1), s => s.Colour = highlight);
                cursor = end + 1;
            }

            if (cursor < text.Length)
                flow.AddText(text.Substring(cursor), s => s.Colour = normal);
        }

        private void updateVisual()
        {
            if (background == null)
                return;

            background.FadeTo(selected ? 0.08f : 0, 80, Easing.OutQuint);
        }
    }
}
