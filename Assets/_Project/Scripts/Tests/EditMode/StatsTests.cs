// StatsTests.cs
// ApplyToAll 헬퍼 + 49 필드 개수 검증.

using FMLite.Domain;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class StatsTests
    {
        [Test]
        public void TechnicalStats_ApplyToAll_AppliesModifierToAllFourteenFields()
        {
            var t = new TechnicalStats
            {
                passing = 1,
                tackling = 2,
                dribbling = 3,
                heading = 4,
                crossing = 5,
                firstTouch = 6,
                finishing = 7,
                longShots = 8,
                freeKickTaking = 9,
                penaltyTaking = 10,
                corners = 11,
                marking = 12,
                technique = 13,
                longThrows = 14,
            };

            t.ApplyToAll(x => x + 10);

            Assert.AreEqual(11, t.passing);
            Assert.AreEqual(12, t.tackling);
            Assert.AreEqual(13, t.dribbling);
            Assert.AreEqual(14, t.heading);
            Assert.AreEqual(15, t.crossing);
            Assert.AreEqual(16, t.firstTouch);
            Assert.AreEqual(17, t.finishing);
            Assert.AreEqual(18, t.longShots);
            Assert.AreEqual(19, t.freeKickTaking);
            Assert.AreEqual(20, t.penaltyTaking);
            Assert.AreEqual(21, t.corners);
            Assert.AreEqual(22, t.marking);
            Assert.AreEqual(23, t.technique);
            Assert.AreEqual(24, t.longThrows);
        }

        [Test]
        public void MentalStats_ApplyToAll_AppliesModifierToAllFourteenFields()
        {
            var m = new MentalStats
            {
                vision = 1,
                anticipation = 1,
                composure = 1,
                concentration = 1,
                decisions = 1,
                determination = 1,
                leadership = 1,
                offTheBall = 1,
                positioning = 1,
                teamwork = 1,
                workRate = 1,
                aggression = 1,
                bravery = 1,
                flair = 1,
            };
            m.ApplyToAll(x => x * 5);
            Assert.AreEqual(5, m.vision);
            Assert.AreEqual(5, m.bravery);
            Assert.AreEqual(5, m.flair);
        }

        [Test]
        public void PhysicalStats_ApplyToAll_Works()
        {
            var p = new PhysicalStats
            {
                pace = 12,
                stamina = 8,
                jumpingReach = 10,
            };
            p.ApplyToAll(x => x - 1);
            Assert.AreEqual(11, p.pace);
            Assert.AreEqual(7, p.stamina);
            Assert.AreEqual(9, p.jumpingReach);
        }

        [Test]
        public void GoalkeepingStats_ApplyToAll_AppliesModifierToAllThirteenFields()
        {
            var g = new GoalkeepingStats
            {
                aerialReach = 1,
                commandOfArea = 1,
                communication = 1,
                eccentricity = 1,
                handling = 1,
                kicking = 1,
                oneOnOnes = 1,
                reflexes = 1,
                rushingOut = 1,
                throwing = 1,
                firstTouchGk = 1,
                passingGk = 1,
                punchingTendency = 1,
            };
            g.ApplyToAll(x => x + 2);
            Assert.AreEqual(3, g.handling);
            Assert.AreEqual(3, g.firstTouchGk);
            Assert.AreEqual(3, g.passingGk);
            Assert.AreEqual(3, g.punchingTendency);
        }
    }
}
