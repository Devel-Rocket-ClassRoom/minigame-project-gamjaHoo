// Match.cs
// 경기 도메인 엔티티. class-diagram.md 명세 기준.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class Match
    {
        public int id;
        public DateTime date;
        public CompetitionType type;
        public int homeClubId;
        public int awayClubId;
        public MatchResult result;
        public List<MatchEvent> events = new List<MatchEvent>();
    }

    public enum MatchEventType
    {
        KickOff,
        HalfTime,
        FullTime,
        PassCompleted,
        Dribble,
        Cross,
        ShotOnTarget,
        ShotOffTarget,
        ShotBlocked,
        ShotSaved,
        Goal,
        PenaltyAwarded,
        PenaltyGoal,
        PenaltyMiss,
        Tackle,
        Interception,
        Clearance,
        Foul,
        YellowCard,
        RedCard,
        SecondYellow,
        Corner,
        FreeKick,
        Injury,
        Substitution,
    }

    [Serializable]
    public class MatchEvent
    {
        public int minute;
        public MatchEventType type;
        public int side; // 0 = Home, 1 = Away
        public int actorPlayerId; // 주체 (슈터/파울러 등). 0 = 없음.
        public int targetPlayerId; // 보조 (어시스트/파울 대상). 0 = 없음.
        public string textKey; // LocalizationSO 키
        public Dictionary<string, string> textArgs; // textKey 포맷 인수
    }
}
