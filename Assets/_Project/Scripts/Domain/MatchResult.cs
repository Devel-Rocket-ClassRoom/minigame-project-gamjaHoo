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

        // V1.0 5-zone (I.2') — 점유율 % (possessionTicks 기반). 활성/비활성 매치 모두 채움 (#55).
        public float homePossessionPct;
        public float awayPossessionPct;

        // V1.0 I.5 — collectEvents=true 시 채워짐. 배경 매치는 빈 리스트.
        public List<MatchEvent> events = new List<MatchEvent>();
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

        // V1.0 확장 (design-decisions.md #44 / algorithms.md V1.0-2)
        public int shots;
        public int shotsOnTarget; // I.2 — 정확 슈팅 (finishing × composure 기반)
        public int passes;
        public int passesCompleted; // I.2 — 성공 패스 (passing / 100 기반)
        public int tackles;
        public int interceptions;
        public int keyPasses;
        public int foulsCommitted;
        public int foulsSuffered;
        public int saves; // GK 선방 (I.4 평점)
    }
}
