// NewSeasonProcessor.cs
// data-flows.md #6 신규 시즌 시작 흐름 V0.1 구현.
// Stateless (design-decisions.md #3, #38).
//
// V0.1 책임:
//   - 리롤 토큰 지급 (max 적용)
//   - 모든 선수 fatigue/form 리셋
//   - League.standings 초기화
//   - 새 시즌 매치 일정 생성 (ScheduleGenerator)
//   - 클럽 season 갱신 (targetLeaguePosition / boardConfidence)
//   - SeasonStartedEvent 발행
// V0.5+ 미구현: 재정 결산 (이적/임금 예산 재배정) / 보드 목표 동적화.

using System;
using System.Collections.Generic;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class NewSeasonProcessor
    {
        public static void Run(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            // a. 리롤 토큰 지급 (max 적용)
            state.rerollTokens += balance.seasonRerollTokenGrant;
            if (state.rerollTokens > balance.maxRerollStockpile)
                state.rerollTokens = balance.maxRerollStockpile;

            // b. 모든 선수 fatigue / form / seasonAppearances / 카드 정지 리셋
            // (seasonAppearances V0.5 G.2 — PlaytimeAgreement Promise 평가 시즌 단위)
            // (seasonYellowCards / suspendedMatches V0.5 I.3 — 시즌 넘어가면 카드 누적 / 정지 해제)
            foreach (var player in state.allPlayers)
            {
                if (player?.state == null)
                    continue;
                player.state.fatigue = 0;
                player.state.form = 50;
                player.state.seasonAppearances = 0;
                player.state.seasonYellowCards = 0;
                player.state.suspendedMatches = 0;
            }

            // c. 새 시즌 매치 일정 + Standings 초기화
            int nextMatchId = ComputeNextMatchId(state);
            DateTime openingDate = ComputeNewSeasonOpeningDate(state.currentDate, balance);

            foreach (var league in state.leagues)
            {
                if (league == null)
                    continue;
                league.seasonYear += 1;

                // Standings 초기화 (clubIds 기반 재구성)
                league.standings = new Standings
                {
                    entries = new List<StandingEntry>(league.clubIds?.Count ?? 0),
                };
                if (league.clubIds != null)
                {
                    foreach (var clubId in league.clubIds)
                        league.standings.entries.Add(new StandingEntry { clubId = clubId });
                }

                // 새 매치 일정
                if (league.clubIds != null && league.clubIds.Count >= 2)
                {
                    var newSchedule = ScheduleGenerator.Generate(
                        league.clubIds,
                        openingDate,
                        league.id,
                        nextMatchId
                    );
                    league.schedule = newSchedule;
                    nextMatchId += newSchedule.Count;
                }
                else
                {
                    league.schedule = new List<Match>();
                }
            }

            // d. 클럽별 season 갱신 (V0.1 단순 — 명성 순위 = 목표, design-decisions.md #27 패턴)
            UpdateClubSeasonTargets(state, balance);

            // e. 보드 약속 생성 (M.5 — 유저 구단만)
            BoardSystem.GenerateSeasonPromises(state, balance);

            // f. SeasonStartedEvent 발행
            int seasonYear = state.leagues.Count > 0 ? state.leagues[0].seasonYear : 0;
            EventBus.Publish(new SeasonStartedEvent { seasonYear = seasonYear });
        }

        private static DateTime ComputeNewSeasonOpeningDate(
            DateTime currentDate,
            GameBalanceSO balance
        )
        {
            // currentDate = 6/1 — 같은 해 8/15 가 매치 개막
            int year = currentDate.Year;
            return new DateTime(year, balance.newSeasonOpeningMonth, balance.newSeasonOpeningDay);
        }

        private static int ComputeNextMatchId(GameState state)
        {
            int max = 0;
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                    if (m != null && m.id > max)
                        max = m.id;
            }
            return max + 1;
        }

        private static void UpdateClubSeasonTargets(GameState state, GameBalanceSO balance)
        {
            // 명성 내림차순 정렬 후 순위 = 목표 (design-decisions.md #27 / ClubGen 패턴)
            var clubsByRep = new List<Club>(state.allClubs);
            clubsByRep.Sort((a, b) => b.reputation.CompareTo(a.reputation));

            for (int i = 0; i < clubsByRep.Count; i++)
            {
                var c = clubsByRep[i];
                if (c.season == null)
                    c.season = new SeasonState();
                c.season.targetLeaguePosition = i + 1;
                c.season.cupTarget = CupTarget.None; // V0.1 컵 미구현
                c.season.boardConfidence = balance.initialBoardConfidence;
            }
        }
    }
}
