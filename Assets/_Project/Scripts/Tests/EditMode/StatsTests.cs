// StatsTests.cs
// ApplyToAll 헬퍼 sanity 검증. 카테고리별 패턴 동일하므로 TechnicalStats 로 대표.

using NUnit.Framework;
using FMLite.Domain;

namespace FMLite.Tests
{
    public class StatsTests
    {
        [Test]
        public void TechnicalStats_ApplyToAll_AppliesModifierToEveryField()
        {
            var t = new TechnicalStats
            {
                passing = 10,
                shooting = 11,
                tackling = 12,
                dribbling = 13,
                heading = 14,
                crossing = 15,
                firstTouch = 16,
                finishing = 17,
                longShots = 18,
                freeKickAccuracy = 19,
                penaltyTaking = 20,
                corners = 21,
            };

            t.ApplyToAll(x => x + 1);

            Assert.AreEqual(11, t.passing);
            Assert.AreEqual(12, t.shooting);
            Assert.AreEqual(13, t.tackling);
            Assert.AreEqual(14, t.dribbling);
            Assert.AreEqual(15, t.heading);
            Assert.AreEqual(16, t.crossing);
            Assert.AreEqual(17, t.firstTouch);
            Assert.AreEqual(18, t.finishing);
            Assert.AreEqual(19, t.longShots);
            Assert.AreEqual(20, t.freeKickAccuracy);
            Assert.AreEqual(21, t.penaltyTaking);
            Assert.AreEqual(22, t.corners);
        }

        [Test]
        public void MentalStats_ApplyToAll_Works()
        {
            var m = new MentalStats { vision = 10, aggression = 5 };
            m.ApplyToAll(x => x * 2);
            Assert.AreEqual(20, m.vision);
            Assert.AreEqual(10, m.aggression);
        }

        [Test]
        public void PhysicalStats_ApplyToAll_Works()
        {
            var p = new PhysicalStats { pace = 12, stamina = 8 };
            p.ApplyToAll(x => x - 1);
            Assert.AreEqual(11, p.pace);
            Assert.AreEqual(7, p.stamina);
        }

        [Test]
        public void GoalkeepingStats_ApplyToAll_Works()
        {
            var g = new GoalkeepingStats { handling = 15, reflexes = 17 };
            g.ApplyToAll(x => x);
            Assert.AreEqual(15, g.handling);
            Assert.AreEqual(17, g.reflexes);
        }
    }
}
