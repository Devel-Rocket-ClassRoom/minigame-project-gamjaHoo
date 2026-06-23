// CareerRepository.cs
// 명예의 전당 — careers/{uid} 읽기/쓰기 어댑터 (Persistence Layer, I/O).
//
// 07·08장 매핑:
//   RecordSeasonAsync      → Push() 로 시즌 노드 키 생성 + UpdateChildrenAsync 멀티패스
//                            (seasons/{key} + score 를 한 번에 원자적 갱신, 07장 7절)
//                            timestamp 는 ServerValue.Timestamp (07장 11절)
//   LoadRecentSeasonsAsync → OrderByChild("timestamp").LimitToLast(N) → Reverse (08장 7절)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using FMLite.Core;
using UnityEngine;

namespace FMLite.Persistence.Cloud
{
    public static class CareerRepository
    {
        private static DatabaseReference CareerRef =>
            FirebaseBootstrap.Instance.Database.RootReference.Child("careers").Child(RequireUid());

        // 한 시즌 기록을 Push 하고, 누적 점수(score)를 같은 호출로 원자적 갱신.
        public static async Task RecordSeasonAsync(
            string clubName,
            int year,
            int position,
            int points,
            int totalScore
        )
        {
            var careerRef = CareerRef;
            DatabaseReference seasonRef = careerRef.Child("seasons").Push();

            var seasonData = new Dictionary<string, object>
            {
                { "year", year },
                { "clubName", clubName },
                { "position", position },
                { "points", points },
                { "timestamp", ServerValue.Timestamp },
            };

            // careers/{uid} 기준 멀티패스: seasons/{key} 전체 + score 를 atomic 갱신.
            var updates = new Dictionary<string, object>
            {
                { $"seasons/{seasonRef.Key}", seasonData },
                { "score", totalScore },
            };
            await careerRef.UpdateChildrenAsync(updates);
        }

        // 최근 N개 시즌(최신순). timestamp 큰 N개를 받아 Reverse.
        public static async Task<List<SeasonRecord>> LoadRecentSeasonsAsync(int limit)
        {
            Query query = CareerRef.Child("seasons").OrderByChild("timestamp").LimitToLast(limit);
            DataSnapshot snapshot = await query.GetValueAsync();

            var list = new List<SeasonRecord>();
            if (snapshot != null && snapshot.Exists)
            {
                foreach (DataSnapshot child in snapshot.Children)
                {
                    var rec = JsonUtility.FromJson<SeasonRecord>(child.GetRawJsonValue());
                    if (rec != null)
                        list.Add(rec);
                }
                list.Reverse(); // 오름차순 도착 → 최신순
            }
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
