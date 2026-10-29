// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class FavouriteBeatmapSetHighlight : CompositeDrawable
    {
        public readonly BindableBool Active = new BindableBool();

        private FavouriteParticleLayer particleLayer = null!;

        public FavouriteBeatmapSetHighlight()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = Panel.CORNER_RADIUS,
                    BorderThickness = 3,
                    BorderColour = colours.Pink1,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Pink1.Opacity(0.01f),
                    },
                },
                particleLayer = new FavouriteParticleLayer
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Active.BindValueChanged(active =>
            {
                this.FadeTo(active.NewValue ? 1 : 0, 250, Easing.OutQuint);
                particleLayer.Active.Value = active.NewValue;
            }, true);
        }

        private partial class FavouriteParticleLayer : CompositeDrawable
        {
            public readonly BindableBool Active = new BindableBool();

            private const double circle_interval = 70;
            private const double heart_interval = 700;

            private double? lastCircleSpawn;
            private double? lastHeartSpawn;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            protected override void Update()
            {
                base.Update();

                if (!Active.Value || DrawWidth <= 0 || DrawHeight <= 0)
                    return;

                double now = Time.Current;

                if (lastCircleSpawn == null || now - lastCircleSpawn >= circle_interval)
                {
                    spawnCircle();
                    lastCircleSpawn = now;
                }

                if (lastHeartSpawn == null || now - lastHeartSpawn >= heart_interval)
                {
                    spawnHeart();
                    lastHeartSpawn = now;
                }
            }

            private void spawnCircle()
            {
                float size = RNG.NextSingle(2, 7);
                Vector2 position = randomPosition();

                var particle = new FastCircle
                {
                    Position = position,
                    Origin = Anchor.Centre,
                    Size = new Vector2(size),
                    Colour = colours.Pink1,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };

                AddInternal(particle);

                double duration = RNG.NextDouble(700, 1400);

                particle
                    .FadeTo(0.8f, 200, Easing.OutQuint)
                    .Then()
                    .FadeOut(duration, Easing.OutQuint)
                    .MoveTo(position + randomDrift(), duration, Easing.OutQuint)
                    .ScaleTo(RNG.NextSingle(0.2f, 0.8f), duration, Easing.OutQuint)
                    .Expire();
            }

            private void spawnHeart()
            {
                float size = RNG.NextSingle(10, 18);
                Vector2 position = randomPosition();

                var particle = new SpriteIcon
                {
                    Position = position,
                    Origin = Anchor.Centre,
                    Size = new Vector2(size),
                    Icon = FontAwesome.Solid.Heart,
                    Colour = colours.Pink0,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };

                AddInternal(particle);

                double duration = RNG.NextDouble(1000, 1800);

                particle
                    .FadeTo(0.9f, 250, Easing.OutQuint)
                    .Then()
                    .FadeOut(duration, Easing.OutQuint)
                    .MoveTo(position + randomDrift(), duration, Easing.OutQuint)
                    .ScaleTo(RNG.NextSingle(0.4f, 1.1f), duration, Easing.OutQuint)
                    .Expire();
            }

            private Vector2 randomPosition() => new Vector2(RNG.NextSingle(0, DrawWidth), RNG.NextSingle(0, DrawHeight));

            private Vector2 randomDrift() => new Vector2(RNG.NextSingle(-30, 30), RNG.NextSingle(-40, 10));
        }
    }
}
