// LeaderboardEntry.cs
// 명예의 전당 — 리더보드 1행 DTO (leaderboard/{uid}).
// 닉네임/구단명을 비정규화(중복 저장)해 상위 N 표시 시 추가 조회가 필요 없다 (08장 8절).

using System;

namespace FMLite.Persistence.Cloud
{
    [Serializable]
    public class LeaderboardEntry
    {
        public string nickname;
        public string clubName;
        public int score;

        // 노드 키(uid). 표시/식별용으로 파싱 후 채운다 — 직렬화 제외.
        [NonSerialized]
        public string uid;
    }
}
