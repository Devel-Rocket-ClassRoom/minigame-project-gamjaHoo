// CareerRepository.cs
// 명예의 전당 — careers/{uid} 읽기/쓰기 어댑터 (Persistence Layer, I/O).
// FM-Lite 전용 얇은 어댑터. DB 접근은 재사용 레이어 FirebaseKit(RealtimeDatabaseService)에 위임.
//   RecordSeasonAsync      → seasons/{key} push + score 를 멀티패스로 원자적 갱신, timestamp=ServerTimestamp
//   LoadRecentSeasonsAsync → OrderByChild("timestamp").LimitToLast(N) → Reverse (최신순)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirebaseKit;

namespace FMLite.Persistence.Cloud
{
    public static class CareerRepository
    {
        // 한 시즌 기록을 Push 하고, 누적 점수(score)를 같은 호출로 원자적 갱신.
        public static Task RecordSeasonAsync(
            string clubName,
            int year,
            int position,
            int points,
            int totalScore
        )
        {
            string uid = RequireUid();
            string seasonKey = RealtimeDatabaseService.GeneratePushKey($"careers/{uid}/seasons");

            var seasonData = new Dictionary<string, object>
            {
                { "year", year },
                { "clubName", clubName },
                { "position", position },
                { "points", points },
                { "timestamp", RealtimeDatabaseService.ServerTimestamp },
            };

            // careers/{uid} 기준 멀티패스: seasons/{key} 전체 + score 를 atomic 갱신.
            var updates = new Dictionary<string, object>
            {
                { $"seasons/{seasonKey}", seasonData },
                { "score", totalScore },
            };
            return RealtimeDatabaseService.UpdateChildrenAsync($"careers/{uid}", updates);
        }

        // 최근 N개 시즌(최신순). timestamp 큰 N개를 받아 Reverse.
        public static async Task<List<SeasonRecord>> LoadRecentSeasonsAsync(int limit)
        {
            string uid = RequireUid();
            var rows = await RealtimeDatabaseService.QueryListAsync<SeasonRecord>(
                $"careers/{uid}/seasons",
                "timestamp",
                limit
            );

            var list = new List<SeasonRecord>(rows.Count);
            foreach (var row in rows)
                list.Add(row.Value);
            list.Reverse(); // 오름차순 도착 → 최신순
            return list;
        }

        private static string RequireUid()
        {
            if (!RealtimeDatabaseService.IsReady)
                throw new InvalidOperationException("Firebase 가 아직 준비되지 않았습니다.");
            string uid = AuthManager.Uid;
            if (string.IsNullOrEmpty(uid))
                throw new InvalidOperationException("로그인된 사용자가 없습니다.");
            return uid;
        }
    }
}
