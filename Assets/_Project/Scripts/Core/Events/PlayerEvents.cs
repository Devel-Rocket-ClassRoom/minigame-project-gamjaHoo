// PlayerEvents.cs
// V1.0 D.4 신규 이벤트.
// algorithms.md V1.0-10 (PlayerStatChangedEvent) + V1.0-11 (PlayerInjuryRecoveredEvent).

namespace FMLite.Core
{
    // 매월 1일 GrowthSystem.Tick 후 stat 변동 시 발행 (큰 점프 = +2 / +3 시점만 발행, +1 은 노이즈 회피).
    public class PlayerStatChangedEvent
    {
        public int playerId;
        public string statName;
        public int oldValue;
        public int newValue;
    }

    // DailyProcessor 의 InjurySystem.ProcessRecovery 가 expectedReturn 도래 시 발행.
    public class PlayerInjuryRecoveredEvent
    {
        public int playerId;
    }
}
