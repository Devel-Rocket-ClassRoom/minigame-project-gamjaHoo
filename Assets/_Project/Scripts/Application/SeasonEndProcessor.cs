// SeasonEndProcessor.cs
// data-flows.md #6 시즌 종료 흐름 V0.1 구현.
// Stateless (design-decisions.md #3, #38).
//
// V0.1 책임:
//   - 계약 만료 → FA 전환 (currentClubId = -1, squad 제거)
//   - 33+ 확률적 은퇴 (GameState 제거)
//   - SeasonEndedEvent 발행
// V1.0 M.1: SaveCareerStats — 리그 매치 결과 집계 → Player.career.Add

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;
using Random = System.Random;

namespace FMLite.Application
{
    public static class SeasonEndProcessor
    {
        public static void Run(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            // a. 시즌 통계 career 저장 (V1.0 M.1) — 은퇴/계약만료 전에 실행
            SaveCareerStats(state);

            // a2. Match 데이터 압축 (V1.0 M.7) — career 저장 후 events/playerStats 비우기
            CompressMatchData(state);

            // b. 시즌 어워드 계산 (V1.0 M.2)
            SeasonAwardSystem.ComputeSeasonAwards(state, balance);

            // c. 재정 결산 (V1.0 M.6)
            FinanceSystem.ProcessSeasonFinance(state, balance);

            // d. 계약 만료 → FA 전환
            ProcessExpiredContracts(state);

            // e. 33+ 확률적 은퇴
            ProcessRetirements(state, balance);

            // f. SeasonEndedEvent 발행 (V0.1 단순 페이로드)
            int seasonYear = state.leagues.FirstOrDefault()?.seasonYear ?? 0;
            EventBus.Publish(new SeasonEndedEvent { seasonYear = seasonYear });
        }

        private static void ProcessExpiredContracts(GameState state)
        {
            var today = state.currentDate;
            foreach (var player in state.allPlayers)
            {
                if (player == null || player.contract == null)
                    continue;
                if (player.contract.endDate.Date > today.Date)
                    continue; // 만료 안 됨

                // 만료 — FA 전환
                int oldClubId = player.currentClubId;
                if (oldClubId >= 0)
                {
                    var club = state.GetClub(oldClubId);
                    club?.seniorSquadIds.Remove(player.id);
                    club?.youthSquadIds.Remove(player.id);
                }
                player.currentClubId = -1;
            }
        }

        private static void ProcessRetirements(GameState state, GameBalanceSO balance)
        {
            // 시드 결정성 — state.randomSeed ^ currentDate.Ticks
            int seed = state.randomSeed ^ unchecked((int)state.currentDate.Ticks);
            var rng = new Random(seed);

            // 은퇴 대상 ID 수집 (foreach 중 RemovePlayer 호출 회피)
            var toRetire = new List<int>();
            foreach (var player in state.allPlayers)
            {
                if (player == null)
                    continue;
                int age = GetAge(player, state.currentDate);
                if (age < balance.retirementMinAge)
                    continue;
                if (rng.NextDouble() < balance.retirementProbabilityPerYear)
                    toRetire.Add(player.id);
            }

            foreach (var id in toRetire)
            {
                var player = state.GetPlayer(id);
                if (player == null)
                    continue;
                // club squad 정리
                if (player.currentClubId >= 0)
                {
                    var club = state.GetClub(player.currentClubId);
                    club?.seniorSquadIds.Remove(id);
                    club?.youthSquadIds.Remove(id);
                }
                state.RemovePlayer(id);
            }
        }

        private static void SaveCareerStats(GameState state)
        {
            foreach (var league in state.leagues)
            {
                if (league == null)
                    continue;
                string competition =
                    GameDatabase.GetLeagueConfig(league.configSOId)?.displayName ?? "";
                int seasonYear = league.seasonYear;

                var acc = new Dictionary<int, LeagueStatAcc>();

                foreach (var match in league.schedule)
                {
                    if (match?.result == null)
                        continue;
                    var result = match.result;
                    foreach (var ps in result.playerStats)
                    {
                        if (!acc.TryGetValue(ps.playerId, out var entry))
                        {
                            int clubId = result.homeStarting11.Contains(ps.playerId)
                                ? match.homeClubId
                                : match.awayClubId;
                            entry = new LeagueStatAcc { clubId = clubId };
                            acc[ps.playerId] = entry;
                        }
                        entry.apps++;
                        entry.goals += ps.goals;
                        entry.assists += ps.assists;
                        entry.ratingSum += ps.rating;
                        entry.yellow += ps.yellowCards;
                        entry.red += ps.redCards;
                        entry.minutes += ps.minutesPlayed;
                    }
                }

                foreach (var kvp in acc)
                {
                    var player = state.GetPlayer(kvp.Key);
                    if (player == null)
                        continue;
                    var e = kvp.Value;
                    player.career.Add(
                        new SeasonStat
                        {
                            seasonYear = seasonYear,
                            clubId = e.clubId,
                            competition = competition,
                            appearances = e.apps,
                            goals = e.goals,
                            assists = e.assists,
                            averageRating = e.apps > 0 ? e.ratingSum / e.apps : 0f,
                            yellowCards = e.yellow,
                            redCards = e.red,
                            minutesPlayed = e.minutes,
                        }
                    );
                }
            }
        }

        private class LeagueStatAcc
        {
            public int clubId;
            public int apps;
            public int goals;
            public int assists;
            public float ratingSum;
            public int yellow;
            public int red;
            public int minutes;
        }

        private static void CompressMatchData(GameState state)
        {
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var match in league.schedule)
                {
                    if (match?.result == null)
                        continue;
                    match.result.events?.Clear();
                    match.result.playerStats?.Clear();
                }
            }
        }

        private static int GetAge(Player p, DateTime currentDate)
        {
            int age = currentDate.Year - p.info.birthDate.Year;
            if (currentDate.DayOfYear < p.info.birthDate.DayOfYear)
                age--;
            return age;
        }
    }
}
