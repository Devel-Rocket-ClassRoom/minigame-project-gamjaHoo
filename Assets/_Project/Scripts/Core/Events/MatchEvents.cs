// MatchEvents.cs
// 경기 관련 이벤트. event-bus-catalog.md "Match Events" 섹션.
// V0.1: MatchDayEvent (EventScheduler 발행, Task 8.1)
//       MatchFinishedEvent (MatchPostProcessor 발행, Task 9.2)
// V1.0 I.2: PlayerInjuredEvent (MatchSimulator 가 매 분 step 에서 부상 발생 시 발행)

using System.Collections.Generic;
using FMLite.Domain;

namespace FMLite.Core
{
    public class MatchDayEvent
    {
        public List<int> matchIds;
        public bool isUserMatch;
    }

    public class MatchFinishedEvent
    {
        public int matchId;
        public MatchResult result;
    }

    public class PlayerInjuredEvent
    {
        public int playerId;
        public InjuryInfo injury;
    }
}
