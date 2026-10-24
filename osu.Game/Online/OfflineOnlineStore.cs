// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Stores;

namespace osu.Game.Online
{
    /// <summary>
    /// An <see cref="OnlineStore"/> which refuses to perform any web requests.
    /// </summary>
    public sealed class OfflineOnlineStore : OnlineStore
    {
        protected override string GetLookupUrl(string url) => null!;
    }
}
