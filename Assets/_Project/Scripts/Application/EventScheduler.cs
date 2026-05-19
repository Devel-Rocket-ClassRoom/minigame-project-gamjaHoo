// EventScheduler.cs
// data-flows.md #2 [3] 오늘 발생할 이벤트 식별 + 분기.
// Stateless (design-decisions.md #3).
//
// V0.1 책임: 매치 분기만. 유스 인스펙션 / 시즌 종료 / 보드 리뷰 / 이적창은
// 해당 Stage 작업 시 본 메서드 확장.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class EventScheduler
    {
        // 반환값: 정지 신호 (userClub 매치 등) — 호출자 (GameLoop) 가 시간 진행 멈춤 결정.
        public static bool Run(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            bool stopRequested = false;
            var  today         = state.currentDate.Date;

            foreach (var league in state.leagues)
            {
                if (league?.schedule == null) continue;

                // 오늘 날짜 매치 식별
                var todaysMatches = league.schedule
                    .Where(m => m.date.Date == today)
                    .ToList();

                if (todaysMatches.Count == 0) continue;

                bool isUserMatch = state.userClubId >= 0 && todaysMatches.Any(m =>
                    m.homeClubId == state.userClubId || m.awayClubId == state.userClubId);

                EventBus.Publish(new MatchDayEvent
                {
                    matchIds    = todaysMatches.Select(m => m.id).ToList(),
                    isUserMatch = isUserMatch,
                });

                if (isUserMatch) stopRequested = true;
            }

            // TODO Stage 10: 유스 인스펙션일 (6월 / 1월 중순) → YouthIntakeAvailableEvent
            // TODO Stage 12: 시즌 종료일 → SeasonEndedEvent
            // TODO V1.0:    이적창 오픈/마감 / 보드 리뷰일

            return stopRequested;
        }
    }
}
