// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarClock : OsuClickableContainer
    {
        private Bindable<ToolbarClockDisplayMode> clockDisplayMode;
        private Bindable<bool> prefer24HourTime;

        private Box hoverBackground;
        private Box flashBackground;

        private DigitalClockDisplay digital;
        private AnalogClockDisplay analog;

        private Container clockContainer;
        private FillFlowContainer clockFlow;
        private bool vertical;

        private ToolbarPosition placement = ToolbarPosition.Top;

        public ToolbarClock()
        {
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        /// <summary>
        /// Updates the orientation of this clock to match the containing toolbar's placement.
        /// When rendered in a vertical toolbar, only the analog clock is displayed.
        /// </summary>
        public void SetPlacement(ToolbarPosition placement)
        {
            this.placement = placement;
            updateOrientation();
        }

        private void updateOrientation()
        {
            vertical = placement is ToolbarPosition.Left or ToolbarPosition.Right;

            AutoSizeAxes = Axes.None;
            RelativeSizeAxes = vertical ? Axes.X : Axes.Y;
            Size = vertical ? new Vector2(1, Toolbar.HEIGHT) : new Vector2(0, 1);
            AutoSizeAxes = vertical ? Axes.None : Axes.X;

            // OsuClickableContainer wraps its children in an internal content container, which has to be
            // oriented as well. Otherwise its auto-sized width would collapse to zero in the vertical toolbar.
            Content.AutoSizeAxes = Axes.None;
            Content.RelativeSizeAxes = vertical ? Axes.Both : Axes.Y;
            Content.Size = vertical ? Vector2.One : new Vector2(0, 1);
            Content.AutoSizeAxes = vertical ? Axes.None : Axes.X;

            if (clockContainer != null)
            {
                clockContainer.AutoSizeAxes = Axes.None;
                clockContainer.RelativeSizeAxes = vertical ? Axes.Both : Axes.Y;
                if (vertical)
                    clockContainer.Size = Vector2.One;
                else
                    clockContainer.Height = 1;
                clockContainer.AutoSizeAxes = vertical ? Axes.None : Axes.X;
            }

            if (clockFlow != null)
            {
                clockFlow.AutoSizeAxes = Axes.None;
                clockFlow.RelativeSizeAxes = vertical ? Axes.Both : Axes.Y;
                if (vertical)
                    clockFlow.Size = Vector2.One;
                else
                    clockFlow.Height = 1;
                clockFlow.AutoSizeAxes = vertical ? Axes.None : Axes.X;

                clockFlow.Direction = vertical ? FillDirection.Vertical : FillDirection.Horizontal;
                clockFlow.Anchor = vertical ? Anchor.Centre : Anchor.CentreLeft;
                clockFlow.Origin = vertical ? Anchor.Centre : Anchor.CentreLeft;
                clockFlow.Padding = vertical ? new MarginPadding() : new MarginPadding(10);
            }

            if (analog != null)
            {
                analog.Anchor = analog.Origin = vertical ? Anchor.Centre : Anchor.CentreLeft;
                digital.Anchor = digital.Origin = vertical ? Anchor.Centre : Anchor.CentreLeft;
            }

            update((ToolbarClockDisplayMode?)null);
        }

        private void update(ToolbarClockDisplayMode? displayMode)
        {
            ToolbarClockDisplayMode mode = displayMode ?? clockDisplayMode?.Value ?? ToolbarClockDisplayMode.Full;

            bool showAnalog = vertical || mode == ToolbarClockDisplayMode.Analog || mode == ToolbarClockDisplayMode.Full;
            bool showDigital = !vertical && mode != ToolbarClockDisplayMode.Analog;
            bool showRuntime = !vertical && (mode == ToolbarClockDisplayMode.DigitalWithRuntime || mode == ToolbarClockDisplayMode.Full);

            digital?.FadeTo(showDigital ? 1 : 0);

            if (digital != null)
                digital.ShowRuntime = showRuntime;

            analog?.FadeTo(showAnalog ? 1 : 0);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            clockDisplayMode = config.GetBindable<ToolbarClockDisplayMode>(OsuSetting.ToolbarClockDisplayMode);
            prefer24HourTime = config.GetBindable<bool>(OsuSetting.Prefer24HourTime);

            Children = new Drawable[]
            {
                clockContainer = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Padding = new MarginPadding(ToolbarButton.PADDING),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 6,
                            CornerExponent = 3f,
                            Children = new Drawable[]
                            {
                                hoverBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = OsuColour.Gray(80).Opacity(180),
                                    Blending = BlendingParameters.Additive,
                                    Alpha = 0,
                                },
                                flashBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    Colour = Color4.White.Opacity(100),
                                    Blending = BlendingParameters.Additive,
                                },
                            }
                        },
                        clockFlow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(5),
                            Padding = new MarginPadding(10),
                            Children = new Drawable[]
                            {
                                analog = new AnalogClockDisplay
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                digital = new DigitalClockDisplay
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            clockDisplayMode.BindValueChanged(displayMode => update(displayMode.NewValue), true);

            prefer24HourTime.BindValueChanged(prefer24H => digital.Use24HourDisplay = prefer24H.NewValue, true);

            updateOrientation();
        }

        protected override bool OnClick(ClickEvent e)
        {
            flashBackground.FadeOutFromOne(800, Easing.OutQuint);

            cycleDisplayMode();

            return base.OnClick(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBackground.FadeIn(200);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBackground.FadeOut(200);

            base.OnHoverLost(e);
        }

        protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverClickSounds(sampleSet);

        private void cycleDisplayMode()
        {
            switch (clockDisplayMode.Value)
            {
                case ToolbarClockDisplayMode.Analog:
                    clockDisplayMode.Value = ToolbarClockDisplayMode.Full;
                    break;

                case ToolbarClockDisplayMode.Digital:
                    clockDisplayMode.Value = ToolbarClockDisplayMode.Analog;
                    break;

                case ToolbarClockDisplayMode.DigitalWithRuntime:
                    clockDisplayMode.Value = ToolbarClockDisplayMode.Digital;
                    break;

                case ToolbarClockDisplayMode.Full:
                    clockDisplayMode.Value = ToolbarClockDisplayMode.DigitalWithRuntime;
                    break;
            }
        }
    }
}
