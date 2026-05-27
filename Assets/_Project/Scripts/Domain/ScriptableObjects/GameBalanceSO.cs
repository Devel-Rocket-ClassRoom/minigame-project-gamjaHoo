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
        public int gk = 3; // 서드키퍼 포함

        [Header("Defense — 각 라인 최소 인원")]
        public int cbMin = 4;
        public int lbMin = 2;
        public int rbMin = 2;

        [Header("Midfield — 그룹 합 최소")]
        public int dmCmGroupMin = 4; // DM + CM 합
        public int lmLwGroupMin = 2; // LM + LW 합
        public int rmRwGroupMin = 2; // RM + RW 합

        [Header("Attack — 그룹 합 최소")]
        public int stCfGroupMin = 4; // ST + CF 합

        [Header("Random Slots")]
        public int randomSlots = 2; // 필수 인원 외 자유 자리 (시드 기반 추첨)
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
        public int minCA = 30;
        public int maxCA = 200;
        public int caRepBase = 60;
        public float caRepCoeff = 0.8f;
        public float caStdDev = 18f;
        public int minAge = 15;
        public int maxAge = 40;
        public int caPeakAge = 27;
        public float caYoungMultiplier = 0.55f;

        [Header("Player Generation — PA")]
        public int minPA = 50;
        public int maxPA = 200;
        public float paGapMaxMean = 50f;
        public int paGapZeroAge = 28;
        public float paGapStdDev = 15f;

        [Header("Player Generation — Hidden Attributes")]
        public float hiddenAttrMean = 50f;
        public float hiddenAttrStdDev = 15f;

        [Header("Player Generation — Stats Distribution")]
        public float statMeanAtCAFloor = 25f; // 1-100 스케일 (V0.1: 4)
        public float statMeanAtCACeil = 85f; // 1-100 스케일 (V0.1: 17)
        public float statEmphasisBonus = 10f; // 1-100 스케일 (V0.1: 2)
        public float statEmphasisPenalty = 5f; // 1-100 스케일 (V0.1: 2)
        public float statStdDev = 10f; // 1-100 스케일 (V0.1: 2.5)
        public float gkSecondaryStatPenalty = 10f; // GK 의 멘탈/피지컬 평균 감점 (V0.1: 1)
        public float gkOutfieldStatPenalty = 40f; // GK 의 테크니컬 평균 감점 (V0.1: 8)
        public float outfieldGkStatBase = 15f; // 필드 플레이어의 GK 스탯 평균 (V0.1: 3)

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
        public int wageBaseAtMinCA = 500;
        public float wagePerCAPoint = 350f;
        public int wageFloor = 500;

        // ============================================================
        // Club Generation (algorithms.md #5)
        // ============================================================

        [Header("Club Generation — Reputation Tiers")]
        // ratio 합 ≈ 1.0. round-off 잔여는 AllocateTierCounts 가 흡수.
        // clubCount 가변 대응 (10/12/20/24 등 어떤 값이든 동작).
        public float[] tierClubRatios = { 0.20f, 0.30f, 0.35f, 0.15f }; // Top4 / Euro / Mid / Rel
        public int[] tierRepMin = { 85, 65, 45, 25 };
        public int[] tierRepMax = { 95, 80, 60, 40 };

        [Header("Club Generation — Finance")]
        public int financeBaseMoney = 5_000_000; // £5M base at rep=0
        public float financeRepCoeff = 4_000_000f; // rep=50 → 205M, rep=95 → 385M
        public float financeNoiseSigma = 0.15f; // 15% σ
        public int financeFloor = 1_000_000;
        public float transferBudgetRatio = 0.20f;
        public float wageBudgetRatio = 0.50f;

        [Header("Club Generation — Facilities")]
        public float facilityNoiseSigma = 1.0f; // ±1 등급 정도 노이즈
        public int minFacilityLevel = 1;
        public int maxFacilityLevel = 5;

        [Header("Club Generation — Formation (V0.1: 4-4-2 단일)")]
        // 분배표는 FormationConfig 단위. V0.1 단일 인스턴스, V1.0 에서 FormationSO 로 추출.
        // 필수 인원 합 + randomSlots = playersPerClub 일치 권장 (불일치 시 분배표 합 기준 진행).
        public FormationConfig formation = new FormationConfig();

        [Header("Club Generation — Age Distribution")]
        public float youthAgeRatio = 0.20f;
        public float primeAgeRatio = 0.60f;
        public float veteranAgeRatio = 0.20f;
        public int youthAgeMin = 16;
        public int youthAgeMax = 21;
        public int primeAgeMin = 22;
        public int primeAgeMax = 28;
        public int veteranAgeMin = 29;
        public int veteranAgeMax = 35;

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
        public float tierEliteRatio = 1.20f;
        public float tierStrongRatio = 1.05f;
        public float tierAverageRatio = 0.90f;
        public float tierWeakRatio = 0.75f;

        // < tierWeakRatio → Poor

        // ============================================================
        // Other Systems
        // ============================================================

        [Header("Starting Squad Gacha")]
        public int initialRerollTokens = 3;
        public int maxRerollStockpile = 5;

        // 5단계 티어 (Elite / Strong / Average / Weak / Poor) 누적 분포 임계점.
        public float[] tierThresholdsAccumulated = new[] { 0.10f, 0.40f, 0.80f, 0.95f };

        [Header("Match Simulation (algorithms.md #2 — V0.1)")]
        public float avgGoalsPerMatch = 2.70f; // EPL 평균 — SimulateLite (I.7) 에서 재활용
        public float homeAdvantageGoalBonus = 0.30f; // SimulateLite (I.7) 에서 재활용

        // strengthRatio 비선형 지수 — I.9 에서 폐기 예정. SimulateLite 가 사용.
        public float strengthExponent = 1.5f;

        // Line enum 순서 (GK=0 / DF=1 / MF=2 / AT=3) 와 일치.
        public float[] scoringWeightByLine = { 0.0f, 0.4f, 1.5f, 5.0f };

        // ============================================================
        // Match Simulation V1.0 — I.2 분 단위 이벤트 (algorithms.md V1.0-2)
        // ============================================================
        // 명세 표의 분모 (Shot=200 / Foul=500 / KeyPass=Cross=50000) 는 placeholder 성격 — 매 분 발생 빈도가
        // 비현실적 (avgCA 100 기준 90분 45슛/팀). I.2 에서 EPL 통계 (12-15 슛/팀/매치) 근사하도록 재조정.
        // EditMode 분포 테스트로 확정. 모두 외부화 (#11 매직 넘버 금지).

        [Header("Match V1.0 — Shot")]
        // EPL 통계 근사 (12-15 슛/팀/매치). avgCA 100 / 720 × 90 = ~12.5 슛/팀.
        public int shotChanceBaseDivisor = 720; // shotChance per min per team = avgCA / N × mentalityModifier
        public float shotOnTargetDivisor = 100f; // (finishing × composure) / N → on-target 확률 %
        public float shotSaveDivisor = 100f; // (reflexes × handling) / N → save 확률 %

        // 슈터 선정 가중 (Line enum 순서 GK=0/DF=1/MF=2/AT=3). scoringWeightByLine 와 동일 의도 — V1.0 매치 엔진은 stat × position 결합.
        public float[] shotPositionWeights = { 0f, 0.4f, 1.5f, 5f };

        [Header("Match V1.0 — KeyPass / Cross")]
        // KeyPass: 11명 × 90 × (50×50) / divisor = 8 keyPasses/팀/매치 target → divisor ~ 310,000
        public int keyPassChanceDivisor = 300_000;
        // Cross (LW/RW 2명만): 2 × 90 × (50×50) / divisor = 15 cross/팀/매치 target → divisor ~ 30,000
        public int crossChanceDivisor = 30_000;

        [Header("Match V1.0 — Pass / Tackle / Interception 누적")]
        // Pass: 11명 × 90 × 50 / divisor = 330 pass/팀 target (30/선수) → divisor 150
        public int passChanceDivisor = 150;
        public float passSuccessDivisor = 100f; // passing / N → 성공 확률 % (passesCompleted)
        // Tackle/Interception (DF/MF 7명만): 7 × 90 × 50 / divisor = 21 tackles/팀 target → divisor 1500
        public int tackleChanceDivisor = 1500;
        public int interceptionChanceDivisor = 1500;

        [Header("Match V1.0 — Foul / Card")]
        // Foul: 7 × 90 × 50 / divisor = 12 fouls/팀 target → divisor 2625
        public int foulChanceDivisor = 2625;
        public float foulYellowRatio = 0.50f; // Foul 시 옐로 확률
        public float foulRedRatio = 0.03f; // Foul 시 레드 확률

        [Header("Match V1.0 — Injury")]
        // 11 × 90 × baseRate = ~0.1 부상/팀/매치 target → baseRate 0.0001 (10매치당 1 부상)
        public float injuryBaseRate = 0.0001f;
        public float injuryProneRefDivisor = 50f; // injuryProneness 기준값 (50 = 평균)
        public int injuryCareerThreateningDays = 90; // recoveryDays >= N → isCareerThreatening = true

        // ============================================================
        // Match Simulation V1.0 — 5-Zone Markov (algorithms.md V1.0-2 / #44 / #55)
        // I.1' 상태 머신 + I.2' zone resolution. OFM 차용.
        // I.2 분 단위 독립 추첨 필드 (shotChanceBaseDivisor 등) 는 5-zone 에서 미사용 → I.9 정리.
        // ============================================================

        [Header("Match 5-Zone — Core")]
        public float homeAdvantageMultiplier = 1.08f; // home 팀 rating 곱셈 (I.8 통합, OFM 1.08)
        public int actionsPerMinuteMin = 1; // 매 분 action 수
        public int actionsPerMinuteMax = 3;

        [Header("Match 5-Zone — Shot")]
        public float shotAccuracyBase = 0.45f; // on-target = base + (shootRating-50)/divisor, clamp 0.15~0.85
        public float shotAccuracyDivisor = 200f;
        public float goalConversionBase = 0.30f; // goal = base + (shootRating-gkRating)/divisor, clamp 0.10~0.70
        public float goalConversionDivisor = 150f;
        public float shotBlockedRatio = 0.40f; // off-target 중 block 비율 (나머지 off)

        [Header("Match 5-Zone — Zone Transition")]
        public float zoneCornerChance = 0.25f; // attacking third 실패 시 corner 확률
        public float zoneCornerToBoxChance = 0.30f; // corner → box 재진입 확률
        public float midfieldTackleRatio = 0.60f; // midfield 실패 시 Tackle vs Interception
        public float attackingThirdTackleRatio = 0.50f;

        [Header("Match 5-Zone — Stoppage")]
        public int stoppageTimeMax = 4; // 하프당 추가시간 최대 (rng 0~N)

        [Header("Match 5-Zone — External Effects (I.8)")]
        public int fatiguePerfThreshold = 50; // fatigue ≤ N → 경기력 보정 없음
        public float fatiguePerfFloor = 0.6f; // fatigue 100 → perf 최저 (0.6)
        public float fatiguePerfPenaltyPerPoint = 0.01f; // fatigue 1pt 초과 당 -1%
        public int fatigueInjuryThreshold = 40; // fatigue > N → 부상률 ×multiplier
        public float fatigueInjuryMultiplier = 1.5f;
        public float formCoeff = 200f; // formMoraleMod = (1+(form-50)/formCoeff)×…
        public float moraleCoeff = 200f;

        [Header("Match 5-Zone — Foul / Card / Injury (I.3)")]
        public float foulProbability = 0.12f; // Tackle 시 파울 base (× aggression 보정)
        public float penaltyProbability = 0.08f; // box 안 파울 → 페널티 확률
        public float yellowCardProbability = 0.30f; // 파울 → 카드 확률
        public float redCardProbability = 0.04f; // 카드 중 다이렉트 레드 비율
        public float matchInjuryProbability = 0.03f; // 파울당 부상 확률
        public float penaltyConversion = 0.75f; // 인-매치 페널티 기본 전환율 (penaltyTaking vs GK 보정)
        public int yellowSuspensionThreshold = 5; // 시즌 누적 옐로 5/10/15 → 정지 1/2/3
        public int redSuspensionMatches = 2; // 레드 / 2옐로 → 정지 경기 수

        [Header("Match 5-Zone — Rating (I.4)")]
        public float ratingBase = 6.5f; // 기본 평점
        public float ratingGoalBonus = 1.0f;
        public float ratingAssistBonus = 0.5f;
        public float ratingKeyPassBonus = 0.1f;
        public float ratingDefActionBonus = 0.05f; // Tackle + Interception 당
        public float ratingShotOnTargetBonus = 0.1f;
        public float ratingYellowPenalty = -0.3f;
        public float ratingRedPenalty = -1.5f;
        public float ratingSaveBonus = 0.15f; // GK 선방당
        public float ratingCleanSheetBonus = 0.5f; // GK 무실점
        public float ratingConcededPenalty = -0.2f; // GK 실점당
        public float ratingWinBonus = 0.2f; // 팀 승리 전원
        public float ratingLossPenalty = -0.2f; // 팀 패배 전원
        public float ratingMin = 1.0f;
        public float ratingMax = 10.0f;

        // ============================================================
        // Youth Intake (algorithms.md #4)
        // ============================================================

        [Header("Youth Intake — PA Distribution")]
        public float youthStarPickProbability = 0.05f; // 5% 스타 픽 (PA bonus)
        public float youthStarPaBonus = 50f; // 스타 PA 평균 보너스
        public float youthPaStdDev = 15f; // PA 분포 σ
        public float youthPaGapStdDev = 25f; // CA-PA 갭 σ (PlayerGen σ=15 의 1.67배)

        [Header("Youth Intake — Age")]
        public int youthIntakeMinAge = 16;
        public int youthIntakeMaxAge = 18;
        public float[] youthIntakeAgeWeights = { 0.40f, 0.40f, 0.20f }; // 16, 17, 18 순

        [Header("Youth Intake — Nationality")]
        public float youthPrimaryNationalityRatio = 0.78f; // 자국 78% (ClubGen 0.70 보다 ↑)

        [Header("Youth Intake — Schedule")]
        public int youthIntakeMainMonth = 6;
        public int youthIntakeMainDay = 15; // 메인 인스펙션: 6/15 (시즌 종료 직후)
        public int youthIntakeSecondMonth = 1;
        public int youthIntakeSecondDay = 15; // 보조 인스펙션: 1/15 (시즌 중간)

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
        public int seasonEndMonth = 5;
        public int seasonEndDay = 15;
        public int fiscalYearStartMonth = 6;
        public int fiscalYearStartDay = 1;
        public int newSeasonOpeningMonth = 8;
        public int newSeasonOpeningDay = 15;

        // ============================================================
        // Transfer Market (algorithms.md #3 / #3.1)
        // ============================================================

        [Header("Transfer Market — Value")]
        public int marketValueBase = 500_000; // CA=100 기준점 (algorithms.md #3 Logic)
        public float marketValueCaExponent = 4.0f; // pow 지수 (슈퍼스타 압도)
        public float marketValuePaCoeff = 50_000f; // PA-CA 갭 1 = 50k

        // AgeCurve 4 구간: 16~21 / 22~28 (피크) / 29~33 / 34+
        public float[] marketValueAgeCurve = { 0.85f, 1.20f, 0.75f, 0.35f };

        // ContractCurve 4 구간: 잔여 1 / 2 / 3 / 4+년
        public float[] marketValueContractCurve = { 0.50f, 0.80f, 1.00f, 1.05f };

        // PositionFactor 4 구간: GK / DF / MF / AT (Line enum 순서)
        public float[] marketValuePositionFactor = { 0.75f, 0.85f, 1.00f, 1.20f };
        public float marketValueInjuryFactor = 0.50f;
        public float aiValueNoiseSigma = 0.10f; // AI 평가 ±10% noise

        [Header("Transfer Market — Acceptance")]
        public float aiAcceptRatio = 1.20f; // offer/marketValue 비율 >= 시 Accept (V0.1 legacy)

        [Header("Transfer Market — V1.0 K.1 협상 4분기")]
        public float aiAcceptThreshold = 1.30f; // ratio >= → Accepted
        public float aiCounterOfferThreshold = 1.10f; // ratio >= → CounterOffer (역제안)
        public float aiMockingThreshold = 0.85f; // ratio < → Mocking (모욕적 오퍼, 사기 감소)
        public float aiCounterOfferFactor = 1.30f; // counterAmount = aiPerceivedValue × factor
        public int maxNegotiationRounds = 3; // 최대 협상 라운드
        public int aiMockingMoralePenalty = 3; // 모욕적 오퍼 시 사기 감소량
        public float playtimeAgreementBonus = 0.2f; // 출전시간 약속 포함 시 선수 수락 확률 +N

        [Header("Transfer Market — 이적시장 활성화 기간 (Transfer Window)")]
        // 여름: 6/1 ~ 8/31 (시즌 종료 직후)
        public int transferWindowSummerStartMonth = 6;
        public int transferWindowSummerStartDay = 1;
        public int transferWindowSummerEndMonth = 8;
        public int transferWindowSummerEndDay = 31;

        // 겨울: 1/1 ~ 1/31 (시즌 중간)
        public int transferWindowWinterStartMonth = 1;
        public int transferWindowWinterStartDay = 1;
        public int transferWindowWinterEndMonth = 1;
        public int transferWindowWinterEndDay = 31;

        // ============================================================
        // Mentality Modifiers (design-decisions.md #45)
        // 배열 인덱스 = Mentality enum 순서 (VeryDefensive=0 ~ VeryAttacking=6)
        // ============================================================

        [Header("Mentality — Shot Frequency Multiplier (VeryDef→VeryAtk)")]
        public float[] mentalityShotMultiplier =
        {
            0.60f,
            0.75f,
            0.88f,
            1.00f,
            1.15f,
            1.30f,
            1.50f,
        };

        [Header("Mentality — Defensive Pressure Multiplier")]
        public float[] mentalityPressureMultiplier =
        {
            1.40f,
            1.20f,
            1.10f,
            1.00f,
            0.90f,
            0.80f,
            0.65f,
        };

        // ── V1.0 D.4 시설 효과 (design-decisions.md #53 / algorithms.md V1.0-10 + V1.0-11) ──

        [Header("Player Growth System (V1.0-10)")]
        public float growthBaseChance = 0.01f;
        public float growthAbsoluteFactor = 0.10f;
        public float growthTrainingCoeff = 0.10f;
        public float growthGymCoeff = 0.05f;
        public float growthPaGapNormalizer = 50f;
        public float growthYouthFactor = 1.5f;
        public int growthYouthPeakAge = 22;
        public int growthPrimePeakAge = 26;
        public int growthDeclineStartAge = 30;
        public float[] growthSizeWeights = { 75f, 20f, 5f };
        public float[] growthSizePeakWeights = { 60f, 30f, 10f };
        public float growthBigJumpAgeThreshold = 1.3f;

        [Header("Injury System (V1.0-11)")]
        public float injuryMedicalRecoveryCoeff = 0.05f;
        public float injuryGymRecoveryCoeff = 0.02f;
        public float injuryMedicalRateCoeff = 0.05f;

        [Header("Scouting System (V1.0 E.2)")]
        public int scoutWeeklyLevelGain = 5;

        [Header("Cpu Transfer AI (V1.0 F.1)")]
        public float aiWeaknessRatioThreshold = 0.95f;
        public int aiCoreInjuryWeeksThreshold = 4;
        public int aiSavingsThreshold = 10000; // 만원 단위. 명성당 임계.
        public float aiOfferAmountRandomMin = 1.20f;
        public float aiOfferAmountRandomMax = 1.40f;
        public float aiBudgetRatio = 0.4f;
        public float aiContractFaThresholdDays = 180f; // 잔여 6개월

        // ── V1.0 G.1 Morale System (algorithms.md V1.0-6 / design-decisions.md #42) ──

        [Header("Morale System (V1.0 G.1)")]
        public int moraleDailyRecoveryRate = 1; // 매일 50 으로 수렴 속도
        public int moraleMatchWinBonus = 8;
        public int moraleMatchLossPenalty = 8;
        public float moraleBigMatchMultiplier = 1.5f;
        public int moraleHighRatingBonus = 5;
        public int moraleLowRatingPenalty = 3; // 부호는 호출부에서
        public int moralePromotionWinBonus = 30; // 시즌 우승 / 승격 — V1.0 M.4 시점 사용
        public int moraleRelegationPenalty = 50; // 강등 — V1.0 M.4 시점 사용 (부호 호출부)
        public int contractRenewalMoraleBoost = 15;
        public int contractRenewalHappinessBoost = 25;
        public int promiseBreakHappinessPenalty = 20; // 부호 호출부
        public int promiseFulfilledHappinessBonus = 10;
        public int transferRequestThreshold = 20; // Happiness < N → TransferRequestEvent
        public float transferListedDiscount = 0.7f; // transferListed 선수 시장가 ×0.7
        public int unhappyThreshold = 40;
        public int satisfiedThreshold = 80;
        public int transferCompletedIncomingMoraleBonus = 20; // 영입된 선수 환영 (spec 침묵 → 합리적 디폴트)
        public int interviewPraiseMoraleBonus = 5;
        public int interviewCriticizeMoralePenalty = 3; // 부호 호출부
        public float ratingHighThreshold = 7.5f;
        public float ratingLowThreshold = 6.0f;

        // ── V1.0 G.2 Promise System (algorithms.md V1.0-6 / design-decisions.md #43) ──

        [Header("Promise System (V1.0 G.2)")]
        public int promisePlaytimeDefaultRatio = 50; // OnInterview PromisePlaytime 의 기본 출전 비율 (%)
        public int promiseDeadlineApproachingDays = 30; // (deadline - currentDate).Days ≤ N → 임박 알림 1회

        [Header("Dressing Room Mood (V1.0 G.3)")]
        public int dressingRoomMoodLowThreshold = 30; // < N → 매치 strength 패널티
        public float dressingRoomLowMoodStrengthFactor = 0.95f; // < 30 시 strength 배율 (≈ 폼 -5)
        public float dressingRoomCaptainLeadershipBonus = 0.2f; // captain leadership × N → mood 가산
        public int promisePlaytimeDeadlineMonth = 5; // 시즌 종료월 (5/15)
        public int promisePlaytimeDeadlineDay = 15;
        public int promiseRenewalDeadlineMonth = 8; // 다음 시즌 개막 (8/15)
        public int promiseRenewalDeadlineDay = 15;
        public int promiseTransferDeadlineDaysSummerEnd = 31; // 8/31 활성화 기간 종료
        public int promiseTransferDeadlineMonthSummerEnd = 8;
        public int promiseTransferDeadlineDaysWinterEnd = 31; // 1/31
        public int promiseTransferDeadlineMonthWinterEnd = 1;

        // ── V1.0 I.6 SubstitutionAI ─────────────────────────────────────
        public int maxSubstitutionsPerTeam = 3;
        public int substitutionFatigueThreshold = 70; // fatigue > N → 교체 후보
        public int substitutionTacticalMinute = 60;   // 전술 교체 (피로/스코어) 시작 분
    }
}
