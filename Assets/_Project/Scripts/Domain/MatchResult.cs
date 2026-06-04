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

        // V0.5 5-zone (I.2') — 점유율 % (possessionTicks 기반). 활성/비활성 매치 모두 채움 (#55).
        public float homePossessionPct;
        public float awayPossessionPct;

        // V0.5 I.5 — collectEvents=true 시 채워짐. 배경 매치는 빈 리스트.
        public List<MatchEvent> events = new List<MatchEvent>();

        // V0.5 I.11 — 승부차기 결착 시 채워짐. 아니면 둘 다 0.
        public int penaltyHomeScore;
        public int penaltyAwayScore;
        public bool decidedByPenalties;

        // V1.0 xG (algorithms.md V1.0-1, #70) — 슛별 xG/위치/결과. AA.5 슛맵 선당김.
        public List<ShotPin> shotMap = new List<ShotPin>();

        // V1.0 (AA.2/AA.4 선당김) — ballZone 점유 누적 [HomeBox..AwayBox]. 히트맵.
        public int[] zoneOccupancy = new int[5];
    }

    // V1.0 — 슈팅 결과 (슛맵 시각화 + 평점).
    public enum ShotOutcome
    {
        Goal,
        Saved, // on target, GK 선방
        Off, // off target / blocked
    }

    [Serializable]
    public class ShotPin
    {
        public int side; // 0 = Home, 1 = Away
        public float x; // 0~1 (피치 길이 방향, 공격 골대 = 1)
        public float y; // 0~1 (피치 폭 방향, 중앙 = 0.5)
        public float xg; // 찬스 품질 (situation, 슈터 무관)
        public ShotOutcome outcome;
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

        // V0.5 확장 (design-decisions.md #44 / algorithms.md V0.5-2)
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

        // V1.0 xG + 평점 재설계 (algorithms.md V1.0-1, #70)
        public float xg; // 누적 xG (본인 슛들의 situation quality 합)
        public int bigChancesMissed; // xG ≥ bigChanceThreshold 인데 미득점
        public int clearances; // 수비 클리어런스 (평점 수비 기여)
    }
}
