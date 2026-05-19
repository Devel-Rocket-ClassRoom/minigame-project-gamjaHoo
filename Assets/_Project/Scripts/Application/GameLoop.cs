// GameLoop.cs
// data-flows.md #2 시간 진행 통합 (Task 8.3 / Stage 8 마무리).
// Stateless 시스템 (design-decisions.md #3, #29 보완).
//
// 위치 근거 (decisions #29): GameManager (Core) 는 State 보유 + 진입점 역할만.
// 흐름 조율은 Application 의 GameLoop 가 담당 — Core → Application 순환
// 의존 회피.
//
// 호출 흐름 (data-flows.md #2):
//   GameTime.Advance(1)              ← Core 정적 호출 (DayAdvancedEvent 발행)
//   state.currentDate 동기화          ← decisions #23 양방향 동기화
//   DailyProcessor.Run(state, balance) ← Application
//   EventScheduler.Run(state)         ← Application (정지 신호 반환)
//   stopRequested 면 ContinueUntilStop 루프 break

using System;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class GameLoop
    {
        // 단일 일 진행. 호출자가 stopRequested 확인 후 다음 행동 결정.
        public static AdvanceDayResult AdvanceDay(GameState state, GameBalanceSO balance)
        {
            if (state == null)   throw new ArgumentNullException(nameof(state));
            if (balance == null) throw new ArgumentNullException(nameof(balance));

            GameTime.Advance(1);
            state.currentDate = GameTime.CurrentDate;     // decisions #23 동기화

            DailyProcessor.Run(state, balance);
            bool stopRequested = EventScheduler.Run(state);

            return new AdvanceDayResult { stopRequested = stopRequested, daysAdvanced = 1 };
        }

        // 정지 이벤트까지 자동 진행. UI Continue 버튼이 호출.
        // maxDays: 무한 루프 안전 가드 (V0.1 단순화 — 1년치).
        public static AdvanceDayResult ContinueUntilStop(
            GameState state, GameBalanceSO balance, int maxDays = 365)
        {
            if (state == null)   throw new ArgumentNullException(nameof(state));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (maxDays < 1)     throw new ArgumentOutOfRangeException(nameof(maxDays));

            int daysAdvanced = 0;
            bool stopRequested = false;
            for (int i = 0; i < maxDays; i++)
            {
                var r = AdvanceDay(state, balance);
                daysAdvanced += r.daysAdvanced;
                if (r.stopRequested)
                {
                    stopRequested = true;
                    break;
                }
            }
            return new AdvanceDayResult { stopRequested = stopRequested, daysAdvanced = daysAdvanced };
        }
    }

    public class AdvanceDayResult
    {
        public bool stopRequested;
        public int  daysAdvanced;
    }
}
