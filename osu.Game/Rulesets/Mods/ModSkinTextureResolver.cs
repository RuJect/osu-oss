// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Resolves the skin texture name for a given <see cref="Mod"/> according to osu!stable's skinning rules.
    /// </summary>
    /// <remarks>
    /// osu!stable uses <c>selection-mod-<name>.png</c> for mod icons, where <c>&lt;name&gt;</c> is a stable-specific identifier.
    /// These identifiers do not always match the mod's <see cref="Mod.Acronym"/> (e.g. DT → doubletime).
    /// </remarks>
    public static class ModSkinTextureResolver
    {
        /// <summary>
        /// Maps a mod's <see cref="Mod.Acronym"/> to the stable skin texture name.
        /// </summary>
        private static readonly Dictionary<string, string> SkinTextureNameMap = new Dictionary<string, string>
        {
            // osu!
            { "DT", "doubletime" },
            { "HR", "hardrock" },
            { "HD", "hidden" },
            { "NF", "nofail" },
            { "NC", "nightcore" },
            { "SD", "suddendeath" },
            { "PF", "perfect" },
            { "SO", "spunout" },
            { "RP", "random" },
            { "RX", "relax" },
            { "AP", "relax2" },
            { "TP", "target" },
            { "MR", "mirror" },
            { "CM", "cinema" },
            { "FL", "flashlight" },
            { "FI", "fadein" },
            { "AT", "autoplay" },
            { "V2", "scorev2" },
            { "HT", "halftime" },
            { "K1", "key1" },
            { "K2", "key2" },
            { "K3", "key3" },
            { "K4", "key4" },
            { "K5", "key5" },
            { "K6", "key6" },
            { "K7", "key7" },
            { "K8", "key8" },
            { "K9", "key9" },
            { "KCoop", "keycoop" },
        };

        /// <summary>
        /// Returns the skin texture name for the given mod.
        /// Falls back to the lower-cased <see cref="Mod.Acronym"/> if no mapping exists.
        /// </summary>
        public static string GetTextureName(Mod mod)
        {
            if (mod == null)
                throw new ArgumentNullException(nameof(mod));

            var acronym = mod.Acronym;

            if (SkinTextureNameMap.TryGetValue(acronym, out var name))
                return $"selection-mod-{name}.png";

            return $"selection-mod-{acronym.ToLowerInvariant()}.png";
        }
    }
}