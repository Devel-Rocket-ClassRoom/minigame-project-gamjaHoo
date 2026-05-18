// Standings.cs
// 리그 순위표 + 구단별 엔트리.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class Standings
    {
        public List<StandingEntry> entries = new List<StandingEntry>();
    }

    [Serializable]
    public class StandingEntry
    {
        public int clubId;
        public int played;
        public int won;
        public int drawn;
        public int lost;
        public int goalsFor;
        public int goalsAgainst;
        public int points;
    }
}
