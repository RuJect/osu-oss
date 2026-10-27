// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SupporterAccessTest
    {
        [Test]
        public void TestActualSupporter()
        {
            using var config = createConfig();

            ClassicAssert.IsTrue(SupporterAccess.IsSupporter(new APIUser { IsSupporter = true }, config));
        }

        [Test]
        public void TestLocalUnlockRequiresOptIn()
        {
            using var config = createConfig();
            var user = new APIUser { IsSupporter = false };

            ClassicAssert.IsFalse(SupporterAccess.IsSupporter(user, config));

            config.GetBindable<bool>(SpecialSetting.UnlockLocalSupporterFeatures).Value = true;

            ClassicAssert.IsTrue(SupporterAccess.IsSupporter(user, config));
        }

        private static SpecialConfigManager createConfig()
            => new SpecialConfigManager(new TemporaryNativeStorage($"supporter-access-test-{Guid.NewGuid():N}"));
    }
}
