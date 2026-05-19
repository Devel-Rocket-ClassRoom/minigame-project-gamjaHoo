// MatchEvents.cs
// 경기 관련 이벤트. event-bus-catalog.md "Match Events" 섹션.
// V0.1: MatchDayEvent (EventScheduler 발행)
// V1.0+: MatchFinishedEvent (Stage 9), PlayerInjuredEvent

using System.Collections.Generic;

namespace FMLite.Core
{
    public class MatchDayEvent
    {
        public List<int> matchIds;
        public bool isUserMatch;
    }
}
