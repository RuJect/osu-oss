// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.API.Services;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.API.UI.Scripting.Components
{
    /// <summary>
    /// Displays the output history of every script (or a single selected script), newest at the bottom.
    /// </summary>
    public partial class ScriptOutputConsole : CompositeDrawable
    {
        private const int max_display = 1000;
        private const float header_height = 44;

        private readonly OutputLog output;
        private readonly FillFlowContainer flow;
        private readonly OsuScrollContainer scroll;
        private readonly FontUsage font = OsuFont.GetFont(size: 15);

        private OsuSpriteText titleText = null!;
        private ShearedIconButton allButton = null!;
        private ShearedIconButton clearButton = null!;

        /// <summary>When set, only output originating from this script is shown.</summary>
        private string? filter;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public ScriptOutputConsole(OutputLog output)
        {
            this.output = output;
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.85f,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = header_height },
                    Child = scroll = new OsuScrollContainer(Direction.Vertical)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = flow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Top = 6, Bottom = 8 },
                        },
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Children = new Drawable[]
                    {
                        titleText = new OsuSpriteText
                        {
                            Text = "Output",
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                            Position = new Vector2(16, 0),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6),
                            AutoSizeAxes = Axes.Both,
                            Position = new Vector2(-10, 0),
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                allButton = new ShearedIconButton(FontAwesome.Solid.Sync, "All")
                                {
                                    Width = 74,
                                    Height = 28,
                                    TextSize = 12,
                                    Action = () => SetFilter(null),
                                },
                                clearButton = new ShearedIconButton(FontAwesome.Solid.Broom, "Clear")
                                {
                                    Width = 84,
                                    Height = 28,
                                    TextSize = 12,
                                    Action = clear,
                                },
                            },
                        },
                    },
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Y = header_height,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var background = (Box)InternalChildren[0];
            var divider = (Box)InternalChildren[3];

            background.Colour = colourProvider.Background4;
            titleText.Colour = colourProvider.Content1;
            allButton.DarkerColour = colours.Blue1;
            allButton.LighterColour = colours.Blue2;
            clearButton.DarkerColour = colours.Yellow;
            clearButton.LighterColour = colours.Orange1;
            divider.Colour = colourProvider.Background6;

            output.OutputAppended += append;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuildFromHistory();
        }

        /// <summary>
        /// Restricts the displayed output to a single script. Pass <c>null</c> to show every script.
        /// </summary>
        public void SetFilter(string? source)
        {
            filter = source;
            updateTitle();

            if (!IsLoaded)
                return;

            Schedule(rebuildFromHistory);
        }

        private void updateTitle()
            => titleText.Text = filter == null ? "Output" : $"Output · {filter}";

        private void rebuildFromHistory()
        {
            if (!IsLoaded || IsDisposed)
                return;

            flow.Clear();

            IReadOnlyList<OutputEntry> history = output.History;
            int start = Math.Max(0, history.Count - max_display);

            for (int i = start; i < history.Count; i++)
            {
                if (matches(history[i]))
                    addEntry(history[i]);
            }

            scroll.ScrollToEnd(false);
        }

        private bool matches(OutputEntry entry) => filter == null || entry.Source == filter;

        private void append(OutputEntry entry)
        {
            if (!IsLoaded || IsDisposed)
                return;

            if (!matches(entry))
                return;

            Schedule(() =>
            {
                addEntry(entry);
                scroll.ScrollToEnd(false);
            });
        }

        private void addEntry(OutputEntry entry)
        {
            var line = new OsuTextFlowContainer(t =>
            {
                t.Font = font;
                t.Shadow = false;
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 12, Vertical = 1 },
            };

            line.AddText($"[{entry.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] ", t => t.Colour = colourProvider.Foreground1);
            line.AddText($"[{entry.Source}] ", t => t.Colour = colourProvider.Highlight1);
            line.AddText(entry.Text, t => t.Colour = ScriptingColours.ForLevel(entry.Level));

            flow.Add(line);

            while (flow.Count > max_display && flow.Count > 0)
            {
                Drawable first = flow[0];
                flow.Remove(first, true);
            }
        }

        private void clear()
        {
            flow.Clear();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            output.OutputAppended -= append;
        }
    }
}
