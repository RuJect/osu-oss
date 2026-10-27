// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    public partial class MenuLogoVisualisation : LogoVisualisation
    {
        private IBindable<APIUser> user = null!;
        private Bindable<Skin> skin = null!;

        [Resolved(canBeNull: true)]
        private SpecialConfigManager? specialConfig { get; set; }

        private IBindable<bool>? unlockLocalSupporterFeatures;

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api, SkinManager skinManager)
        {
            user = api.LocalUser.GetBoundCopy();
            skin = skinManager.CurrentSkin.GetBoundCopy();

            user.ValueChanged += _ => UpdateColour();
            skin.BindValueChanged(_ => UpdateColour(), true);

            if (specialConfig != null)
            {
                unlockLocalSupporterFeatures = specialConfig.GetBindable<bool>(SpecialSetting.UnlockLocalSupporterFeatures);
                unlockLocalSupporterFeatures.BindValueChanged(_ => UpdateColour());
            }
        }

        protected virtual void UpdateColour()
        {
            if (SupporterAccess.IsSupporter(user.Value, specialConfig))
                Colour = skin.Value.GetConfig<GlobalSkinColours, Color4>(GlobalSkinColours.MenuGlow)?.Value ?? Color4.White;
            else
                Colour = Color4.White;
        }
    }
}
