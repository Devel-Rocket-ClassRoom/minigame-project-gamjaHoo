// GameBalanceSO.cs
// V0.1 밸런싱 수치 외부화 (design-decisions.md #11). 알고리즘 명세 진행에 따라 필드 점진 추가.
// Player Generation 관련 필드는 algorithms.md #1 Balancing Parameters 섹션 기준.

using System;
using UnityEngine;

namespace FMLite.Domain
{
    // 포메이션 단위 분배표 정책 (design-decisions.md #28 / #32).
    // V0.1: 4-4-2 단일 인스턴스 (GameBalanceSO.formation).
    // V1.0: FormationSO 로 추출 + List<FormationSO> availableFormations 카탈로그.
    [Serializable]
    public class FormationConfig
    {
        public string name = "4-4-2";

        [Header("Goalkeeper")]
        public int gk = 3;              // 서드키퍼 포함

        [Header("Defense — 각 라인 최소 인원")]
        public int cbMin = 4;
        public int lbMin = 2;
        public int rbMin = 2;

        [Header("Midfield — 그룹 합 최소")]
        public int dmCmGroupMin = 4;    // DM + CM 합
        public int lmLwGroupMin = 2;    // LM + LW 합
        public int rmRwGroupMin = 2;    // RM + RW 합

        [Header("Attack — 그룹 합 최소")]
        public int stCfGroupMin = 4;    // ST + CF 합

        [Header("Random Slots")]
        public int randomSlots = 2;     // 필수 인원 외 자유 자리 (시드 기반 추첨)
    }

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

        [Header("Club Generation — Formation (V0.1: 4-4-2 단일)")]
        // 분배표는 FormationConfig 단위. V0.1 단일 인스턴스, V1.0 에서 FormationSO 로 추출.
        // 필수 인원 합 + randomSlots = playersPerClub 일치 권장 (불일치 시 분배표 합 기준 진행).
        public FormationConfig formation = new FormationConfig();

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
        // Starting Squad Gacha (algorithms.md #6)
        // ============================================================

        [Header("Gacha — Tier Cuts (lineCA / expectedMeanCA ratio)")]
        public float tierEliteRatio   = 1.20f;
        public float tierStrongRatio  = 1.05f;
        public float tierAverageRatio = 0.90f;
        public float tierWeakRatio    = 0.75f;
        // < tierWeakRatio → Poor

        // ============================================================
        // Other Systems
        // ============================================================

        [Header("Starting Squad Gacha")]
        public int initialRerollTokens = 3;
        public int maxRerollStockpile = 5;
        // 5단계 티어 (Elite / Strong / Average / Weak / Poor) 누적 분포 임계점.
        public float[] tierThresholdsAccumulated = new[] { 0.10f, 0.40f, 0.80f, 0.95f };

        [Header("Match Simulation (algorithms.md #2)")]
        public float avgGoalsPerMatch       = 2.70f;     // EPL 평균
        public float homeAdvantageGoalBonus = 0.30f;     // homeLambda 에 가산 (away 감산 X)
        // strengthRatio 비선형 지수 — CA 차이를 골수 차이로 증폭.
        // k=1 이면 선형 (강팀 64% / 원정 51% 근사). k=1.5 (기본) 이면 강팀 ~72% / 원정 ~59%.
        // V1.0+ 매치 엔진 재작성 시 finishing 등 개별 stats 가 결정력 직접 표현하므로 k=1 회귀 또는 폐기.
        public float strengthExponent       = 1.5f;
        // Line enum 순서 (GK=0 / DF=1 / MF=2 / AT=3) 와 일치.
        // GK=0 → 페널티/코너 GK 골은 V1.0+ 텍스트 이벤트 시스템 진입 시 예외 처리.
        public float[] scoringWeightByLine  = { 0.0f, 0.4f, 1.5f, 5.0f };

        // ============================================================
        // Youth Intake (algorithms.md #4)
        // ============================================================

        [Header("Youth Intake — PA Distribution")]
        public float youthStarPickProbability = 0.05f;     // 5% 스타 픽 (PA bonus)
        public float youthStarPaBonus         = 50f;       // 스타 PA 평균 보너스
        public float youthPaStdDev            = 15f;       // PA 분포 σ
        public float youthPaGapStdDev         = 25f;       // CA-PA 갭 σ (PlayerGen σ=15 의 1.67배)

