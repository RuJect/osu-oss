// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.API.UI.Scripting
{
    /// <summary>
    /// Standard osu! fullscreen overlay header for the scripting console.
    /// </summary>
    public partial class ScriptingOverlayHeader : OverlayHeader
    {
        protected override OverlayTitle CreateTitle() => new ScriptingTitle();

        private partial class ScriptingTitle : OverlayTitle
        {
            public ScriptingTitle()
            {
                Title = "Scripting Console";
                Description = "Run JavaScript against the live game";
                Icon = FontAwesome.Solid.Terminal;
            }
        }
    }
}
