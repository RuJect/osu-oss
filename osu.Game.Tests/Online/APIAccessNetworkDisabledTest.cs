// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Online.API;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class APIAccessNetworkDisabledTest : ImportTest
    {
        [Test]
        public void TestNetworkDisabled()
        {
            using (var host = new CleanRunHeadlessGameHost())
            {
                try
                {
                    var osu = LoadOsuIntoHost(host, noNetwork: true);

                    Assert.That(osu.API, Is.TypeOf<APIAccess>());
                    Assert.That(osu.API.State.Value, Is.EqualTo(APIState.Offline));
                    Assert.That(osu.API.IsLoggedIn, Is.False);
                    Assert.That(osu.API.NotificationsClient.IsConnected.Value, Is.False);

                    var request = new TestRequest();
                    osu.API.Perform(request);
                    Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        private class TestRequest : APIRequest
        {
            protected override string Target => @"test";
        }
    }
}
