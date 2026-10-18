// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.API.UI.Scripting.Components
{
    /// <summary>
    /// A <see cref="ShearedButton"/> which displays a leading icon alongside its label. Both elements
    /// inherit the button's text colour, so hover and disabled states remain consistent.
    /// </summary>
    public partial class ShearedIconButton : ShearedButton
    {
        public ShearedIconButton(IconUsage icon, string text)
        {
            TextColour = Colour4.White;
            TextSize = 14;

            ButtonContent.Clear();
            ButtonContent.Add(new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(13),
                        Icon = icon,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text,
                        Font = OsuFont.TorusAlternate.With(null, 14),
                    },
                },
            });
        }
    }
}
