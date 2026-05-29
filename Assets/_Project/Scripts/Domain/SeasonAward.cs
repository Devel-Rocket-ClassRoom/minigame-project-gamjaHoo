// SeasonAward.cs
// 시즌 시상 기록 (design-decisions.md #51).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    public enum AwardType
    {
        LeagueMVP,
        TopScorer,
        TopAssist,
        YoungPlayer,
        BestEleven,
        GoldenGlove,
        ManagerOfSeason,
        MonthlyManagerOfMonth, // V0.5 M.3
        MonthlyPlayerOfMonth, // V0.5 M.3
    }

    [Serializable]
    public class SeasonAward
    {
        public AwardType type;
        public List<int> playerIds = new List<int>(); // BestEleven=11명, 나머지=1명, ManagerOfSeason=0
        public int seasonYear;
        public int leagueId;
        public DateTime awardedAt;
    }
}
