// Facilities.cs
// 구단 시설 등급 (스카우트 / 훈련 / 유스).

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class Facilities
    {
        // V0.1 필드 (유지)
        public int scoutLevel;
        public int trainingLevel;
        public int youthLevel;

        public bool hasPendingUpgrade;
        public FacilityType pendingUpgradeType;
        public DateTime upgradeCompletionDate;

        // V1.0 신규 분리 필드 (design-decisions.md #49)
        public int youthCoachLevel; // 유스 평균 PA + 트레잇 가중치
        public int youthRecruitmentLevel; // 유스 풀 크기 + 인스펙션 빈도
        public int youthFacilityLevel; // 유스 성장률 + 콜업 적응
        public int medicalLevel; // 부상 회복 속도 + 발생률 ↓
        public int stadiumLevel; // 입장료 수입 + 명성 가산
        public int gymLevel; // 피지컬 성장률 + 부상 회복
    }
}
