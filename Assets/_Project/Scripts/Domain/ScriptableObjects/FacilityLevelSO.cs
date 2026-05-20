// FacilityLevelSO.cs
// 시설 유형 × 등급별 효과 정의. (Scout/Training/Youth) × Lv1~5 = 15 인스턴스.
// V0.1 활용은 Youth 풀 크기 + 평균 PA 위주. Scout/Training 효과는 V1.0+ 확장.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "FacilityLevel", menuName = "FM-Lite/Facility Level")]
    public class FacilityLevelSO : ScriptableObject
    {
        public FacilityType facilityType;
        public int level; // 1~5

        [Header("Upgrade")]
        public int upgradeCost;
        public int upgradeDurationDays;

        [Header("Youth Effect (only when facilityType == Youth)")]
        public int youthPoolSize;
        public int youthAvgPA;
    }
}
