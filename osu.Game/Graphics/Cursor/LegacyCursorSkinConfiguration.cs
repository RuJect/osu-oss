// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// A subset of <c>OsuSkinConfiguration</c> covering the cursor-related skin.ini <c>[General]</c> keys.
    /// Mirrored into <c>osu.Game</c> so the menu-context cursor can read them without referencing
    /// <c>osu.Game.Rulesets.Osu</c> (which would create a circular project dependency).
    /// </summary>
    /// <remarks>
    /// <see cref="Skinning.LegacySkin.genericLookup{TLookup,TValue}"/> keys the parsed configuration by
    /// the enum value's <see cref="object.ToString"/>, so the names must match <c>OsuSkinConfiguration</c> exactly.
    /// </remarks>
    internal enum LegacyCursorSkinConfiguration
    {
        CursorExpand,
        CursorRotate,
    }
}
