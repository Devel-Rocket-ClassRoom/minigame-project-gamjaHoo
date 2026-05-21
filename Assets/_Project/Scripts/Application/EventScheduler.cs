// EventScheduler.cs
// data-flows.md #2 [3] 오늘 발생할 이벤트 식별 + 분기.
// Stateless (design-decisions.md #3).
//
// V0.1 책임: 매치 분기 + 유스 인스펙션 트리거 + 시즌 종료/시작 트리거 (Stage 12).
// V1.0+: 보드 리뷰 / 이적창 자동 알림.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class EventScheduler
    {
        // 반환값: 정지 신호 (userClub 매치 / 유스 인스펙션) — 호출자 (GameLoop) 가 시간 진행 멈춤 결정.
        public static bool Run(GameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            bool stopRequested = false;
            var today = state.currentDate.Date;

            // ── 매치 분기 (Task 8.1) ─────────────────────────────────
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;

                var todaysMatches = league.schedule.Where(m => m.date.Date == today).ToList();

                if (todaysMatches.Count == 0)
                    continue;

                bool isUserMatch =
                    state.userClubId >= 0
                    && todaysMatches.Any(m =>
                        m.homeClubId == state.userClubId || m.awayClubId == state.userClubId
                    );

                EventBus.Publish(
                    new MatchDayEvent
                    {
                        matchIds = todaysMatches.Select(m => m.id).ToList(),
                        isUserMatch = isUserMatch,
                    }
                );

                if (isUserMatch)
                    stopRequested = true;
            }

            // ── 유스 인스펙션 트리거 (Task 10.1, #39) ────────────────
            // 6/15 메인 / 1/15 보조 (외부화). userClub 만 V0.1 (#35).
            if (TryTriggerYouthIntake(state, today))
                stopRequested = true;

            // ── 시즌 종료 / 신규 시즌 트리거 (Task 12.1/12.2, design-decisions.md #38) ──
            if (TryTriggerSeasonEnd(state, today))
                stopRequested = true;

            if (TryTriggerNewSeason(state, today))
                stopRequested = true;

            // TODO V1.0:    이적창 오픈/마감 / 보드 리뷰일

            return stopRequested;
        }

        private static bool TryTriggerSeasonEnd(GameState state, DateTime today)
        {
            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return false;
            if (today.Month != balance.seasonEndMonth || today.Day != balance.seasonEndDay)
                return false;
            SeasonEndProcessor.Run(state, balance);
            return true;
        }

        private static bool TryTriggerNewSeason(GameState state, DateTime today)
        {
            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return false;
            if (
                today.Month != balance.fiscalYearStartMonth
                || today.Day != balance.fiscalYearStartDay
            )
                return false;
            NewSeasonProcessor.Run(state, balance);
            return true;
        }

        private static bool TryTriggerYouthIntake(GameState state, DateTime today)
        {
            if (state.userClubId < 0)
                return false;

            var balance = GameDatabase.GameBalance;
            if (balance == null)
                return false; // GameDatabase 미초기화 (테스트 등) — 스킵

            bool isMainDay =
                today.Month == balance.youthIntakeMainMonth
                && today.Day == balance.youthIntakeMainDay;
            bool isSecondDay =
                today.Month == balance.youthIntakeSecondMonth
                && today.Day == balance.youthIntakeSecondDay;
            if (!isMainDay && !isSecondDay)
                return false;

            var userClub = state.GetClub(state.userClubId);
            if (userClub == null)
                return false;

            var league = state.leagues.FirstOrDefault(l =>
                l != null && l.clubIds != null && l.clubIds.Contains(userClub.id)
            );
            if (league == null)
                return false;

            var leagueConfig = GameDatabase.GetLeagueConfig(league.configSOId);
            if (leagueConfig == null)
                return false;

            YouthSystem.GenerateIntake(userClub, state, balance, leagueConfig);
            return true; // 정지 신호 — UI 유스 풀 화면
        }
    }
}
