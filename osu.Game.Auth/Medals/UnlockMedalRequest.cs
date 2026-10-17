// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Auth.Medals
{
    public class UnlockMedalRequest : APIRequest
    {
        private readonly int medalId;

        public int MedalId => medalId;

        protected override string Target => $"me/achievements/{MedalId}";

        public UnlockMedalRequest(int medalId)
        {
            this.medalId = medalId;
        }

        protected override WebRequest CreateWebRequest()
        {
            WebRequest request = base.CreateWebRequest();
            request.Method = HttpMethod.Put;
            return request;
        }
    }
}
