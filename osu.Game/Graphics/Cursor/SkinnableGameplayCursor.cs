// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Renders the active skin's gameplay cursor outside of gameplay, mirroring the scale, rotation and click
    /// behaviour of the osu! ruleset cursor without taking a reference on <c>osu.Game.Rulesets.Osu</c>.
    /// </summary>
    public partial class SkinnableGameplayCursor : CompositeDrawable
    {
        public const float BASE_SIZE = 50f;

        private const float pressed_scale = 1.2f;
        private const float released_scale = 1f;
        private const int rotation_revolution_duration_ms = 10_000;
        private const float default_cursor_size = 28f;

        private Container scaleContainer = null!;
        private Drawable? rotationTarget;

        private Bindable<float> gameplayCursorSize = null!;
        private bool expandOnClick;
        private float currentExpandFactor = released_scale;

        [Resolved(canBeNull: true)]
        private ISkinSource? skinSource { get; set; }

        [Resolved(canBeNull: true)]
        private SkinManager? skinManager { get; set; }

        public SkinnableGameplayCursor()
        {
            Size = new Vector2(BASE_SIZE);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            gameplayCursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize);

            InternalChild = scaleContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            buildCursor();

            gameplayCursorSize.BindValueChanged(_ => updateScale(), true);

            if (skinSource != null)
                skinSource.SourceChanged += onSkinSourceChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource != null)
                skinSource.SourceChanged -= onSkinSourceChanged;

            base.Dispose(isDisposing);
        }

        public void Expand()
        {
            if (!expandOnClick)
                return;

            currentExpandFactor = pressed_scale;
            scaleContainer
                .ScaleTo(targetScale(released_scale))
                .ScaleTo(targetScale(pressed_scale), 400, Easing.OutElasticHalf);
        }

        public void Contract()
        {
            currentExpandFactor = released_scale;
            scaleContainer.ScaleTo(targetScale(released_scale), 400, Easing.OutQuad);
        }

        private void onSkinSourceChanged() => Schedule(buildCursor);

        private void buildCursor()
        {
            expandOnClick = skinSource?.GetConfig<LegacyCursorSkinConfiguration, bool>(LegacyCursorSkinConfiguration.CursorExpand)?.Value ?? true;

            scaleContainer.Child = rotationTarget = createCursorSprites();

            if (rotationTarget != null && shouldRotate())
                rotationTarget.Spin(rotation_revolution_duration_ms, RotationDirection.Clockwise);
        }

        private void updateScale() => scaleContainer.Scale = targetScale(currentExpandFactor);

        private Vector2 targetScale(float expandFactor) => new Vector2(gameplayCursorSize.Value * expandFactor);

        private bool shouldRotate()
            => skinSource?.GetConfig<LegacyCursorSkinConfiguration, bool>(LegacyCursorSkinConfiguration.CursorRotate)?.Value ?? true;

        private Drawable createCursorSprites()
        {
            if (isArgonSkin())
                return createArgonCursor();

            ISkin? cursorProvider = skinSource?.FindProvider(s => s.GetTexture(@"cursor") != null);

            // Non-legacy system skins (e.g. triangles) don't provide their own cursor textures.
            // Gameplay falls back to the classic skin for these, so mirror that here.
            if (cursorProvider == null && skinManager?.DefaultClassicSkin.GetTexture(@"cursor") != null)
                cursorProvider = skinManager.DefaultClassicSkin;

            Texture? cursor = cursorProvider?.GetTexture(@"cursor");

            if (cursor == null)
                return createDefaultCursor();

            var stack = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            stack.Add(new Sprite
            {
                Texture = cursor,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });

            Texture? middle = cursorProvider?.GetTexture(@"cursormiddle");

            if (middle != null)
            {
                stack.Add(new Sprite
                {
                    Texture = middle,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }

            return stack;
        }

        private bool isArgonSkin() => skinSource?.AllSources.Any(s => s is ArgonSkin) == true;

        private Drawable createArgonCursor()
        {
            return new Container
            {
                Size = new Vector2(default_cursor_size),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = 6,
                        BorderColour = ColourInfo.GradientVertical(Colour4.FromHex("FC618F"), Colour4.FromHex("BB1A41")),
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0.4f,
                                Colour = Colour4.FromHex("FC618F").Darken(0.6f),
                            },
                            new CircularContainer
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                BorderThickness = 2,
                                BorderColour = Color4.White.Opacity(0.8f),
                                Child = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    AlwaysPresent = true,
                                },
                            },
                        },
                    },
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Scale = new Vector2(0.2f),
                        Colour = Color4.White,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Radius = 20,
                            Colour = new Color4(171, 255, 255, 100),
                        },
                    },
                },
            };
        }

        private Drawable createDefaultCursor()
        {
            return new Container
            {
                Size = new Vector2(default_cursor_size),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    new CircularContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = default_cursor_size / 6,
                        BorderColour = Color4.White,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Shadow,
                            Colour = Color4.Pink.Opacity(0.5f),
                            Radius = 5,
                        },
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true,
                        },
                    },
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Scale = new Vector2(0.14f),
                        Colour = new Color4(34, 93, 204, 255),
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Radius = 8,
                            Colour = Color4.White,
                        },
                    },
                },
            };
        }
    }
}
