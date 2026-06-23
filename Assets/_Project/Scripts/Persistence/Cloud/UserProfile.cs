// UserProfile.cs
// 명예의 전당 — 클라우드 감독 프로필 DTO (users/{uid}).
// Firebase 전송용 단순 데이터. JsonUtility 로 직렬화/역직렬화하여
// SetRawJsonValueAsync 로 "구조"로 저장한다 (07장 6절).

using System;

namespace FMLite.Persistence.Cloud
{
    [Serializable]
    public class UserProfile
    {
        public string nickname;
        public long createdAt; // Unix milliseconds
    }
}
