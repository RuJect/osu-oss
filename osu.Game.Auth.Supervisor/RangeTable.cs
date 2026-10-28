// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Auth.Isolation.Supervisor
{
    /// <summary>
    /// The set of proprietary-module mappings published by the game. Overlapping ranges are merged
    /// so membership tests stay cheap.
    /// </summary>
    internal sealed class RangeTable
    {
        private const int max_ranges = 512;

        private readonly List<(ulong Base, ulong End)> ranges = new List<(ulong, ulong)>();

        public void Add(ulong @base, ulong end)
        {
            if (end <= @base)
                return;

            for (int i = 0; i < ranges.Count; i++)
            {
                (ulong baseI, ulong endI) = ranges[i];

                if (@base <= endI && end >= baseI)
                {
                    ranges[i] = (Math.Min(baseI, @base), Math.Max(endI, end));
                    return;
                }
            }

            if (ranges.Count >= max_ranges)
                return;

            ranges.Add((@base, end));
        }

        public bool Contains(ulong ip)
        {
            foreach ((ulong @base, ulong end) in ranges)
            {
                if (ip >= @base && ip < end)
                    return true;
            }

            return false;
        }
    }
}
