// LeaderboardRepository.cs
// 명예의 전당 — leaderboard 읽기/쓰기/실시간 어댑터 (Persistence Layer, I/O).
//
// 08장 매핑:
//   SubmitEntryAsync → leaderboard/{uid} SetRawJsonValue (1인 1행, 덮어쓰기)
//   LoadTopAsync     → OrderByChild("score").LimitToLast(N) (서버 정렬) → 클라 내림차순 재정렬
//   StartListener    → Query.ValueChanged (상위 N 변경만 통지)
//
// ⚠️ ValueChanged 콜백은 메인스레드 보장이 없다. UI 를 만지는 호출측에서
//    메인스레드로 마샬링해야 한다 (Persistence 는 raw 콜백만 전달).

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using FMLite.Core;
using UnityEngine;

namespace FMLite.Persistence.Cloud
{
    public static class LeaderboardRepository
    {
        private static DatabaseReference LeaderboardRef =>
            FirebaseBootstrap.Instance.Database.RootReference.Child("leaderboard");

        public static async Task SubmitEntryAsync(string nickname, string clubName, int score)
        {
            string uid = RequireUid();
            var entry = new LeaderboardEntry
            {
                nickname = nickname,
                clubName = clubName,
                score = score,
            };
            string json = JsonUtility.ToJson(entry);
            await LeaderboardRef.Child(uid).SetRawJsonValueAsync(json);
        }

        // 상위 N명. LimitToLast 는 "값이 큰 N개"를 오름차순으로 돌려주므로 내림차순 재정렬.
        public static async Task<List<LeaderboardEntry>> LoadTopAsync(int limit)
        {
            RequireUid();
            Query query = LeaderboardRef.OrderByChild("score").LimitToLast(limit);
            DataSnapshot snapshot = await query.GetValueAsync();
            return ParseEntries(snapshot);
        }

        private static Query _listenerQuery;
        private static EventHandler<ValueChangedEventArgs> _handler;

        public static void StartListener(int limit, Action<List<LeaderboardEntry>> onChanged)
        {
            StopListener();
            RequireUid();
            _listenerQuery = LeaderboardRef.OrderByChild("score").LimitToLast(limit);
            _handler = (sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogError($"[Leaderboard] 리스너 오류: {args.DatabaseError.Message}");
                    return;
                }
                onChanged?.Invoke(ParseEntries(args.Snapshot));
            };
            _listenerQuery.ValueChanged += _handler;
        }

        public static void StopListener()
        {
            if (_listenerQuery != null && _handler != null)
                _listenerQuery.ValueChanged -= _handler;
            _listenerQuery = null;
            _handler = null;
        }

        private static List<LeaderboardEntry> ParseEntries(DataSnapshot snapshot)
        {
            var list = new List<LeaderboardEntry>();
            if (snapshot != null && snapshot.Exists)
            {
                foreach (DataSnapshot child in snapshot.Children)
                {
                    var entry = JsonUtility.FromJson<LeaderboardEntry>(child.GetRawJsonValue());
                    if (entry != null)
                    {
                        entry.uid = child.Key;
                        list.Add(entry);
                    }
                }
            }
            list.Sort((a, b) => b.score.CompareTo(a.score)); // 점수 내림차순(1위 먼저)
            return list;
        }

        private static string RequireUid()
        {
            var fb = FirebaseBootstrap.Instance;
            if (fb == null || !fb.IsReady)
                throw new InvalidOperationException(
                    "Firebase 가 아직 준비되지 않았습니다 (FirebaseBootstrap.IsReady=false)."
                );
            return fb.UserId;
        }
    }
}
