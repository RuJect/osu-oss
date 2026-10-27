// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API
{
    /// <summary>
    /// Resolves whether local, client-side supporter-only features are available.
    /// This deliberately does not affect server-side supporter checks.
    /// </summary>
    public static class SupporterAccess
    {
        public static bool IsSupporter(APIUser? user, SpecialConfigManager? config)
            => user?.IsSupporter == true
               || config?.Get<bool>(SpecialSetting.UnlockLocalSupporterFeatures) == true;
    }
}
