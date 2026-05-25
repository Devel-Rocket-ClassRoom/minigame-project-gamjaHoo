// SeasonHistory.cs
// 완료된 시즌의 구단 기록 스냅샷.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class SeasonHistory
    {
        public int seasonYear;
        public int leaguePosition;
        public int points;
        public int goalsFor;
        public int goalsAgainst;
        public List<SeasonAward> awards = new List<SeasonAward>();
    }
}
