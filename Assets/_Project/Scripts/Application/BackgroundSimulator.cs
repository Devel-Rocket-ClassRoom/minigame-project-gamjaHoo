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
using System.Linq;
using FMLite.Core;
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

            // R.5 (#76) — 매치데이 비유저 매치 배치 처리 전 유저 구단 순위 캡처 (역전 감지).
            int userPosBefore = LeaguePosition(state, state.userClubId);

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

                    // H.4: 유저 매치는 여기서 시뮬하지 않고 MatchPreviewScene "매치 시작" 시점에
                    // SimulateUserMatch 로 온디맨드 처리 (경기 직전 점검 게이트). 시드 결정성 유지.
                    bool isUserMatch =
                        match.homeClubId == state.userClubId
                        || match.awayClubId == state.userClubId;
                    if (isUserMatch)
                        continue;

                    var result = MatchSimulator.Simulate(
                        match,
                        state,
                        balance,
                        collectEvents: false
                    );
                    MatchPostProcessor.Process(match, result, state, balance, publishEvent: false);
                    BoardSystem.ProcessMatchResult(state, balance, match, league);
                }
            }

            // R.5 (#76) — 배치 처리 후 유저 구단 순위 변동 시 1회 발행.
            PublishStandingsChange(state, userPosBefore);
        }

        // H.4: 유저 매치 온디맨드 시뮬 (MatchPreviewScene "매치 시작"). SimulateDay 와 동일 후처리.
        // 이벤트 수집(collectEvents:true) — MatchTextScene 분 단위 표시용.
        public static void SimulateUserMatch(Match match, GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (match == null || match.result != null)
                return; // 이미 시뮬됨 / 없음
            if (state.GetClub(match.homeClubId) == null || state.GetClub(match.awayClubId) == null)
            {
                Debug.LogWarning(
                    $"[BackgroundSimulator] user match.id={match.id} 클럽 누락 — 시뮬 스킵"
                );
                return;
            }

            // R.5 (#76) — 유저 매치 처리 전 순위 캡처 → 후 변동 시 발행.
            int userPosBefore = LeaguePosition(state, state.userClubId);

            var result = MatchSimulator.Simulate(match, state, balance, collectEvents: true);
            MatchPostProcessor.Process(match, result, state, balance, publishEvent: false);
            var league = FindLeagueOf(state, match);
            if (league != null)
                BoardSystem.ProcessMatchResult(state, balance, match, league);

            PublishStandingsChange(state, userPosBefore);
        }

        // R.5 (#76) — 유저 구단 리그 1-based 순위 (승점 → 득실차 → 다득점). 미존재 시 -1.
        public static int LeaguePosition(GameState state, int clubId)
        {
            if (clubId < 0)
                return -1;
            var club = state?.GetClub(clubId);
            if (club == null)
                return -1;
            var league = state.leagues?.Find(l => l != null && l.id == club.leagueId);
            if (league?.standings?.entries == null)
                return -1;

            var sorted = league
                .standings.entries.OrderByDescending(e => e.points)
                .ThenByDescending(e => e.goalsFor - e.goalsAgainst)
                .ThenByDescending(e => e.goalsFor)
                .ToList();
            int idx = sorted.FindIndex(e => e.clubId == clubId);
            return idx < 0 ? -1 : idx + 1;
        }

        private static void PublishStandingsChange(GameState state, int before)
        {
            if (state.userClubId < 0)
                return;
            int after = LeaguePosition(state, state.userClubId);
            if (before > 0 && after > 0 && before != after)
                EventBus.Publish(
                    new StandingsChangedEvent
                    {
                        clubId = state.userClubId,
                        oldPosition = before,
                        newPosition = after,
                    }
                );
        }

        private static League FindLeagueOf(GameState state, Match match)
        {
            if (state?.leagues == null)
                return null;
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                    if (ReferenceEquals(m, match))
                        return league;
            }
            return null;
        }
    }
}
