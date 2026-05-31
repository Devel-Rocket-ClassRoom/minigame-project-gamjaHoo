// GameInitializer.cs
// data-flows.md #1 게임 시작 흐름 orchestrator (Task 7.1).
// ClubGen + ScheduleGen + Standings 초기화를 통합해 완전한 초기 GameState 생성.
// userClub 선정 / 초기 세이브는 호출자(UI) 책임 — V0.1 단순화.
//
// seasonStart = 프리시즌 시작일 (state.currentDate 초기값).
// 첫 매치는 GameBalanceSO.newSeasonOpening (예: 8/15) 부터.
// 프리시즌 기간 동안 사용자가 구단 선택 / 스쿼드 점검 가능 (FM 표준 흐름).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;
using Random = System.Random;

namespace FMLite.Application
{
    public static class GameInitializer
    {
        public static GameState NewGame(
            int seed,
            DateTime seasonStart,
            LeagueConfigSO leagueConfig,
            GameBalanceSO balance
        )
        {
            if (leagueConfig == null)
                throw new ArgumentNullException(nameof(leagueConfig));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var rng = new Random(seed);

            // (a) 빈 GameState 메타 설정
            var state = new GameState
            {
                randomSeed = seed,
                currentDate = seasonStart,
                rerollTokens = balance.initialRerollTokens,
                userClubId = -1, // UI 가 구단 선택 후 갱신
                nextPlayerId = 1,
            };

            // (b) League 인스턴스
            const int LeagueId = 1;
            var league = new League
            {
                id = LeagueId,
                configSOId = leagueConfig.id,
                seasonYear = seasonStart.Year,
                clubIds = new List<int>(),
                schedule = new List<Match>(),
                standings = new Standings { entries = new List<StandingEntry>() },
            };
            state.leagues.Add(league);

            // (c-d) ClubGen → state 에 일괄 등록 + nextPlayerId 갱신
            var clubGenResult = ClubGenerator.Generate(
                rng,
                leagueConfig,
                balance,
                seasonStart,
                leagueId: LeagueId,
                startClubId: 1,
                startPlayerId: state.nextPlayerId
            );

            foreach (var club in clubGenResult.Clubs)
                state.AddClub(club);
            foreach (var player in clubGenResult.Players)
                state.AddPlayer(player);

            league.clubIds = clubGenResult.Clubs.Select(c => c.id).ToList();
            if (clubGenResult.Players.Count > 0)
                state.nextPlayerId = clubGenResult.Players.Max(p => p.id) + 1;

            // (e) Schedule → League.schedule
            // 첫 매치일 = seasonStart 이후 가장 가까운 newSeasonOpening (프리시즌 분리).
            DateTime firstMatchDate = new DateTime(
                seasonStart.Year,
                balance.newSeasonOpeningMonth,
                balance.newSeasonOpeningDay
            );
            if (firstMatchDate < seasonStart)
                firstMatchDate = firstMatchDate.AddYears(1);

            league.schedule = ScheduleGenerator.Generate(
                league.clubIds,
                firstMatchDate,
                LeagueId,
                startMatchId: 1
            );

            // (e-2) Standings 초기화 (모든 구단 0:0:0:0)
            league.standings.entries = league
                .clubIds.Select(id => new StandingEntry { clubId = id })
                .ToList();

            // (f) 안전망 — AddClub/AddPlayer 가 이미 인덱스 동기화하므로 사실상 no-op
            state.BuildIndexes();

            // (g) 모든 구단 캡틴/부캡틴 자동 배정 (J.6)
            foreach (var club in state.allClubs)
                CaptainSystem.AssignAuto(club, state, balance);

            // (h) InboxRouter wire (V1.0 #66 / design-decisions.md). 멱등.
            InboxRouter.Wire(state);

            return state;
        }
    }
}
