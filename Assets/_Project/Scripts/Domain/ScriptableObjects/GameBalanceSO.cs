// GameBalanceSO.cs
// V0.1 밸런싱 수치 외부화 (design-decisions.md #11). 알고리즘 명세 진행에 따라 필드 점진 추가.
// Player Generation 관련 필드는 algorithms.md #1 Balancing Parameters 섹션 기준.

using UnityEngine;

namespace FMLite.Domain
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "FM-Lite/Game Balance")]
    public class GameBalanceSO : ScriptableObject
    {
        [Header("Debug")]
        public bool isDebugMode = true;

        // ============================================================
        // Player Generation (algorithms.md #1)
        // ============================================================

        [Header("Player Generation — CA")]
        public int   minCA = 30;
        public int   maxCA = 200;
        public int   caRepBase = 60;
        public float caRepCoeff = 0.8f;
        public float caStdDev = 18f;
        public int   minAge = 15;
        public int   maxAge = 40;
        public int   caPeakAge = 27;
        public float caYoungMultiplier = 0.55f;

        [Header("Player Generation — PA")]
        public int   minPA = 50;
        public int   maxPA = 200;
        public float paGapMaxMean = 50f;
        public int   paGapZeroAge = 28;
        public float paGapStdDev = 15f;

        [Header("Player Generation — Stats Distribution")]
        public float statMeanAtCAFloor = 4f;
        public float statMeanAtCACeil = 17f;
        public float statEmphasisBonus = 2f;
        public float statEmphasisPenalty = 2f;
        public float statStdDev = 2.5f;
        public float gkSecondaryStatPenalty = 1f;     // GK 의 멘탈/피지컬 평균 감점
        public float gkOutfieldStatPenalty = 8f;       // GK 의 테크니컬 평균 감점
        public float outfieldGkStatBase = 3f;          // 필드 플레이어의 GK 스탯 평균

        [Header("Player Generation — Traits")]
        public float traitProbabilityPerPlayer = 0.30f;
        public float additionalTraitProbability = 0.15f;

        [Header("Player Generation — Secondary Positions")]
        public float secondaryPositionProbability = 0.40f;
        public float thirdPositionProbability = 0.15f;

        [Header("Player Generation — Personal")]
        public float footRightRatio = 0.62f;
        public float footLeftRatio = 0.30f;
        public float footBothRatio = 0.08f;

        [Header("Player Generation — Nationality")]
        public float primaryNationalityRatio = 0.70f;

        [Header("Player Generation — Initial Contract")]
        public int   wageBaseAtMinCA = 500;
        public float wagePerCAPoint = 350f;
        public int   wageFloor = 500;

        // ============================================================
        // Other Systems
        // ============================================================

        [Header("Starting Squad Gacha")]
        public int initialRerollTokens = 3;
        public int maxRerollStockpile = 5;
        // 5단계 티어 (Elite / Strong / Average / Weak / Poor) 누적 분포 임계점.
        public float[] tierThresholdsAccumulated = new[] { 0.10f, 0.40f, 0.80f, 0.95f };

        [Header("Match Simulation")]
        public float avgGoalsPerMatch = 2.70f;
        public float homeAdvantageBonus = 0.10f;

        [Header("Daily / Season")]
        public int fatigueRecoveryPerDay = 15;
        public int fatigueGainPerMatch = 30;
        public int retirementMinAge = 33;
        public float retirementProbabilityPerYear = 0.15f;
        public int seasonRerollTokenGrant = 3;

        [Header("Transfer")]
        public float marketValueAgeFactor = 1.0f;
        public float marketValueCAFactor = 100f;
    }
}