        [Header("Youth Intake — Age")]
        public int     youthIntakeMinAge      = 16;
        public int     youthIntakeMaxAge      = 18;
        public float[] youthIntakeAgeWeights  = { 0.40f, 0.40f, 0.20f };  // 16, 17, 18 순

        [Header("Youth Intake — Nationality")]
        public float youthPrimaryNationalityRatio = 0.78f;  // 자국 78% (ClubGen 0.70 보다 ↑)

        [Header("Youth Intake — Schedule")]
        public int youthIntakeMainMonth   = 6;
        public int youthIntakeMainDay     = 15;            // 메인 인스펙션: 6/15 (시즌 종료 직후)
        public int youthIntakeSecondMonth = 1;
        public int youthIntakeSecondDay   = 15;            // 보조 인스펙션: 1/15 (시즌 중간)

        [Header("Daily / Season")]
        public int fatigueRecoveryPerDay = 15;
        public int fatigueGainPerMatch = 30;
        public int retirementMinAge = 33;
        public float retirementProbabilityPerYear = 0.15f;
        public int seasonRerollTokenGrant = 3;

        // ============================================================
        // Season Cycle (data-flows.md #6, design-decisions.md #38)
        // ============================================================
        // 3 시점 변수명 분리 (사용자 혼동 회피, 2026-05-20):
        //  - seasonEnd*       = 5/15 — SeasonEndProcessor 트리거
        //  - fiscalYearStart* = 6/1  — NewSeasonProcessor 트리거 (회계연도)
        //  - newSeasonOpening*= 8/15 — ScheduleGenerator 가 새 시즌 첫 매치 배치
        // V1.0+ 트리거: 캘린더/요일 dynamic 계산 ("5월 마지막 토요일") + 매년 가변 일정.

        [Header("Season Cycle — Trigger Days")]
        public int seasonEndMonth        = 5;
        public int seasonEndDay          = 15;
        public int fiscalYearStartMonth  = 6;
        public int fiscalYearStartDay    = 1;
        public int newSeasonOpeningMonth = 8;
        public int newSeasonOpeningDay   = 15;

        // ============================================================
        // Transfer Market (algorithms.md #3 / #3.1)
        // ============================================================

        [Header("Transfer Market — Value")]
        public int     marketValueBase            = 500_000;        // CA=100 기준점 (algorithms.md #3 Logic)
        public float   marketValueCaExponent      = 4.0f;            // pow 지수 (슈퍼스타 압도)
        public float   marketValuePaCoeff         = 50_000f;         // PA-CA 갭 1 = 50k
        // AgeCurve 4 구간: 16~21 / 22~28 (피크) / 29~33 / 34+
        public float[] marketValueAgeCurve        = { 0.85f, 1.20f, 0.75f, 0.35f };
        // ContractCurve 4 구간: 잔여 1 / 2 / 3 / 4+년
        public float[] marketValueContractCurve   = { 0.50f, 0.80f, 1.00f, 1.05f };
        // PositionFactor 4 구간: GK / DF / MF / AT (Line enum 순서)
        public float[] marketValuePositionFactor  = { 0.75f, 0.85f, 1.00f, 1.20f };
        public float   marketValueInjuryFactor    = 0.50f;
        public float   aiValueNoiseSigma          = 0.10f;           // AI 평가 ±10% noise

        [Header("Transfer Market — Acceptance")]
        public float aiAcceptRatio = 1.20f;     // offer/marketValue 비율 >= 시 Accept

        [Header("Transfer Market — 이적시장 활성화 기간 (Transfer Window)")]
        // 여름: 6/1 ~ 8/31 (시즌 종료 직후)
        public int transferWindowSummerStartMonth = 6;
        public int transferWindowSummerStartDay   = 1;
        public int transferWindowSummerEndMonth   = 8;
        public int transferWindowSummerEndDay     = 31;
        // 겨울: 1/1 ~ 1/31 (시즌 중간)
        public int transferWindowWinterStartMonth = 1;
        public int transferWindowWinterStartDay   = 1;
        public int transferWindowWinterEndMonth   = 1;
        public int transferWindowWinterEndDay     = 31;
    }
}
