// CloudProfileRepository.cs
// 명예의 전당 — users/{uid} 프로필 읽기/쓰기 어댑터 (Persistence Layer, I/O).
// FM-Lite 전용 얇은 어댑터. 인증/DB 접근은 재사용 레이어 FirebaseKit 에 위임.

using System;
using System.Threading.Tasks;
using FirebaseKit;
using UnityEngine;

namespace FMLite.Persistence.Cloud
{
    public static class CloudProfileRepository
    {
        // users/{uid} 를 통째로 저장 (최초 생성 / 전체 갱신).
        public static Task SetProfileAsync(string nickname)
        {
            string uid = RequireUid();
            var profile = new UserProfile
            {
                nickname = nickname,
                createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            return RealtimeDatabaseService.SetAsync($"users/{uid}", profile);
        }

        // 닉네임 한 칸만 갱신 — 경로를 좁혔으므로 createdAt 등 다른 필드는 보존된다.
        public static Task SetNicknameAsync(string nickname)
        {
            string uid = RequireUid();
            return RealtimeDatabaseService.SetValueAsync($"users/{uid}/nickname", nickname);
        }

        public static Task<UserProfile> GetProfileAsync()
        {
            string uid = RequireUid();
            return RealtimeDatabaseService.GetAsync<UserProfile>($"users/{uid}");
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
