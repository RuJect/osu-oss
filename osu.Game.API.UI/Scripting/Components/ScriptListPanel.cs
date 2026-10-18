// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.API.Scripting;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.API.UI.Scripting.Components
{
    /// <summary>
    /// Lists all known scripts and allows selecting one for editing.
    /// </summary>
    public partial class ScriptListPanel : CompositeDrawable
    {
        private const float header_height = 44;

        private readonly ScriptRepository repository;
        private readonly ScriptRunner runner;
        private readonly FillFlowContainer list;
        private readonly Box background;
        private readonly OsuSpriteText title;
        private readonly Box divider;
        private readonly Dictionary<ScriptDefinition, ScriptListItem> items = new Dictionary<ScriptDefinition, ScriptListItem>();

        private ScriptDefinition? selected;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public event Action<ScriptDefinition>? ScriptSelected;

        public ScriptListPanel(ScriptRepository repository, ScriptRunner runner)
        {
            this.repository = repository;
            this.runner = runner;
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = header_height },
                    Child = new OsuScrollContainer(Direction.Vertical)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = list = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Top = 6, Bottom = 6 },
                        },
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Children = new Drawable[]
                    {
                        title = new OsuSpriteText
                        {
                            Text = "Scripts",
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                            Position = new Vector2(16, 0),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    },
                },
                divider = new Box
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
            background.Colour = colourProvider.Background4;
            title.Colour = colourProvider.Content1;
            divider.Colour = colourProvider.Background6;

            repository.DefinitionsChanged += rebuild;
            runner.RunningScriptsChanged += updateRunningStates;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuild();
        }

        public void SetSelected(ScriptDefinition? definition)
        {
            selected = definition;

            foreach (ScriptListItem item in items.Values)
                item.SetSelected(item.Definition == definition);
        }

        private void rebuild()
        {
            Schedule(() =>
            {
                list.Clear();
                items.Clear();

                foreach (ScriptDefinition definition in repository.Definitions)
                {
                    var item = new ScriptListItem(definition, colourProvider, onItemClicked)
                    {
                        IsRunning = runner.Running.Any(i => i.Definition == definition),
                    };

                    item.SetSelected(definition == selected);
                    items[definition] = item;
                    list.Add(item);
                }
            });
        }

        private void updateRunningStates()
        {
            Schedule(() =>
            {
                foreach (ScriptListItem item in items.Values)
                    item.IsRunning = runner.Running.Any(i => i.Definition == item.Definition);
            });
        }

        private void onItemClicked(ScriptDefinition definition) => ScriptSelected?.Invoke(definition);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            repository.DefinitionsChanged -= rebuild;
            runner.RunningScriptsChanged -= updateRunningStates;
        }

        private partial class ScriptListItem : OsuClickableContainer
        {
            public readonly ScriptDefinition Definition;

            public bool IsRunning
            {
                set
                {
                    running = value;
                    updateColour();
                }
            }

            public void SetSelected(bool value)
            {
                isSelected = value;
                updateColour();
            }

            private readonly OverlayColourProvider colourProvider;

            private bool isSelected;
            private bool running;
            private bool hovered;

            private readonly Box background;
            private readonly Box accent;
            private readonly Box statusDot;
            private readonly SpriteIcon icon;
            private readonly OsuSpriteText label;

            public ScriptListItem(ScriptDefinition definition, OverlayColourProvider colourProvider, Action<ScriptDefinition> onClick)
            {
                Definition = definition;
                this.colourProvider = colourProvider;

                RelativeSizeAxes = Axes.X;
                Height = 36;
                Action = () => onClick(definition);

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                        Alpha = 0,
                    },
                    accent = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = colourProvider.Highlight1,
                        Alpha = 0,
                    },
                    statusDot = new Box
                    {
                        Size = new Vector2(6),
                        Position = new Vector2(-16, 0),
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.Centre,
                        Colour = colourProvider.Light1,
                        Alpha = 0,
                    },
                    icon = new SpriteIcon
                    {
                        Size = new Vector2(13),
                        Position = new Vector2(18, 0),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        Icon = FontAwesome.Solid.FileCode,
                        Colour = colourProvider.Foreground1,
                    },
                    label = new OsuSpriteText
                    {
                        Text = definition.Name,
                        Font = OsuFont.GetFont(size: 14),
                        Position = new Vector2(34, 0),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Colour = colourProvider.Foreground1,
                        Shadow = false,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hovered = true;
                updateColour();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hovered = false;
                updateColour();
                base.OnHoverLost(e);
            }

            private void updateColour()
            {
                background.FadeTo(isSelected || hovered ? 1 : 0, 120);
                accent.FadeTo(isSelected ? 1 : 0, 120);
                statusDot.Alpha = running ? 1 : 0;

                icon.FadeColour(isSelected ? colourProvider.Highlight1 : colourProvider.Foreground1, 120);
                label.FadeColour(isSelected || hovered ? colourProvider.Content1 : colourProvider.Foreground1, 120);
            }
        }
    }
}
