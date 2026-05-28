// SeasonStat.cs
// 선수 시즌별 누적 스탯 (이력용).

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class SeasonStat
    {
        public int seasonYear;
        public int clubId;
        public string competition; // 리그 이름 (leagueConfig.displayName)
        public int appearances;
        public int goals;
        public int assists;
        public float averageRating;
        public int yellowCards;
        public int redCards;
        public int minutesPlayed;
    }
}
