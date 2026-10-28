// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public abstract partial class ToolbarButton : OsuClickableContainer, IKeyBindingHandler<GlobalAction>
    {
        public const float PADDING = 3;

        protected GlobalAction? Hotkey { get; set; }

        /// <summary>
        /// The current placement of the containing toolbar. Drives the orientation of this button.
        /// </summary>
        protected ToolbarPosition Placement { get; private set; } = ToolbarPosition.Top;

        /// <summary>
        /// Whether the containing toolbar is rendered as a vertical bar (i.e. docked to the left or right edge).
        /// </summary>
        public bool Vertical { get; private set; }

        /// <summary>
        /// The size of <see cref="ButtonContent"/> along the toolbar's main axis. Subclasses may enlarge this for buttons
        /// which display extra content (e.g. the home button).
        /// </summary>
        protected float ContentWidth { get; set; } = Toolbar.HEIGHT;

        /// <summary>
        /// Updates this button's orientation to match the placement of the containing toolbar.
        /// </summary>
        public void SetPlacement(ToolbarPosition placement)
        {
            Placement = placement;
            Vertical = placement is ToolbarPosition.Left or ToolbarPosition.Right;
            UpdateLayout();
        }

        public void SetIcon(Drawable icon)
        {
            IconContainer.Icon = icon;
            IconContainer.Show();
        }

        public void SetIcon(IconUsage icon) => SetIcon(new SpriteIcon { Icon = icon });

        public LocalisableString TooltipMain
        {
            get => tooltip1.Text;
            set => tooltip1.Text = value;
        }

        public LocalisableString TooltipSub
        {
            get => tooltip2.Text;
            set => tooltip2.Text = value;
        }

        protected virtual Anchor TooltipAnchor => Anchor.TopLeft;

        protected readonly Container ButtonContent;
        protected ConstrainedIconContainer IconContainer;
        protected Box HoverBackground;
        private readonly Box flashBackground;
        private readonly FillFlowContainer tooltipContainer;
        private readonly SpriteText tooltip1;
        private readonly SpriteText tooltip2;
        protected FillFlowContainer Flow;

        protected readonly Container BackgroundContent;

        private readonly FillFlowContainer subTooltipFlow;

        protected ToolbarButton()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;

            Children = new Drawable[]
            {
                ButtonContent = new Container
                {
                    Width = Toolbar.HEIGHT,
                    RelativeSizeAxes = Axes.Y,
                    Padding = new MarginPadding(PADDING),
                    Children = new Drawable[]
                    {
                        BackgroundContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 6,
                            CornerExponent = 3f,
                            Children = new Drawable[]
                            {
                                HoverBackground = new Box
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
                        Flow = new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Padding = new MarginPadding { Left = Toolbar.HEIGHT / 2, Right = Toolbar.HEIGHT / 2 },
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Children = new Drawable[]
                            {
                                IconContainer = new ConstrainedIconContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(20),
                                    Alpha = 0,
                                },
                            },
                        },
                    },
                },
                tooltipContainer = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    RelativeSizeAxes = Axes.Both, // stops us being considered in parent's autosize
                    Anchor = TooltipAnchor.HasFlag(Anchor.x0) ? Anchor.BottomLeft : Anchor.BottomRight,
                    Origin = TooltipAnchor,
                    Position = new Vector2(TooltipAnchor.HasFlag(Anchor.x0) ? 5 : -5, 5),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        tooltip1 = new OsuSpriteText
                        {
                            Anchor = TooltipAnchor,
                            Origin = TooltipAnchor,
                            Shadow = true,
                            Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                        },
                        subTooltipFlow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = TooltipAnchor,
                            Origin = TooltipAnchor,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                tooltip2 = new OsuSpriteText { Shadow = true },
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Applies an orientation to this button based on the containing toolbar's <see cref="Placement"/>.
        /// </summary>
        protected virtual void UpdateLayout()
        {
            if (Vertical)
            {
                AutoSizeAxes = Axes.None;
                RelativeSizeAxes = Axes.None;
                RelativeSizeAxes = Axes.X;
                Size = new Vector2(1, ContentWidth);

                ButtonContent.AutoSizeAxes = Axes.None;
                ButtonContent.RelativeSizeAxes = Axes.None;
                ButtonContent.Size = new Vector2(Toolbar.HEIGHT, ContentWidth);

                Flow.AutoSizeAxes = Axes.None;
                Flow.Direction = FillDirection.Vertical;
                Flow.Anchor = Anchor.CentreLeft;
                Flow.Origin = Anchor.CentreLeft;
                Flow.Padding = new MarginPadding { Top = Toolbar.HEIGHT / 2, Bottom = Toolbar.HEIGHT / 2 };
                Flow.RelativeSizeAxes = Axes.X;
                Flow.Width = 1;
                Flow.AutoSizeAxes = Axes.Y;

                IconContainer.Anchor = Anchor.TopCentre;
                IconContainer.Origin = Anchor.TopCentre;
            }
            else
            {
                AutoSizeAxes = Axes.None;
                RelativeSizeAxes = Axes.None;
                RelativeSizeAxes = Axes.Y;
                Size = new Vector2(ContentWidth, 1);
                AutoSizeAxes = Axes.X;

                ButtonContent.AutoSizeAxes = Axes.None;
                ButtonContent.RelativeSizeAxes = Axes.None;
                ButtonContent.RelativeSizeAxes = Axes.Y;
                ButtonContent.Size = new Vector2(ContentWidth, 1);

                Flow.AutoSizeAxes = Axes.None;
                Flow.Direction = FillDirection.Horizontal;
                Flow.Anchor = Anchor.TopCentre;
                Flow.Origin = Anchor.TopCentre;
                Flow.Padding = new MarginPadding { Left = Toolbar.HEIGHT / 2, Right = Toolbar.HEIGHT / 2 };
                Flow.RelativeSizeAxes = Axes.Y;
                Flow.Height = 1;
                Flow.AutoSizeAxes = Axes.X;

                IconContainer.Anchor = Anchor.CentreLeft;
                IconContainer.Origin = Anchor.CentreLeft;
            }

            applyTooltipLayout();
        }

        private void applyTooltipLayout()
        {
            bool xLeft = TooltipAnchor.HasFlag(Anchor.x0);
            Anchor childAnchor;

            switch (Placement)
            {
                case ToolbarPosition.Bottom:
                    childAnchor = xLeft ? Anchor.BottomLeft : Anchor.BottomRight;
                    tooltipContainer.Anchor = xLeft ? Anchor.TopLeft : Anchor.TopRight;
                    tooltipContainer.Origin = xLeft ? Anchor.BottomLeft : Anchor.BottomRight;
                    tooltipContainer.Position = new Vector2(xLeft ? 5 : -5, -5);
                    break;

                case ToolbarPosition.Left:
                    childAnchor = Anchor.CentreLeft;
                    tooltipContainer.Anchor = Anchor.CentreRight;
                    tooltipContainer.Origin = Anchor.CentreLeft;
                    tooltipContainer.Position = new Vector2(5, 0);
                    break;

                case ToolbarPosition.Right:
                    childAnchor = Anchor.CentreRight;
                    tooltipContainer.Anchor = Anchor.CentreLeft;
                    tooltipContainer.Origin = Anchor.CentreRight;
                    tooltipContainer.Position = new Vector2(-5, 0);
                    break;

                default:
                    childAnchor = TooltipAnchor;
                    tooltipContainer.Anchor = xLeft ? Anchor.BottomLeft : Anchor.BottomRight;
                    tooltipContainer.Origin = TooltipAnchor;
                    tooltipContainer.Position = new Vector2(xLeft ? 5 : -5, 5);
                    break;
            }

            tooltip1.Anchor = childAnchor;
            tooltip1.Origin = childAnchor;
            subTooltipFlow.Anchor = childAnchor;
            subTooltipFlow.Origin = childAnchor;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Hotkey != null)
            {
                subTooltipFlow.Add(new HotkeyDisplay
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Hotkey = new Hotkey(Hotkey.Value),
                    Margin = new MarginPadding { Left = 3 },
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The toolbar may have been given a placement before this button was added to the tree.
            UpdateLayout();
        }

        protected override bool OnMouseDown(MouseDownEvent e) => false;

        protected override bool OnClick(ClickEvent e)
        {
            flashBackground.FadeIn(50).Then().FadeOutFromOne(800, Easing.OutQuint);
            tooltipContainer.FadeOut(100);
            return base.OnClick(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            HoverBackground.FadeIn(300, Easing.OutQuint);
            tooltipContainer.FadeIn(200, Easing.OutQuint);

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            HoverBackground.FadeOut(200, Easing.Out);
            tooltipContainer.FadeOut(100, Easing.Out);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == Hotkey && !e.Repeat)
            {
                TriggerClick();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }

    public partial class OpaqueBackground : Container
    {
        public OpaqueBackground()
        {
            RelativeSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(30)
                },
                new Triangles
                {
                    RelativeSizeAxes = Axes.Both,
                    ColourLight = OsuColour.Gray(40),
                    ColourDark = OsuColour.Gray(20),
                },
            };
        }
    }
}
