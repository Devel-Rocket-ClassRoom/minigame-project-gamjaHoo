using System.Collections.Generic;

namespace FMLite.Utils
{
    // design-decisions.md #40 (Absolute/Relative) + #53 (Gym 피지컬 보정)
    // algorithms.md V0.5-10 GrowthSystem 가 IsAbsolute / IsPhysical 분기 활용.
    public static class StatMetadata
    {
        private static readonly HashSet<string> AbsoluteStats = new HashSet<string>
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

        private static readonly HashSet<string> PhysicalStats = new HashSet<string>
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

        public static bool IsAbsolute(string statName) => AbsoluteStats.Contains(statName);

        public static bool IsPhysical(string statName) => PhysicalStats.Contains(statName);
    }
}
