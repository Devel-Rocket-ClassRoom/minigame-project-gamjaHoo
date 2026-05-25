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
    }
}
