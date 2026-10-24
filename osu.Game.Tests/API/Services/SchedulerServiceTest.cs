// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.API.Services;

namespace osu.Game.Tests.API.Services
{
    [TestFixture]
    public class SchedulerServiceTest
    {
        private SchedulerService scheduler = null!;
        private readonly List<Exception> errors = new List<Exception>();

        [SetUp]
        public void SetUp()
        {
            scheduler = new SchedulerService();
            errors.Clear();
        }

        [Test]
        public void TestElapsedIsRelativeToFirstTick()
        {
            scheduler.Tick(1000, e => errors.Add(e));
            ClassicAssert.AreEqual(0, scheduler.Elapsed);

            scheduler.Tick(1500, e => errors.Add(e));
            ClassicAssert.AreEqual(500, scheduler.Elapsed);
        }

        [Test]
        public void TestTimerFiresOnceAndIsRemovedWhenNotRepeating()
        {
            int fired = 0;
            scheduler.AddTimer(() => fired++, 100, false);

            scheduler.Tick(0, e => errors.Add(e));
            ClassicAssert.AreEqual(0, fired);

            scheduler.Tick(100, e => errors.Add(e));
            ClassicAssert.AreEqual(1, fired);

            scheduler.Tick(200, e => errors.Add(e));
            ClassicAssert.AreEqual(1, fired);
            ClassicAssert.IsFalse(scheduler.HasPendingCallbacks);
        }

        [Test]
        public void TestRepeatingTimerFiresEachInterval()
        {
            int fired = 0;
            scheduler.AddTimer(() => fired++, 100, true);

            scheduler.Tick(0, e => errors.Add(e));
            scheduler.Tick(100, e => errors.Add(e));
            scheduler.Tick(200, e => errors.Add(e));

            ClassicAssert.AreEqual(2, fired);
        }

        [Test]
        public void TestCancelTimerStopsIt()
        {
            int fired = 0;
            int id = scheduler.AddTimer(() => fired++, 100, true);

            ClassicAssert.IsTrue(scheduler.CancelTimer(id));
            ClassicAssert.IsFalse(scheduler.CancelTimer(id));

            scheduler.Tick(100, e => errors.Add(e));
            scheduler.Tick(200, e => errors.Add(e));

            ClassicAssert.AreEqual(0, fired);
        }

        [Test]
        public void TestUpdateHandlersRunEveryTick()
        {
            int ticks = 0;
            scheduler.AddUpdateHandler(() => ticks++);

            scheduler.Tick(0, e => errors.Add(e));
            scheduler.Tick(16, e => errors.Add(e));

            ClassicAssert.AreEqual(2, ticks);
        }

        [Test]
        public void TestHandlerExceptionIsReportedAndDoesNotStopOthers()
        {
            int ran = 0;
            scheduler.AddUpdateHandler(() => throw new InvalidOperationException("boom"));
            scheduler.AddUpdateHandler(() => ran++);

            scheduler.Tick(0, e => errors.Add(e));

            ClassicAssert.AreEqual(1, ran);
            ClassicAssert.AreEqual(1, errors.Count);
            ClassicAssert.AreEqual("boom", errors[0].Message);
        }
    }
}
