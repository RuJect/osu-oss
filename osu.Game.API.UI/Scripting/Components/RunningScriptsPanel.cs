// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.API.Scripting;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.API.UI.Scripting.Components
{
    /// <summary>
    /// Lists running script instances and allows stopping them individually or en masse.
    /// </summary>
    public partial class RunningScriptsPanel : CompositeDrawable
    {
        private const float header_height = 44;

        private readonly ScriptRunner runner;
        private readonly FillFlowContainer list;

        private string? selectedSource;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public event Action<ScriptInstance>? StopRequested;

        /// <summary>
        /// Raised when the selected script changes. The value is the script name to filter output by,
        /// or <c>null</c> when the selection is cleared.
        /// </summary>
        public event Action<string?>? SelectedSourceChanged;

        public RunningScriptsPanel(ScriptRunner runner)
        {
            this.runner = runner;
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
                        new OsuSpriteText
                        {
                            Text = "Running",
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                            Position = new Vector2(16, 0),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new ShearedIconButton(FontAwesome.Solid.Stop, "Stop all")
                        {
                            Width = 92,
                            Height = 28,
                            TextSize = 12,
                            Position = new Vector2(-10, 0),
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Action = () => runner.StopAll(),
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
            var header = (Container)InternalChildren[2];
            var stopAll = (ShearedIconButton)header.Children[1];
            var divider = (Box)InternalChildren[3];

            background.Colour = colourProvider.Background4;
            ((OsuSpriteText)header.Children[0]).Colour = colourProvider.Content1;
            stopAll.DarkerColour = colours.Red1;
            stopAll.LighterColour = colours.Red2;
            divider.Colour = colourProvider.Background6;

            runner.RunningScriptsChanged += rebuild;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuild();
        }

        private void rebuild()
        {
            Schedule(() =>
            {
                list.Clear();

                foreach (ScriptInstance instance in runner.Running)
                {
                    ScriptInstance captured = instance;
                    bool isSelected = selectedSource == captured.Name;

                    var stopButton = new ShearedIconButton(FontAwesome.Solid.Stop, "Stop")
                    {
                        Width = 74,
                        Height = 24,
                        TextSize = 11,
                        DarkerColour = colours.Red1,
                        LighterColour = colours.Red2,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Position = new Vector2(-10, 0),
                        Action = () => StopRequested?.Invoke(captured),
                    };

                    list.Add(new OsuClickableContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 34,
                        Action = () =>
                        {
                            selectedSource = selectedSource == captured.Name ? null : captured.Name;
                            rebuild();
                            SelectedSourceChanged?.Invoke(selectedSource);
                        },
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Highlight1,
                                Alpha = isSelected ? 0.35f : 0f,
                            },
                            new Box
                            {
                                Size = new Vector2(6),
                                Position = new Vector2(14, 0),
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.Centre,
                                Colour = colourProvider.Highlight1,
                            },
                            new OsuSpriteText
                            {
                                Text = captured.Name,
                                Font = OsuFont.GetFont(size: 13),
                                Position = new Vector2(30, 0),
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Colour = colourProvider.Content1,
                                Shadow = false,
                            },
                            stopButton,
                        },
                    });
                }
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            runner.RunningScriptsChanged -= rebuild;
        }
    }
}
