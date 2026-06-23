// LeaderboardRepository.cs
// 명예의 전당 — leaderboard 읽기/쓰기/실시간 어댑터 (Persistence Layer, I/O).
// FM-Lite 전용 얇은 어댑터. DB 접근은 재사용 레이어 FirebaseKit(RealtimeDatabaseService)에 위임.
//   SubmitEntryAsync → leaderboard/{uid} 덮어쓰기 (1인 1행)
//   LoadTopAsync     → OrderByChild("score").LimitToLast(N) (서버 정렬) → 클라 내림차순 재정렬
//   StartListener    → 쿼리 실시간 구독 (상위 N 변경만 통지)
//
// ⚠️ StartListener 콜백은 메인스레드 보장이 없다. UI 호출측에서 메인스레드로 마샬링할 것.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirebaseKit;

namespace FMLite.Persistence.Cloud
{
    public static class LeaderboardRepository
    {
        public static Task SubmitEntryAsync(string nickname, string clubName, int score)
        {
            string uid = RequireUid();
            var entry = new LeaderboardEntry
            {
                nickname = nickname,
                clubName = clubName,
                score = score,
            };
            return RealtimeDatabaseService.SetAsync($"leaderboard/{uid}", entry);
        }

        // 상위 N명. 서버는 오름차순으로 돌려주므로 점수 내림차순 재정렬.
        public static async Task<List<LeaderboardEntry>> LoadTopAsync(int limit)
        {
            RequireUid();
            var rows = await RealtimeDatabaseService.QueryListAsync<LeaderboardEntry>(
                "leaderboard",
                "score",
                limit
            );
            return ToSortedEntries(rows);
        }

        private static IDisposable _subscription;

        public static void StartListener(int limit, Action<List<LeaderboardEntry>> onChanged)
        {
            StopListener();
            RequireUid();
            _subscription = RealtimeDatabaseService.SubscribeList<LeaderboardEntry>(
                "leaderboard",
                "score",
                limit,
                rows => onChanged?.Invoke(ToSortedEntries(rows))
            );
        }

        public static void StopListener()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        // 노드 키(uid)를 entry 에 채우고 점수 내림차순 정렬(1위 먼저).
        private static List<LeaderboardEntry> ToSortedEntries(List<DbEntry<LeaderboardEntry>> rows)
        {
            var list = new List<LeaderboardEntry>(rows.Count);
            foreach (var row in rows)
            {
                row.Value.uid = row.Key;
                list.Add(row.Value);
            }
            list.Sort((a, b) => b.score.CompareTo(a.score));
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
