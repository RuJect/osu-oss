// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// A simple, script-created overlay. Contents are added via <see cref="AddContent"/> and the overlay is
    /// shown/hidden with the standard <see cref="VisibilityContainer.Show"/>/<see cref="VisibilityContainer.Hide"/> methods.
    /// </summary>
    public partial class ScriptOverlay : OverlayContainer
    {
        private readonly FillFlowContainer flow;

        public ScriptOverlay(string title)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.Black,
                Alpha = 0.85f,
            });

            AddInternal(flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Padding = new MarginPadding(16),
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = title, Colour = Colour4.White },
                }
            });
        }

        /// <summary>Adds a drawable to the overlay's content flow. Called on the update thread.</summary>
        public void AddContent(Drawable content) => flow.Add(content);

        protected override void PopIn() => this.FadeIn(150, Easing.OutQuint);

        protected override void PopOut() => this.FadeOut(150, Easing.InQuint);
    }
}
