// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarRulesetTabButton : TabItem<RulesetInfo>
    {
        private readonly RulesetButton ruleset;

        public ToolbarRulesetTabButton(RulesetInfo value)
            : base(value)
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;
            Child = ruleset = new RulesetButton
            {
                Active = false,
            };

            var rInstance = value.CreateInstance();

            ruleset.TooltipMain = rInstance.Description;
            ruleset.TooltipSub = ToolbarStrings.PlaySomeRuleset(rInstance.Description);
            ruleset.SetIcon(rInstance.CreateIcon());
        }

        protected override void OnActivated() => ruleset.Active = true;

        protected override void OnDeactivated() => ruleset.Active = false;

        /// <summary>
        /// Updates the orientation of this tab's button to match the containing toolbar.
        /// </summary>
        public void SetPlacement(ToolbarPosition placement)
        {
            bool vertical = placement is ToolbarPosition.Left or ToolbarPosition.Right;

            AutoSizeAxes = Axes.None;
            RelativeSizeAxes = Axes.None;

            if (vertical)
            {
                RelativeSizeAxes = Axes.X;
                Size = new Vector2(1, Toolbar.HEIGHT);
            }
            else
            {
                RelativeSizeAxes = Axes.Y;
                Size = new Vector2(Toolbar.HEIGHT, 1);
                AutoSizeAxes = Axes.X;
            }

            ruleset.SetPlacement(placement);
        }

        private partial class RulesetButton : ToolbarButton
        {
            protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds();

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public RulesetButton()
            {
                ButtonContent.Padding = new MarginPadding(PADDING)
                {
                    Bottom = 5
                };
            }

            protected override void UpdateLayout()
            {
                base.UpdateLayout();

                ButtonContent.Padding = Vertical
                    ? new MarginPadding(PADDING) { Right = 5 }
                    : new MarginPadding(PADDING) { Bottom = 5 };
            }

            public bool Active
            {
                set => Scheduler.AddOnce(() =>
                {
                    IconContainer.Colour = value ? Color4Extensions.FromHex("#00FFAA") : colours.GrayF;
                });
            }

            protected override bool OnClick(ClickEvent e)
            {
                Parent!.TriggerClick();
                return base.OnClick(e);
            }
        }
    }
}
