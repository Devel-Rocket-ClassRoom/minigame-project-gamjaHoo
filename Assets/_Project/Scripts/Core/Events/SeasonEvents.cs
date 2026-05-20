// SeasonEvents.cs
// 시즌 사이클 이벤트. event-bus-catalog.md "Season Events".
// Stage 12 Task 12.1 / 12.2 (#44 / #45) 에서 발행.
// V0.1: SeasonEndedEvent / SeasonStartedEvent.
// V1.0+: BoardReviewEvent (보드 시스템 도입 시).

namespace FMLite.Core
{
    public class SeasonEndedEvent
    {
        public int seasonYear;
        // V1.0+: SeasonSummary 페이로드 (시상/순위/재정 요약). V0.1 단순.
    }

    public class SeasonStartedEvent
    {
        public int seasonYear;
        // V1.0+: SeasonState target 페이로드. V0.1 단순.
    }
}
