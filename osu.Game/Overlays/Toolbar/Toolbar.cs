// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backdrops;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Theming;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class Toolbar : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        public const float HEIGHT = 40;
        public const float TOOLTIP_HEIGHT = 30;

        /// <summary>
        /// Whether the user hid this <see cref="Toolbar"/> with <see cref="GlobalAction.ToggleToolbar"/>.
        /// In this state, automatic toggles should not occur, respecting the user's preference to have no toolbar.
        /// </summary>
        private bool hiddenByUser;

        public Action OnHome;

        private ToolbarUserButton userButton;
        private ToolbarRulesetSelector rulesetSelector;
        private ToolbarClock clock;

        private Container layout;
        private Container leftButtons;
        private Container rulesetContainer;
        private Container rightButtons;
        private FillFlowContainer leftFlow;
        private FillFlowContainer rightFlow;
        private ToolbarBackground background;

        private bool rulesetBuilt;
        private bool rulesetVertical;
        private Box rulesetGradient;

        private ToolbarPosition placement = ToolbarPosition.Top;
        private bool inverted;
        private bool flowOrderInverted;

        private Bindable<ToolbarPosition> placementBindable;
        private Bindable<bool> invertBindable;

        private const double transition_time = 500;

        protected readonly IBindable<OverlayActivation> OverlayActivationMode = new Bindable<OverlayActivation>(OverlayActivation.All);

        // Toolbar and its components need keyboard input even when hidden.
        public override bool PropagateNonPositionalInputSubTree => OverlayActivationMode.Value != OverlayActivation.Disabled;

        /// <summary>
        /// The screen edge this toolbar is docked to.
        /// </summary>
        public ToolbarPosition Placement => placement;

        /// <summary>
        /// The distance the toolbar currently extends from its docked screen edge.
        /// Used by <see cref="OsuGame"/> to reserve space for the toolbar.
        /// </summary>
        public float CurrentInset
        {
            get
            {
                switch (placement)
                {
                    case ToolbarPosition.Bottom:
                        return Math.Max(0, DrawHeight - Position.Y);

                    case ToolbarPosition.Left:
                        return Math.Max(0, Position.X + DrawWidth);

                    case ToolbarPosition.Right:
                        return Math.Max(0, DrawWidth - Position.X);

                    default:
                        return Math.Max(0, Position.Y + DrawHeight);
                }
            }
        }

        public Toolbar()
        {
            RelativeSizeAxes = Axes.X;
            Size = new Vector2(1, HEIGHT);
            AlwaysPresent = true;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            updateGroupPositions();

            // this only needed to be set for the initial LoadComplete/Update, so layout completes and gets buttons in a state they can correctly handle keyboard input for hotkeys.
            AlwaysPresent = false;
        }

        [Resolved(canBeNull: true)]
        private SpecialConfigManager specialConfig { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame osuGame)
        {
            HoverInterceptor interceptor;

            Children = new Drawable[]
            {
                background = new ToolbarBackground(),
                layout = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                interceptor = new HoverInterceptor
                {
                    RelativeSizeAxes = Axes.Both
                }
            };

            var settingsButton = new ToolbarSettingsButton();
            var homeButton = new ToolbarHomeButton
            {
                Action = () => OnHome?.Invoke()
            };

            leftButtons = new Container
            {
                Name = "Left buttons",
                Depth = float.MinValue,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Colour = background.UsesGlass ? Color4.Transparent : background.BackgroundColour,
                        RelativeSizeAxes = Axes.Both,
                    },
                    leftFlow = new FillFlowContainer
                    {
                        Direction = FillDirection.Horizontal,
                        RelativeSizeAxes = Axes.Y,
                        AutoSizeAxes = Axes.X,
                        Children = new Drawable[]
                        {
                            settingsButton,
                            homeButton,
                        },
                    },
                }
            };

            rulesetContainer = new Container
            {
                Name = "Ruleset selector",
            };

            rightButtons = new Container
            {
                Name = "Right buttons",
                Children = new Drawable[]
                {
                    new Box
                    {
                        Colour = background.UsesGlass ? Color4.Transparent : background.BackgroundColour,
                        RelativeSizeAxes = Axes.Both,
                    },
                    rightFlow = new FillFlowContainer
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Direction = FillDirection.Horizontal,
                        RelativeSizeAxes = Axes.Y,
                        AutoSizeAxes = Axes.X,
                        Children = new Drawable[]
                        {
                            new ToolbarNewsButton(),
                            new ToolbarChangelogButton(),
                            new ToolbarWikiButton(),
                            new ToolbarRankingsButton(),
                            new ToolbarBeatmapListingButton(),
                            new ToolbarChatButton(),
                            new ToolbarSocialButton(),
                            new ToolbarMusicButton(),
                            //new ToolbarButton
                            //{
                            //    Icon = FontAwesome.Solid.search
                            //},
                            userButton = new ToolbarUserButton(),
                            clock = new ToolbarClock(),
                            new ToolbarNotificationButton(),
                        }
                    },
                }
            };

            layout.Children = new Drawable[]
            {
                leftButtons,
                rulesetContainer,
                rightButtons,
            };

            ((IBindable<bool>)background.ShowGradient).BindTo(interceptor.ReceivedHover);

            if (osuGame != null)
                OverlayActivationMode.BindTo(osuGame.OverlayActivationMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (specialConfig != null)
            {
                placementBindable = specialConfig.GetBindable<ToolbarPosition>(SpecialSetting.ToolbarPosition);
                placementBindable.BindValueChanged(p =>
                {
                    placement = p.NewValue;
                    ApplyLayout();
                }, true);

                invertBindable = specialConfig.GetBindable<bool>(SpecialSetting.InvertToolbar);
                invertBindable.BindValueChanged(i =>
                {
                    inverted = i.NewValue;
                    ApplyLayout();
                }, true);
            }
            else
            {
                ApplyLayout();
            }
        }

        /// <summary>
        /// Reconfigures the toolbar layout to match the current <see cref="placement"/> and inversion state.
        /// </summary>
        private void ApplyLayout()
        {
            bool vertical = isVertical;
            bool bottom = placement == ToolbarPosition.Bottom;
            bool right = placement == ToolbarPosition.Right;

            // The toolbar itself.
            if (vertical)
            {
                RelativeSizeAxes = Axes.Y;
                Size = new Vector2(HEIGHT, 1);
                Anchor = right ? Anchor.TopRight : Anchor.TopLeft;
                Origin = right ? Anchor.TopRight : Anchor.TopLeft;
            }
            else
            {
                RelativeSizeAxes = Axes.X;
                Size = new Vector2(1, HEIGHT);
                Anchor = bottom ? Anchor.BottomLeft : Anchor.TopLeft;
                Origin = bottom ? Anchor.BottomLeft : Anchor.TopLeft;
            }

            if (State.Value == Visibility.Visible)
            {
                Position = Vector2.Zero;
            }
            else
            {
                Position = placement switch
                {
                    ToolbarPosition.Bottom => new Vector2(0, HEIGHT),
                    ToolbarPosition.Left => new Vector2(-HEIGHT, 0),
                    ToolbarPosition.Right => new Vector2(HEIGHT, 0),
                    _ => new Vector2(0, -HEIGHT),
                };
            }

            // Button groups. Each group stays in its own drawable; inversion changes which edge each group is docked
            // to and reverses the button order within each group (the framework does not support relocating
            // already-loaded drawables between containers).
            leftButtons.AutoSizeAxes = Axes.None;
            leftButtons.RelativeSizeAxes = vertical ? Axes.X : Axes.Y;
            setRelativeSizeToFill(leftButtons, vertical);
            leftButtons.AutoSizeAxes = vertical ? Axes.Y : Axes.X;
            leftButtons.Anchor = leftButtons.Origin = Anchor.TopLeft;

            rightButtons.AutoSizeAxes = Axes.None;
            rightButtons.RelativeSizeAxes = vertical ? Axes.X : Axes.Y;
            setRelativeSizeToFill(rightButtons, vertical);
            rightButtons.AutoSizeAxes = vertical ? Axes.Y : Axes.X;
            rightButtons.Anchor = rightButtons.Origin = Anchor.TopLeft;

            leftFlow.AutoSizeAxes = Axes.None;
            leftFlow.Direction = vertical ? FillDirection.Vertical : FillDirection.Horizontal;
            leftFlow.Anchor = leftFlow.Origin = Anchor.TopLeft;
            leftFlow.RelativeSizeAxes = vertical ? Axes.X : Axes.Y;
            setRelativeSizeToFill(leftFlow, vertical);
            leftFlow.AutoSizeAxes = vertical ? Axes.Y : Axes.X;

            rightFlow.AutoSizeAxes = Axes.None;
            rightFlow.Direction = vertical ? FillDirection.Vertical : FillDirection.Horizontal;
            rightFlow.Anchor = rightFlow.Origin = Anchor.TopLeft;
            rightFlow.RelativeSizeAxes = vertical ? Axes.X : Axes.Y;
            setRelativeSizeToFill(rightFlow, vertical);
            rightFlow.AutoSizeAxes = vertical ? Axes.Y : Axes.X;

            applyFlowOrder();

            // Background gradient.
            background.SetPlacement(placement);

            // Ruleset selector (recreated as its scroll direction cannot change at runtime).
            applyRulesetContainer(vertical);
            updateRulesetGradient(vertical);
            rulesetSelector.SetPlacement(placement);
            rulesetSelector.SetInverted(inverted);

            // Buttons.
            foreach (var button in leftFlow.Children.OfType<ToolbarButton>())
                button.SetPlacement(placement);
            foreach (var button in rightFlow.Children.OfType<ToolbarButton>())
                button.SetPlacement(placement);

            clock.SetPlacement(placement);

            if (State.Value == Visibility.Visible)
                PopIn();
        }

        private void applyFlowOrder()
        {
            if (flowOrderInverted == inverted)
                return;

            flowOrderInverted = inverted;

            reverseChildren(leftFlow);
            reverseChildren(rightFlow);
        }

        private static void reverseChildren(Container<Drawable> container)
        {
            var children = container.Children.ToArray();

            container.Clear(false);

            for (int i = children.Length - 1; i >= 0; i--)
                container.Add(children[i]);
        }

        private static void setRelativeSizeToFill(Drawable drawable, bool vertical)
        {
            // Changing RelativeSizeAxes preserves the previous absolute size. Reset the relative axis to fill the parent.
            if (vertical)
                drawable.Width = 1;
            else
                drawable.Height = 1;
        }

        private void updateGroupPositions()
        {
            if (isVertical)
            {
                Container top = inverted ? rightButtons : leftButtons;
                Container bottom = inverted ? leftButtons : rightButtons;

                top.X = 0;
                top.Y = 0;
                bottom.X = 0;
                bottom.Y = top.DrawHeight + Math.Max(0, DrawHeight - top.DrawHeight - bottom.DrawHeight);

                rulesetContainer.X = 0;
                rulesetContainer.Y = top.DrawHeight;
                rulesetContainer.Width = DrawWidth;
                rulesetContainer.Height = Math.Max(0, bottom.Y - top.DrawHeight);
            }
            else
            {
                Container left = inverted ? rightButtons : leftButtons;
                Container right = inverted ? leftButtons : rightButtons;

                left.X = 0;
                left.Y = 0;
                right.X = left.DrawWidth + Math.Max(0, DrawWidth - left.DrawWidth - right.DrawWidth);
                right.Y = 0;

                rulesetContainer.X = left.DrawWidth;
                rulesetContainer.Y = 0;
                rulesetContainer.Width = Math.Max(0, right.X - left.DrawWidth);
                rulesetContainer.Height = DrawHeight;
            }
        }

        private void applyRulesetContainer(bool vertical)
        {
            // The scroll container's direction cannot be changed at runtime, so it (along with the selector) is rebuilt on orientation changes.
            if (rulesetBuilt && rulesetVertical == vertical)
                return;

            rulesetContainer.Clear(true);

            rulesetSelector = new ToolbarRulesetSelector();
            rulesetSelector.Current.BindTo(ruleset);

            var scroll = new OsuScrollContainer(vertical ? Direction.Vertical : Direction.Horizontal)
            {
                ScrollbarVisible = false,
                RelativeSizeAxes = Axes.Both,
                Masking = false,
                Children = new Drawable[] { rulesetSelector },
            };

            rulesetContainer.Add(scroll);
            rulesetContainer.Add(rulesetGradient = new Box
            {
                Width = vertical ? 1 : 50,
                Height = vertical ? 50 : 1,
                RelativeSizeAxes = vertical ? Axes.X : Axes.Y,
            });

            rulesetBuilt = true;
            rulesetVertical = vertical;

            updateRulesetGradient(vertical);
        }

        /// <summary>
        /// Positions the ruleset selector's overflow fade against the side where content continues.
        /// </summary>
        private void updateRulesetGradient(bool vertical)
        {
            if (rulesetGradient == null)
                return;

            Color4 fadeColour = background.BackgroundColour.Opacity(background.UsesGlass ? 0.6f : 1f);

            rulesetGradient.Colour = vertical
                ? (inverted
                    ? ColourInfo.GradientVertical(fadeColour, fadeColour.Opacity(0))
                    : ColourInfo.GradientVertical(fadeColour.Opacity(0), fadeColour))
                : (inverted
                    ? ColourInfo.GradientHorizontal(fadeColour, fadeColour.Opacity(0))
                    : ColourInfo.GradientHorizontal(fadeColour.Opacity(0), fadeColour));

            rulesetGradient.Anchor = vertical ? (inverted ? Anchor.TopLeft : Anchor.BottomLeft) : (inverted ? Anchor.TopLeft : Anchor.TopRight);
            rulesetGradient.Origin = rulesetGradient.Anchor;
        }

        private bool isVertical => placement is ToolbarPosition.Left or ToolbarPosition.Right;

        public partial class ToolbarBackground : Container
        {
            public Bindable<bool> ShowGradient { get; } = new BindableBool();

            public Color4 BackgroundColour { get; } = new OverlayColourProvider(OverlayColourScheme.Blue).Background6;

            public bool UsesGlass => ThemeManager.GlassActive;

            private readonly Box gradientBackground;

            public ToolbarBackground()
            {
                RelativeSizeAxes = Axes.Both;
                Children = new Drawable[]
                {
                    new GlassBackdrop
                    {
                        TintColour = BackgroundColour.Opacity(ThemeManager.Current.GetSurfaceAlpha("toolbar", 0.72f)),
                        BlurSigma = Math.Max(2.5f, ThemeManager.Current.GlassBlur / 4f),
                        FrameBufferScale = 0.5f,
                    },
                    gradientBackground = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.BottomLeft,
                        Alpha = 0,
                        Height = 80,
                        Colour = ColourInfo.GradientVertical(
                            OsuColour.Gray(0f).Opacity(0.7f), OsuColour.Gray(0).Opacity(0)),
                    },
                };
            }

            /// <summary>
            /// Positions the hover gradient against the correct edge for the given toolbar placement.
            /// </summary>
            public void SetPlacement(ToolbarPosition placement)
            {
                switch (placement)
                {
                    case ToolbarPosition.Bottom:
                        gradientBackground.RelativeSizeAxes = Axes.X;
                        gradientBackground.Size = new Vector2(1, 80);
                        gradientBackground.Anchor = Anchor.TopLeft;
                        gradientBackground.Origin = Anchor.TopLeft;
                        gradientBackground.Colour = ColourInfo.GradientVertical(OsuColour.Gray(0).Opacity(0), OsuColour.Gray(0f).Opacity(0.7f));
                        break;

                    case ToolbarPosition.Left:
                        gradientBackground.RelativeSizeAxes = Axes.Y;
                        gradientBackground.Size = new Vector2(80, 1);
                        gradientBackground.Anchor = Anchor.TopRight;
                        gradientBackground.Origin = Anchor.TopRight;
                        gradientBackground.Colour = ColourInfo.GradientHorizontal(OsuColour.Gray(0f).Opacity(0.7f), OsuColour.Gray(0).Opacity(0));
                        break;

                    case ToolbarPosition.Right:
                        gradientBackground.RelativeSizeAxes = Axes.Y;
                        gradientBackground.Size = new Vector2(80, 1);
                        gradientBackground.Anchor = Anchor.TopLeft;
                        gradientBackground.Origin = Anchor.TopLeft;
                        gradientBackground.Colour = ColourInfo.GradientHorizontal(OsuColour.Gray(0).Opacity(0), OsuColour.Gray(0f).Opacity(0.7f));
                        break;

                    default:
                        gradientBackground.RelativeSizeAxes = Axes.X;
                        gradientBackground.Size = new Vector2(1, 80);
                        gradientBackground.Anchor = Anchor.BottomLeft;
                        gradientBackground.Origin = Anchor.BottomLeft;
                        gradientBackground.Colour = ColourInfo.GradientVertical(OsuColour.Gray(0f).Opacity(0.7f), OsuColour.Gray(0).Opacity(0));
                        break;
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                ShowGradient.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                if (ShowGradient.Value)
                    gradientBackground.FadeIn(2500, Easing.OutQuint);
                else
                    gradientBackground.FadeOut(200, Easing.OutQuint);
            }
        }

        /// <summary>
        /// Whenever the mouse cursor is within the bounds of the toolbar, we want the background gradient to show, for toolbar button descriptions to be legible.
        /// Unfortunately we also need to ensure that the toolbar buttons handle hover, to prevent the possibility of multiple descriptions being shown
        /// due to hover events passing through multiple buttons.
        /// This drawable is a workaround, that when placed front-most in the toolbar, allows to see whether hover events have been propagated through it without handling them.
        /// </summary>
        private partial class HoverInterceptor : Drawable
        {
            public IBindable<bool> ReceivedHover => receivedHover;
            private readonly Bindable<bool> receivedHover = new BindableBool();

            protected override bool OnHover(HoverEvent e)
            {
                receivedHover.Value = true;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                receivedHover.Value = false;
                base.OnHoverLost(e);
            }
        }

        protected override void UpdateState(ValueChangedEvent<Visibility> state)
        {
            bool blockShow = hiddenByUser || OverlayActivationMode.Value == OverlayActivation.Disabled;

            if (state.NewValue == Visibility.Visible && blockShow)
            {
                State.Value = Visibility.Hidden;
                return;
            }

            base.UpdateState(state);
        }

        protected override void PopIn()
        {
            switch (placement)
            {
                case ToolbarPosition.Bottom:
                case ToolbarPosition.Top:
                    this.MoveToY(0, transition_time, Easing.OutQuint);
                    break;

                case ToolbarPosition.Left:
                case ToolbarPosition.Right:
                    this.MoveToX(0, transition_time, Easing.OutQuint);
                    break;
            }

            this.FadeIn(transition_time / 4, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            userButton.StateContainer?.Hide();

            switch (placement)
            {
                case ToolbarPosition.Bottom:
                    this.MoveToY(DrawSize.Y, transition_time, Easing.OutQuint);
                    break;

                case ToolbarPosition.Top:
                    this.MoveToY(-DrawSize.Y, transition_time, Easing.OutQuint);
                    break;

                case ToolbarPosition.Left:
                    this.MoveToX(-DrawSize.X, transition_time, Easing.OutQuint);
                    break;

                case ToolbarPosition.Right:
                    this.MoveToX(DrawSize.X, transition_time, Easing.OutQuint);
                    break;
            }

            this.FadeOut(transition_time, Easing.InQuint);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (OverlayActivationMode.Value == OverlayActivation.Disabled)
                return false;

            switch (e.Action)
            {
                case GlobalAction.ToggleToolbar:
                    hiddenByUser = State.Value == Visibility.Visible; // set before toggling to allow the operation to always succeed.
                    ToggleVisibility();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
