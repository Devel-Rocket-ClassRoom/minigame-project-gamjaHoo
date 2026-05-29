using FMLite.Utils;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class StatMetadataTests
    {
        private static readonly string[] AllAbsolute =
        {
            "determination",
            "workRate",
            "leadership",
            "flair",
            "bravery",
            "aggression",
            "concentration",
            "naturalFitness",
            "composure",
            "decisions",
        };

        [Test]
        public void IsAbsolute_AbsoluteStats_ReturnsTrue()
        {
            foreach (var stat in AllAbsolute)
                Assert.IsTrue(StatMetadata.IsAbsolute(stat), $"{stat} should be Absolute");
        }

        [Test]
        public void IsAbsolute_RelativeStats_ReturnsFalse()
        {
            Assert.IsFalse(StatMetadata.IsAbsolute("passing"));
            Assert.IsFalse(StatMetadata.IsAbsolute("tackling"));
            Assert.IsFalse(StatMetadata.IsAbsolute("pace"));
            Assert.IsFalse(StatMetadata.IsAbsolute("finishing"));
        }

        [Test]
        public void IsAbsolute_ExactlyTenAbsoluteStats()
        {
            Assert.AreEqual(10, AllAbsolute.Length);
        }

        // ── IsPhysical (V0.5 D.4 / design-decisions.md #53) ──

        private static readonly string[] AllPhysical =
        {
            "acceleration",
            "agility",
            "balance",
            "jumpingReach",
            "naturalFitness",
            "pace",
            "stamina",
            "strength",
        };

        [Test]
        public void IsPhysical_PhysicalStats_ReturnsTrue()
        {
            foreach (var stat in AllPhysical)
                Assert.IsTrue(StatMetadata.IsPhysical(stat), $"{stat} should be Physical");
        }

        [Test]
        public void IsPhysical_NonPhysicalStats_ReturnsFalse()
        {
            Assert.IsFalse(StatMetadata.IsPhysical("passing"));
            Assert.IsFalse(StatMetadata.IsPhysical("vision"));
            Assert.IsFalse(StatMetadata.IsPhysical("finishing"));
            Assert.IsFalse(StatMetadata.IsPhysical("handling"));
        }

        [Test]
        public void IsPhysical_ExactlyEightPhysicalStats()
        {
            Assert.AreEqual(8, AllPhysical.Length);
        }

        [Test]
        public void NaturalFitness_BothAbsoluteAndPhysical()
        {
            // naturalFitness 는 Absolute (인성) ∩ Physical (Gym 보정 대상).
            // GrowthSystem 에서 absoluteFactor=0.1 + gymBonus 둘 다 적용.
            Assert.IsTrue(StatMetadata.IsAbsolute("naturalFitness"));
            Assert.IsTrue(StatMetadata.IsPhysical("naturalFitness"));
        }
    }
}
