// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.API.Commands;
using osu.Game.Graphics.Containers;

namespace osu.Game.API.UI.Commands
{
    /// <summary>A scrollable popup listing the argument suggestions for the current command input.</summary>
    public partial class CommandSuggestionList : Container
    {
        public const float ENTRY_HEIGHT = CommandSuggestionEntry.HEIGHT;

        private const float vertical_padding = 6;
        private const int max_visible_entries = 5;

        private readonly List<CommandSuggestionEntry> entries = new List<CommandSuggestionEntry>();

        private OsuScrollContainer scroll = null!;
        private FillFlowContainer flow = null!;
        private IReadOnlyList<CommandSuggestion> suggestions = Array.Empty<CommandSuggestion>();
        private bool popupVisible;

        /// <summary>Invoked with the index of the suggestion which was clicked.</summary>
        public Action<int>? Clicked;

        public bool PopupVisible
        {
            get => popupVisible;
            set
            {
                if (popupVisible == value)
                    return;

                popupVisible = value;
                Invalidate(Invalidation.DrawSize);
            }
        }

        public override bool IsPresent => popupVisible && base.IsPresent;

        public IReadOnlyList<CommandSuggestion> Suggestions => suggestions;

        public int SelectedIndex { get; private set; } = -1;

        [BackgroundDependencyLoader]
        private void load()
        {
            Masking = true;
            CornerRadius = 8;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("1B1B24"),
                },
                scroll = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarOverlapsContent = true,
                    Child = flow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Vertical = vertical_padding },
                    },
                },
            };
        }

        public void SetSuggestions(IReadOnlyList<CommandSuggestion> newSuggestions)
        {
            suggestions = newSuggestions;
            flow.Clear();
            entries.Clear();
            SelectedIndex = -1;

            for (int i = 0; i < newSuggestions.Count; i++)
            {
                int index = i;

                var entry = new CommandSuggestionEntry(newSuggestions[i])
                {
                    Clicked = () => Clicked?.Invoke(index),
                };

                entries.Add(entry);
                flow.Add(entry);
            }

            Height = Math.Min(newSuggestions.Count, max_visible_entries) * ENTRY_HEIGHT + vertical_padding * 2;
        }

        public void SetSelection(int index)
        {
            SelectedIndex = index;

            for (int i = 0; i < entries.Count; i++)
                entries[i].Selected = i == index;

            if (index >= 0 && index < entries.Count)
                ScheduleAfterChildren(() => scroll.ScrollIntoView(entries[index], false));
        }
    }
}
