// GameTimeTests.cs
// DoD 검증: v0.1-tasks.md Task 2.2.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using FMLite.Core;

namespace FMLite.Tests
{
    public class GameTimeTests
    {
        private readonly DateTime _baseline = new DateTime(2024, 7, 1);

        [SetUp]
        public void Setup()
        {
            EventBus.Clear();
            GameTime.Reset(_baseline);
        }

        [Test]
        public void Advance_OneDay_AdvancesDate()
        {
            GameTime.Advance(1);
            Assert.AreEqual(_baseline.AddDays(1), GameTime.CurrentDate);
        }

        [Test]
        public void Advance_OneDay_FiresEventOnce()
        {
            int count = 0;
            EventBus.Subscribe<DayAdvancedEvent>(_ => count++);

            GameTime.Advance(1);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Advance_MultipleDays_FiresEventPerDayWithCorrectDates()
        {
            var received = new List<DateTime>();
            EventBus.Subscribe<DayAdvancedEvent>(e => received.Add(e.newDate));

            GameTime.Advance(5);

            Assert.AreEqual(5, received.Count);
            Assert.AreEqual(_baseline.AddDays(5), GameTime.CurrentDate);
            Assert.AreEqual(_baseline.AddDays(1), received[0]);
            Assert.AreEqual(_baseline.AddDays(5), received[4]);
        }

        [Test]
        public void Advance_ZeroOrNegative_DoesNothing()
        {
            int count = 0;
            EventBus.Subscribe<DayAdvancedEvent>(_ => count++);

            GameTime.Advance(0);
            GameTime.Advance(-5);

            Assert.AreEqual(_baseline, GameTime.CurrentDate);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Reset_SetsDateWithoutFiringEvent()
        {
            int count = 0;
            EventBus.Subscribe<DayAdvancedEvent>(_ => count++);

            var newDate = new DateTime(2025, 1, 1);
            GameTime.Reset(newDate);

            Assert.AreEqual(newDate, GameTime.CurrentDate);
            Assert.AreEqual(0, count);
        }
    }
}
