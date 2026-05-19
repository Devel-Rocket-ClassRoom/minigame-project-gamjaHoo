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
        // Club Generation (algorithms.md #5)
        // ============================================================

        [Header("Club Generation — Reputation Tiers")]
        // ratio 합 ≈ 1.0. round-off 잔여는 AllocateTierCounts 가 흡수.
        // clubCount 가변 대응 (10/12/20/24 등 어떤 값이든 동작).
        public float[] tierClubRatios = { 0.20f, 0.30f, 0.35f, 0.15f };  // Top4 / Euro / Mid / Rel
        public int[]   tierRepMin     = {  85,    65,    45,    25   };
        public int[]   tierRepMax     = {  95,    80,    60,    40   };

        [Header("Club Generation — Finance")]
        public int   financeBaseMoney    = 5_000_000;     // £5M base at rep=0
        public float financeRepCoeff     = 4_000_000f;    // rep=50 → 205M, rep=95 → 385M
        public float financeNoiseSigma   = 0.15f;         // 15% σ
        public int   financeFloor        = 1_000_000;
        public float transferBudgetRatio = 0.20f;
        public float wageBudgetRatio     = 0.50f;

        [Header("Club Generation — Facilities")]
        public float facilityNoiseSigma  = 1.0f;          // ±1 등급 정도 노이즈
        public int   minFacilityLevel    = 1;
        public int   maxFacilityLevel    = 5;

        [Header("Club Generation — Squad Composition")]
        // 기본 합 = 25 (LeagueConfigSO.playersPerClub 기본값과 일치).
        // playersPerClub ≠ Σsquad* → 분배표 합 기준으로 진행 + 경고. V1.0 에서 ratio화 검토.
        public int squadGK = 3;
        public int squadCB = 4;
        public int squadLB = 2;
        public int squadRB = 2;
        public int squadDM = 2;
        public int squadCM = 3;
        public int squadAM = 2;
        public int squadLM = 1;
        public int squadRM = 1;
        public int squadLW = 1;
        public int squadRW = 1;
        public int squadST = 2;
        public int squadCF = 1;

        [Header("Club Generation — Age Distribution")]
        public float youthAgeRatio   = 0.20f;
        public float primeAgeRatio   = 0.60f;
        public float veteranAgeRatio = 0.20f;
        public int   youthAgeMin     = 16;
        public int   youthAgeMax     = 21;
        public int   primeAgeMin     = 22;
        public int   primeAgeMax     = 28;
        public int   veteranAgeMin   = 29;
        public int   veteranAgeMax   = 35;

        [Header("Club Generation — Foundation Year")]
        public int clubMinAgeYears = 50;
        public int clubMaxAgeYears = 150;

        [Header("Club Generation — Homegrown")]
        // 초기 스쿼드에서 자체 유스 출신 비율 (player.youthClubId = club.id).
        public float homegrownRatio = 0.20f;

        [Header("Club Generation — Board")]
        public int initialBoardConfidence = 50;

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
