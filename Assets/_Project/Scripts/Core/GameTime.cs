// GameTime.cs
// 게임 내 날짜 진행. Advance(N) 호출 시 하루씩 N번 DayAdvancedEvent 발행.
// V0.1: 자체 상태 보유 — 추후 GameState.currentDate 와 양방향 동기화.
// 명세: docs/design-decisions.md #23, docs/event-bus-catalog.md

using System;

namespace FMLite.Core
{
    public static class GameTime
    {
        private static readonly DateTime DefaultStartDate = new DateTime(2024, 7, 1);

        public static DateTime CurrentDate { get; private set; } = DefaultStartDate;

        public static void Advance(int days)
        {
            if (days <= 0) return;
            for (int i = 0; i < days; i++)
            {
                CurrentDate = CurrentDate.AddDays(1);
                EventBus.Publish(new DayAdvancedEvent { newDate = CurrentDate });
            }
        }

        public static void Reset(DateTime startDate)
        {
            CurrentDate = startDate;
        }
    }
}
