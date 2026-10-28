// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.InteropServices;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// Wire format of the game &lt;-&gt; supervisor control channel.
    /// </summary>
    public static class ControlProtocol
    {
        /// <summary>Magic of a published mapping range message (<c>"AUR0"</c>).</summary>
        public const uint RANGE_MAGIC = 0x41555230u;

        /// <summary>A single mapping (<c>[base, end)</c>) of a proprietary module.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RangeMessage
        {
            public uint Magic;
            public uint Reserved;
            public ulong Base;
            public ulong End;
        }
    }
}
