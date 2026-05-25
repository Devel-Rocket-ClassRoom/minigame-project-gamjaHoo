using System.Collections.Generic;

namespace FMLite.Utils
{
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

        public static bool IsAbsolute(string statName) => AbsoluteStats.Contains(statName);
    }
}
