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
        public int appearances;
        public int goals;
        public int assists;
        public float averageRating;
    }
}
