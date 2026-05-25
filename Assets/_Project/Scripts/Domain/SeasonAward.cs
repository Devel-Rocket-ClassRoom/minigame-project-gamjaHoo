// SeasonAward.cs
// 시즌 시상 기록 (design-decisions.md #51).

using System;

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
    }

    [Serializable]
    public class SeasonAward
    {
        public AwardType type;
        public int playerId; // -1 = ManagerOfSeason (선수 없음)
        public int seasonYear;
    }
}
