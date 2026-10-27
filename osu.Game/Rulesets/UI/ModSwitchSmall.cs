// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.UI
{
    public partial class ModSwitchSmall : CompositeDrawable
    {
        public BindableBool Active { get; } = new BindableBool();

        public const float DEFAULT_SIZE = 60;

        private readonly IMod mod;

        private Drawable background = null!;
        private SpriteIcon? modIcon;
        private Sprite? skinIcon;
        private bool skinIconUsed;

        private Color4 activeForegroundColour;
        private Color4 inactiveForegroundColour;

        private Color4 activeBackgroundColour;
        private Color4 inactiveBackgroundColour;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private SpecialConfigManager? specialConfig { get; set; } = null!;

        private bool UseSkinModIcons => specialConfig?.Get<bool>(SpecialSetting.UseSkinModIcons) == true;

        private IBindable<bool>? useSkinModIconsBindable;

        public ModSwitchSmall(IMod mod)
        {
            this.mod = mod;

            Size = new Vector2(DEFAULT_SIZE);
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, OsuColour colours, OverlayColourProvider? colourProvider)
        {
            InternalChildren = new[]
            {
                background = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Texture = textures.Get("Icons/BeatmapDetails/mod-icon"),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                skinIcon = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Texture = null,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0,
                },
                contentFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    Child = tinySwitch = new ModSwitchTiny(mod)
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Scale = new Vector2(0.6f),
                        Active = { BindTarget = Active }
                    }
                }
            };

            var modTypeColour = colours.ForModType(mod.Type);

            inactiveForegroundColour = colourProvider?.Background5 ?? colours.Gray3;
            activeForegroundColour = modTypeColour;

            inactiveBackgroundColour = colourProvider?.Background2 ?? colours.Gray5;
            activeBackgroundColour = Interpolation.ValueAt<Colour4>(0.1f, Colour4.Black, modTypeColour, 0, 1);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Active.BindValueChanged(_ => updateState(), true);
            skin.SourceChanged += onSkinChanged;

            if (specialConfig != null)
            {
                useSkinModIconsBindable = specialConfig.GetBindable<bool>(SpecialSetting.UseSkinModIcons);
                useSkinModIconsBindable.BindValueChanged(_ => onSkinChanged());
            }

            FinishTransforms(true);
            updateIcon();
            updateState(); // Apply initial colours after modIcon is created
        }

        private void onSkinChanged() => Scheduler.AddOnce(updateIcon);

        private void updateIcon()
        {
            if (UseSkinModIcons && mod is Mod skinMod && skin.GetTexture(ModSkinTextureResolver.GetTextureName(skinMod)) is { } skinTexture)
            {
                skinIcon!.Texture = skinTexture;
                skinIcon.FadeIn();
                background.FadeOut();
                tinySwitch.ShowPill = false;
                skinIconUsed = true;
            }
            else
            {
                skinIcon!.Texture = null;
                skinIcon.FadeOut();
                background.FadeIn();
                skinIconUsed = false;

                if (mod.Icon != null)
                {
                    // Lazy-create modIcon if it doesn't exist yet
                    if (modIcon == null)
                    {
                        contentFlow.Insert(-1, modIcon = new SpriteIcon
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(37, 26),
                            // arbitrary adjustment for better vertical alignment
                            Margin = new MarginPadding { Top = -1 },
                            Icon = mod.Icon.Value
                        });
                        tinySwitch.Scale = new Vector2(0.3f);
                    }
                    else
                    {
                        modIcon.Icon = mod.Icon.Value;
                    }
                    modIcon.FadeIn();
                }
                else if (modIcon != null)
                {
                    modIcon.FadeOut();
                }

                tinySwitch.ShowPill = true;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin != null)
                skin.SourceChanged -= onSkinChanged;

            useSkinModIconsBindable?.UnbindAll();
        }

        private void updateState()
        {
            if (!skinIconUsed)
            {
                modIcon?.FadeColour(Active.Value ? activeForegroundColour : inactiveForegroundColour, 200, Easing.OutQuint);
                background.FadeColour(Active.Value ? activeBackgroundColour : inactiveBackgroundColour, 200, Easing.OutQuint);
            }
        }

        // ReSharper disable once UnusedMember.Local
        private ModSwitchTiny tinySwitch = null!;
        // ReSharper disable once UnusedMember.Local
        private FillFlowContainer contentFlow = null!;
    }
}