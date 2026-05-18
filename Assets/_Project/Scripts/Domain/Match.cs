// Match.cs
// 경기 도메인 엔티티. class-diagram.md 명세 기준.
// MatchEvent 는 V0.1 placeholder — V1.0 텍스트 이벤트 시스템에서 본격 필드 추가.

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

    [Serializable]
    public class MatchEvent
    {
        // V0.1: placeholder (필드 없음).
        // V1.0: minute, type(Goal/Card/Sub/...), playerId, descriptionKey 등 추가 예정.
    }
}
