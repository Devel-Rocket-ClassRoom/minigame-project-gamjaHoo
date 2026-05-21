// ScheduleGenerator.cs
// 리그 더블 라운드 로빈 일정 생성 (Task 7.2).
// 표준 Circle method (Berger tables 변형): 한 팀 고정 + 나머지 회전.
// n팀 (짝수) → (n-1) 라운드 × n/2 경기 × 2(홈/원정) = n(n-1) 경기.
// 20팀 기준 38라운드 × 10경기 = 380경기.

using System;
using System.Collections.Generic;
using FMLite.Domain;
using UnityEngine;

namespace FMLite.Application
{
    public static class ScheduleGenerator
    {
        public static List<Match> Generate(
            List<int> clubIds,
            DateTime seasonStart,
            int leagueId,
            int startMatchId,
            int roundIntervalDays = 7
        )
        {
            if (clubIds == null || clubIds.Count < 2)
            {
                Debug.LogWarning(
                    $"[ScheduleGenerator] clubIds.Count = {clubIds?.Count ?? 0} → 빈 일정 반환"
                );
                return new List<Match>();
            }
            if (clubIds.Count % 2 != 0)
                throw new ArgumentException(
                    $"ScheduleGenerator: clubIds.Count 는 짝수여야 함 (현재 {clubIds.Count}). "
                        + "홀수 팀은 'bye' 처리가 필요한데 V0.1 스코프 외."
                );

            int n = clubIds.Count;
            int rounds = n - 1; // 1차 라운드 수 = n-1
            var matches = new List<Match>();
            int nextId = startMatchId;

            // 1차 라운드 (n-1 라운드, 각 라운드 n/2 경기)
            for (int r = 0; r < rounds; r++)
            {
                DateTime roundDate = seasonStart.AddDays(r * roundIntervalDays);
                foreach (var (homeId, awayId) in GeneratePairs(clubIds, r))
                    matches.Add(BuildMatch(nextId++, roundDate, homeId, awayId));
            }

            // 2차 라운드 (1차 반전 — 홈/원정 스왑)
            for (int r = 0; r < rounds; r++)
            {
                DateTime roundDate = seasonStart.AddDays((rounds + r) * roundIntervalDays);
                foreach (var (homeId, awayId) in GeneratePairs(clubIds, r))
                    matches.Add(BuildMatch(nextId++, roundDate, awayId, homeId)); // 반전
            }

            return matches;
        }

        private static Match BuildMatch(int id, DateTime date, int homeId, int awayId) =>
            new Match
            {
                id = id,
                date = date,
                type = CompetitionType.League,
                homeClubId = homeId,
                awayClubId = awayId,
                events = new List<MatchEvent>(),
                // result 는 시뮬 전까지 default (null)
            };

        // Circle method 한 라운드 페어 생성.
        // clubIds[0] 고정, clubIds[1..n-1] 회전. 홈/원정은 alternation.
        private static List<(int home, int away)> GeneratePairs(List<int> clubIds, int round)
        {
            int n = clubIds.Count;
            int rotateLen = n - 1;
            int fixedId = clubIds[0];
            int rotatedIdx = round % rotateLen;
            int rotatedId = clubIds[1 + rotatedIdx];

            var pairs = new List<(int, int)>(n / 2);

            // 첫 페어 (고정팀 vs 회전 위치 팀): 라운드 짝수면 고정 홈, 홀수면 회전 홈.
            pairs.Add(round % 2 == 0 ? (fixedId, rotatedId) : (rotatedId, fixedId));

            // 나머지 페어들 (회전 그룹 내부 매칭): i 짝수/홀수로 홈/원정 alternation
            for (int i = 1; i < n / 2; i++)
            {
                int idxA = (rotatedIdx + i) % rotateLen;
                int idxB = (rotatedIdx - i + rotateLen) % rotateLen;
                int teamA = clubIds[1 + idxA];
                int teamB = clubIds[1 + idxB];
                pairs.Add(i % 2 == 0 ? (teamA, teamB) : (teamB, teamA));
            }

            return pairs;
        }
    }
}
