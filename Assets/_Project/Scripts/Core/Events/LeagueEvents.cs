// LeagueEvents.cs
// 리그 관련 이벤트. event-bus-catalog.md "V1.0 플레이테스트 인박스 확장 이벤트".
// V1.0 R.5 (#76): StandingsChangedEvent.

namespace FMLite.Core
{
    // V1.0 R.5 (#76) — BackgroundSimulator 가 매치데이 처리 후 유저 구단 리그 순위가 실제로 변동한 경우 발행.
    // 빈번 발행 회피: 유저 구단 한정 + 매치데이당 최대 1~2회(비유저 매치 배치 / 유저 매치) + 실제 변동 시만.
    public class StandingsChangedEvent
    {
        public int clubId;
        public int oldPosition;
        public int newPosition;
    }
}
