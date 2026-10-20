// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarRulesetSelector : RulesetSelector
    {
        protected Drawable ModeButtonLine { get; private set; }

        private Circle modeButtonCircle;

        private ToolbarPosition placement = ToolbarPosition.Top;

        private bool inverted;
        private bool tabOrderInverted;

        private readonly List<ToolbarRulesetTabButton> tabButtons = new List<ToolbarRulesetTabButton>();

        [Resolved]
        private MusicController musicController { get; set; }

        private readonly Dictionary<RulesetInfo, Sample> rulesetSelectionSample = new Dictionary<RulesetInfo, Sample>();
        private readonly Dictionary<RulesetInfo, SampleChannel> rulesetSelectionChannel = new Dictionary<RulesetInfo, SampleChannel>();
        private Sample defaultSelectSample;

        private ISampleStore samples;

        public ToolbarRulesetSelector()
        {
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuGameBase game)
        {
            AddRangeInternal(new[]
            {
                new OpaqueBackground
                {
                    Depth = 1,
                    Masking = true,
                },
                ModeButtonLine = new Container
                {
                    Size = new Vector2(Toolbar.HEIGHT, 3),
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Y = -1,
                    Children = new Drawable[]
                    {
                        modeButtonCircle = new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(18, 3),
                        }
                    }
                },
            });

            var store = new ResourceStore<byte[]>(game.Resources);
            samples = audio.GetSampleStore(new NamespacedResourceStore<byte[]>(store, "Samples"), audio.SampleMixer);

            foreach (var r in Rulesets.AvailableRulesets)
            {
                store.AddStore(r.CreateInstance().CreateResourceStore());

                rulesetSelectionSample[r] = samples.Get($@"UI/ruleset-select-{r.ShortName}");
            }

            defaultSelectSample = audio.Samples.Get(@"UI/default-select");

            Current.ValueChanged += playRulesetSelectionSample;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindDisabledChanged(_ => Scheduler.AddOnce(currentDisabledChanged));
            currentDisabledChanged();

            // The toolbar may have been given a placement before this selector was added to the tree.
            updateLayout();
            applyTabOrder();

            Current.BindValueChanged(_ => moveLineToCurrent());
            // Scheduled to allow the button flow layout to be computed before the line position is updated
            ScheduleAfterChildren(moveLineToCurrent);
        }

        private void currentDisabledChanged()
        {
            this.FadeColour(Current.Disabled ? Color4.Gray : Color4.White, 300);
        }

        private bool hasInitialPosition;

        private void moveLineToCurrent()
        {
            if (SelectedTab != null)
            {
                if (isVertical)
                    ModeButtonLine.MoveToY(SelectedTab.DrawPosition.Y, !hasInitialPosition ? 0 : 500, Easing.OutElasticQuarter);
                else
                    ModeButtonLine.MoveToX(SelectedTab.DrawPosition.X, !hasInitialPosition ? 0 : 500, Easing.OutElasticQuarter);

                hasInitialPosition = true;
            }
        }

        private bool isVertical => placement is ToolbarPosition.Left or ToolbarPosition.Right;

        /// <summary>
        /// Whether this selector is currently rendered vertically (in a left/right docked toolbar).
        /// </summary>
        public bool Vertical => isVertical;

        /// <summary>
        /// Updates the orientation of this selector to match the containing toolbar's placement.
        /// </summary>
        public void SetPlacement(ToolbarPosition placement)
        {
            this.placement = placement;
            updateLayout();
        }

        /// <summary>
        /// Mirrors the order of the ruleset tabs when the containing toolbar is inverted.
        /// </summary>
        public void SetInverted(bool inverted)
        {
            this.inverted = inverted;
            applyTabOrder();
        }

        private void applyTabOrder()
        {
            if (TabContainer == null)
                return;

            var tabs = TabContainer.Children.ToArray();

            if (tabs.Length == 0 || tabOrderInverted == inverted)
                return;

            tabOrderInverted = inverted;

            TabContainer.Clear(false);

            for (int i = tabs.Length - 1; i >= 0; i--)
                TabContainer.Add(tabs[i]);

            ScheduleAfterChildren(moveLineToCurrent);
        }

        protected override void AddTabItem(TabItem<RulesetInfo> tab, bool addToDropdown = true)
        {
            base.AddTabItem(tab, addToDropdown);

            if (tab is ToolbarRulesetTabButton button)
            {
                tabButtons.Add(button);
                button.SetPlacement(placement);
            }
        }

        private void updateLayout()
        {
            AutoSizeAxes = Axes.None;
            RelativeSizeAxes = isVertical ? Axes.X : Axes.Y;
            if (isVertical)
                Width = 1;
            else
                Height = 1;
            AutoSizeAxes = isVertical ? Axes.Y : Axes.X;

            TabContainer.AutoSizeAxes = Axes.None;
            TabContainer.AllowMultiline = isVertical;
            TabContainer.Direction = isVertical ? FillDirection.Vertical : FillDirection.Horizontal;
            TabContainer.RelativeSizeAxes = isVertical ? Axes.X : Axes.Y;
            if (isVertical)
                TabContainer.Width = 1;
            else
                TabContainer.Height = 1;
            TabContainer.AutoSizeAxes = isVertical ? Axes.Y : Axes.X;

            if (ModeButtonLine != null)
            {
                ModeButtonLine.Size = isVertical ? new Vector2(3, Toolbar.HEIGHT) : new Vector2(Toolbar.HEIGHT, 3);
                ModeButtonLine.Anchor = isVertical ? Anchor.TopRight : Anchor.BottomLeft;
                ModeButtonLine.Origin = isVertical ? Anchor.TopRight : Anchor.BottomLeft;
                ModeButtonLine.Position = isVertical ? new Vector2(-1, 0) : new Vector2(0, -1);
            }

            if (modeButtonCircle != null)
                modeButtonCircle.Size = isVertical ? new Vector2(3, 18) : new Vector2(18, 3);

            foreach (var button in tabButtons)
                button.SetPlacement(placement);

            moveLineToCurrent();
        }

        private void playRulesetSelectionSample(ValueChangedEvent<RulesetInfo> r)
        {
            // Don't play sample on first setting of value
            if (r.OldValue == null)
                return;

            var channel = rulesetSelectionSample[r.NewValue]?.GetChannel();

            // Skip sample choking and ducking for the default/fallback sample
            if (channel == null)
            {
                defaultSelectSample.Play();
                return;
            }

            foreach (var pair in rulesetSelectionChannel)
                pair.Value?.Stop();

            rulesetSelectionChannel[r.NewValue] = channel;
            channel.Play();

            // Longer unduck delay for Mania sample
            int unduckDelay = r.NewValue.OnlineID == 3 ? 750 : 500;
            musicController?.DuckMomentarily(unduckDelay, new DuckParameters { DuckDuration = 0 });
        }

        public override bool HandleNonPositionalInput => !Current.Disabled && base.HandleNonPositionalInput;

        public override bool HandlePositionalInput => !Current.Disabled && base.HandlePositionalInput;

        public override bool PropagatePositionalInputSubTree => !Current.Disabled && base.PropagatePositionalInputSubTree;

        protected override TabItem<RulesetInfo> CreateTabItem(RulesetInfo value) => new ToolbarRulesetTabButton(value);

        protected override TabFillFlowContainer CreateTabFlow() => new TabFillFlowContainer
        {
            RelativeSizeAxes = Axes.Y,
            AutoSizeAxes = Axes.X,
            Direction = FillDirection.Horizontal,
        };

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            base.OnKeyDown(e);

            if (e.ControlPressed && !e.Repeat && e.Key >= Key.Number1 && e.Key <= Key.Number9)
            {
                int requested = e.Key - Key.Number1;

                RulesetInfo found = Rulesets.AvailableRulesets.ElementAtOrDefault(requested);
                if (found != null)
                    SelectItem(found);
                return true;
            }

            return false;
        }

        protected override void Dispose(bool isDisposing)
        {
            samples?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
