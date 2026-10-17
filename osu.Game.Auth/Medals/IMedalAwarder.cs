// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game;

namespace osu.Game.Auth.Medals
{
    public interface IMedalAwarder
    {
        int MedalId { get; }

        bool Enabled { get; set; }

        bool CheckMedalCriteria(OsuGame game);
    }
}
