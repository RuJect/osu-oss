// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;

namespace osu.Game.Users.Drawables
{
    public partial class DrawableFlag : BaseDrawableFlag, IHasTooltip
    {
        public LocalisableString TooltipText => CountryCode == CountryCode.Unknown ? string.Empty : CountryCode.GetDescription();

        public DrawableFlag(CountryCode countryCode) : base(countryCode) { }

        [BackgroundDependencyLoader]
        private void load(TextureStore ts)
        {
            ArgumentNullException.ThrowIfNull(ts);

            string textureName = CountryCode == CountryCode.Unknown ? "__" : CountryCode.ToString();
            Texture = ts.Get($@"Flags/{textureName}") ?? ts.Get(@"Flags/__");
        }
    }
}
