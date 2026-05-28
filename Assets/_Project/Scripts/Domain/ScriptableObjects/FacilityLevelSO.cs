// FacilityLevelSO.cs
// 시설 유형 × 등급별 효과 정의. V1.0: 8 type × Lv1~10 = 80 인스턴스.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "FacilityLevel", menuName = "FM-Lite/Facility Level")]
    public class FacilityLevelSO : ScriptableObject
    {
        public FacilityType facilityType;
        public int level; // 1~10

        [Header("Upgrade")]
        public int upgradeCost;
        public int upgradeDurationDays;

        [Header("Scout")]
        public int scoutingListSize; // 스카우팅 명단 최대 인원
        public int caAccuracyMargin; // ± CA/PA 추정 오차

        [Header("Training")]
        public float trainingEfficiency = 1.0f; // 1군 훈련 성장률 곱셈

        [Header("Youth Coach")]
        public int youthAvgPABonus; // 유스 인테이크 평균 PA 가산
        public float traitGrantChance; // 추가 트레잇 부여 확률 (0-1)

        [Header("Youth Recruitment")]
        public int youthPoolSize; // 유스 인테이크 풀 크기
        public float signRatio = 1.0f; // 영입 가능 비율 (Lv1=0.33 ~ Lv10=1.0)

        [Header("Youth Facility")]
        public float youthGrowthRate = 1.0f; // 유스 선수 성장률 곱셈

        [Header("Medical")]
        public float injuryRateMultiplier = 1.0f; // <1 = 부상 발생 ↓
        public float recoverySpeedMultiplier = 1.0f; // >1 = 회복 속도 ↑

        [Header("Stadium")]
        public int ticketRevenueBase; // 홈 경기당 기본 입장료 수입
        public int reputationBonus; // 구단 명성 가산

        [Header("Gym")]
        public float physicalGrowthBonus = 1.0f; // 피지컬 스탯 성장률 곱셈

        // V0.1 호환 — Youth 타입에서 사용하던 평균PA 필드
        [HideInInspector]
        public int youthAvgPA;
    }
}
