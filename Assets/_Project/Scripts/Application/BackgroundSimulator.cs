// BackgroundSimulator.cs
// data-flows.md #2 [4] / #3 [6] — 라운드별 일괄 시뮬레이션.
// Stateless (design-decisions.md #3). GameLoop.AdvanceDay 가 EventScheduler 다음 호출.
//
// V0.1 정책 (옵션 A):
//   - isActiveSimulation 무시. 모든 매치 동일 알고리즘 (design-decisions.md #33, SimulateLite 폐기).
//   - MatchFinishedEvent 모든 매치 발행. UI 없으니 구독자 0 → EventBus.Publish 비용 ~0.
//
// V0.5+ 진화 (#34):
//   - publishEvent 옵션 도입 — UI 도입 후 유저 매치만 발행 / 비활성 매치 생략.
//   - 분 단위 이벤트 시뮬 도입 시 비활성 구단 경량 경로 (SimulateLite) 분리 검토.

using System;
using FMLite.Domain;
using UnityEngine;

namespace FMLite.Application
{
    public static class BackgroundSimulator
    {
        // state.currentDate 매치 일괄 처리. 이미 처리된 매치 (match.result != null) 는 스킵.
        public static void SimulateDay(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var today = state.currentDate.Date;

            for (int li = 0; li < state.leagues.Count; li++)
            {
                var league = state.leagues[li];
                if (league?.schedule == null)
                    continue;

                for (int mi = 0; mi < league.schedule.Count; mi++)
                {
                    var match = league.schedule[mi];
                    if (match == null)
                        continue;
                    if (match.date.Date != today)
                        continue;
                    if (match.result != null)
                        continue; // 이미 처리됨 (PostProcessor 가 throw 하기 전 필터)

                    // 데이터 정합성 방어 — 클럽 누락 시 스킵 (테스트 fixture 단순화 / 데이터 깨짐 방어).
                    // MatchSimulator 가 throw 하기 전에 graceful skip.
                    if (
                        state.GetClub(match.homeClubId) == null
                        || state.GetClub(match.awayClubId) == null
                    )
                    {
                        Debug.LogWarning(
                            $"[BackgroundSimulator] match.id={match.id} 클럽 누락 — 시뮬 스킵"
                        );
                        continue;
                    }

                    // 유저 클럽 매치는 이벤트 수집 (N.3 MatchTextScene 표시용)
                    bool isUserMatch =
                        match.homeClubId == state.userClubId
                        || match.awayClubId == state.userClubId;
                    var result = MatchSimulator.Simulate(
                        match,
                        state,
                        balance,
                        collectEvents: isUserMatch
                    );
                    MatchPostProcessor.Process(match, result, state, balance, publishEvent: false);
                    BoardSystem.ProcessMatchResult(state, balance, match, league);
                }
            }
        }
    }
}
