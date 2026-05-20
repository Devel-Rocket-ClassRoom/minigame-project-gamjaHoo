// SeasonEndProcessor.cs
// data-flows.md #6 시즌 종료 흐름 V0.1 구현.
// Stateless (design-decisions.md #3, #38).
//
// V0.1 책임:
//   - 계약 만료 → FA 전환 (currentClubId = -1, squad 제거)
//   - 33+ 확률적 은퇴 (GameState 제거)
//   - SeasonEndedEvent 발행
// V1.0+ 미구현: 시상 / 보드 평가 / 재정 결산 / 사기 정산 / Match 압축 / 갱신 협상.

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

            // a. 계약 만료 → FA 전환
            ProcessExpiredContracts(state);

            // b. 33+ 확률적 은퇴
            ProcessRetirements(state, balance);

            // c. SeasonEndedEvent 발행 (V0.1 단순 페이로드)
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

        private static int GetAge(Player p, DateTime currentDate)
        {
            int age = currentDate.Year - p.info.birthDate.Year;
            if (currentDate.DayOfYear < p.info.birthDate.DayOfYear)
                age--;
            return age;
        }
    }
}
