// CloudProfileRepository.cs
// 명예의 전당 — users/{uid} 프로필 읽기/쓰기 어댑터 (Persistence Layer, I/O).
// SaveSystem 과 같은 레이어. FirebaseBootstrap(Core) 에서 Database/uid 를 얻는다.
//
// 07장 매핑:
//   SetProfileAsync     → SetRawJsonValueAsync (객체를 구조로 저장)
//   SetNicknameAsync    → 좁힌 경로에 SetValueAsync (다른 필드 보존)
//   GetProfileAsync     → GetValueAsync + GetRawJsonValue

using System;
using System.Threading.Tasks;
using Firebase.Database;
using FMLite.Core;
using UnityEngine;

namespace FMLite.Persistence.Cloud
{
    public static class CloudProfileRepository
    {
        private static DatabaseReference UsersRef =>
            FirebaseBootstrap.Instance.Database.RootReference.Child("users");

        // users/{uid} 를 통째로 저장 (최초 생성 / 전체 갱신).
        public static async Task SetProfileAsync(string nickname)
        {
            string uid = RequireUid();
            var profile = new UserProfile
            {
                nickname = nickname,
                createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            string json = JsonUtility.ToJson(profile);
            await UsersRef.Child(uid).SetRawJsonValueAsync(json);
        }

        // 닉네임 한 칸만 갱신 — 경로를 좁혔으므로 createdAt 등 다른 필드는 보존된다.
        public static async Task SetNicknameAsync(string nickname)
        {
            string uid = RequireUid();
            await UsersRef.Child(uid).Child("nickname").SetValueAsync(nickname);
        }

        public static async Task<UserProfile> GetProfileAsync()
        {
            string uid = RequireUid();
            DataSnapshot snapshot = await UsersRef.Child(uid).GetValueAsync();
            if (!snapshot.Exists)
                return null;
            return JsonUtility.FromJson<UserProfile>(snapshot.GetRawJsonValue());
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
