// MatchResult.cs
// 경기 결과 + 선수별 경기 스탯.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class MatchResult
    {
        public int homeScore;
        public int awayScore;
        public List<int> homeStarting11 = new List<int>();
        public List<int> awayStarting11 = new List<int>();
        public List<PlayerMatchStat> playerStats = new List<PlayerMatchStat>();
    }

    [Serializable]
    public class PlayerMatchStat
    {
        public int playerId;
        public int minutesPlayed;
        public int goals;
        public int assists;
        public float rating;
        public int yellowCards;
        public int redCards;
    }
}
