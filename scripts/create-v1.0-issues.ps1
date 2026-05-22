# V1.0 이슈 일괄 생성 스크립트
# v1.0-tasks.md Stage A~P 모든 Task → GitHub Issue + 메타데이터 (Type / Area / Milestone / Priority / Size / 보드 #50)
#
# 사용법:
#   .\scripts\create-v1.0-issues.ps1                # 전체 실행
#   .\scripts\create-v1.0-issues.ps1 -StartFrom A.2 # 특정 Task 부터 실행
#
# 의존: gh CLI 인증, repo: Devel-Rocket-ClassRoom/minigame-project-gamjaHoo

param(
    [string]$StartFrom = "",
    [switch]$DryRun = $false
)

$ErrorActionPreference = 'Continue'

# UTF-8 출력 인코딩 강제 (PowerShell 5.x 의 CP949 mojibake 회피)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# GraphQL queries (variables 형식 — ID! strict 검증 통과)
$AddQuery = 'mutation($p: ID!, $c: ID!) { addProjectV2ItemById(input: { projectId: $p, contentId: $c }) { item { id } } }'
$UpdQuery = 'mutation($p: ID!, $i: ID!, $f: ID!, $o: String!) { updateProjectV2ItemFieldValue(input: { projectId: $p, itemId: $i, fieldId: $f, value: { singleSelectOptionId: $o } }) { projectV2Item { id } } }'

# Project + field IDs
$ProjectId       = "PVT_kwDODykJwc4BYAHm"
$PriorityFieldId = "PVTSSF_lADODykJwc4BYAHmzhTJKJg"
$SizeFieldId     = "PVTSSF_lADODykJwc4BYAHmzhTJKKY"
$PriorityOpts    = @{ P0 = "93ef17a8"; P1 = "224bd1d6"; P2 = "4eea6a89" }
$SizeOpts        = @{ XS = "e2f4dac2"; S = "ba56ccb2"; M = "0ce57aba"; L = "a6edd398"; XL = "bf501b62" }

$Repo  = "Devel-Rocket-ClassRoom/minigame-project-gamjaHoo"
$Owner = "Devel-Rocket-ClassRoom"

# Task 정의 — v1.0-tasks.md 기반
# Stage A (A.1~A.5) 는 이미 #179~#183 으로 생성됨 (시범).
# 본 스크립트는 Stage B~P 의 77 Task 를 일괄 생성.
$Tasks = @(
    # ─── Stage B: 선수 능력치 재정의 (5) ──────────────────────
    @{ Key="B.1"; Title="[Domain] Stats 49 카테고리 재정의 (FM 1:1)"; Type="Feature"; Labels="area:domain"; Priority="P0"; Size="M";
       Body=@"
## 목표
``Stats.cs`` 4 카테고리 = FM26 표준 49 필드 (Technical 14 / Mental 14 / Physical 8 / Goalkeeping 13).

## 명세
- v1.0-tasks.md Stage B / Task B.1
- design-decisions.md #39
- algorithms.md V1.0-1

## 작업 내용
- [ ] ``TechnicalStats`` 14 필드 (기존 12 + Marking, Technique, LongThrows 신규 / shooting → finishing 통일 / freeKickAccuracy → freeKickTaking)
- [ ] ``MentalStats`` 14 필드 (기존 12 + Bravery, Flair)
- [ ] ``PhysicalStats`` 8 필드 (jumping → jumpingReach 명칭만)
- [ ] ``GoalkeepingStats`` 13 필드 (기존 10 + FirstTouchGk, PassingGk, PunchingTendency)
- [ ] ``ApplyToAll`` 헬퍼 갱신
- [ ] StatsTests 갱신 (카테고리별 4 → 49 필드 검증)

## DoD
- [ ] Stats 49 필드 / 직렬화 라운드트립 OK
"@ }

    @{ Key="B.2"; Title="[Data] 스탯 스케일 1-100 + GameBalanceSO 재산정"; Type="Task"; Labels="area:domain,area:data"; Priority="P0"; Size="S";
       Body=@"
## 목표
스탯 스케일 1-20 → 1-100 으로 변경. 외부화 수치 ~25 필드 재산정.

## 명세
- v1.0-tasks.md Stage B / Task B.2
- design-decisions.md #39
- algorithms.md V1.0-1

## 작업 내용
- [ ] ``statMeanAtCAFloor`` 5→25 / ``statMeanAtCACeil`` 17→85 / ``statStdDev`` 2→10
- [ ] ``statEmphasisBonus`` 2→10 / ``statEmphasisPenalty`` 1→5
- [ ] ``gkSecondaryStatPenalty`` 2→10 / ``gkOutfieldStatPenalty`` 8→40
- [ ] ``ClampStat`` 1-100
- [ ] 시드 자산 (Balance/GameBalance.asset) Unity reimport

## DoD
- [ ] PlayerGenerator 1-100 stats 정상 생성
- [ ] algorithms.md #1 Test Scenarios T1-T7 1-100 기준 갱신
"@ }

    @{ Key="B.3"; Title="[Domain] Hidden Attributes 도입 + PlayerGen 통합"; Type="Feature"; Labels="area:domain"; Priority="P0"; Size="S";
       Body=@"
## 목표
``Player.hiddenAttrs`` 도입 + PlayerGenerator 5단계 Hidden 추첨 통합.

## 명세
- v1.0-tasks.md Stage B / Task B.3
- design-decisions.md #40
- algorithms.md V1.0-1 (5단계)

## 작업 내용
- [ ] Task A.4 의 ``HiddenAttributes`` 본격 활용
- [ ] PlayerGenerator 5단계 Hidden 추첨 추가
- [ ] ``GameBalanceSO.hiddenAttrMean = 50 / hiddenAttrStdDev = 15`` 추가
- [ ] 트레잇 가산점 (BigMatch → pressureHandling +20, InjuryProne → injuryProneness +30 등)
- [ ] EditMode 테스트 (Hidden 분포 / 트레잇 가산점)

## DoD
- [ ] 100명 생성 → Hidden 9 필드 평균 ~50, std ~15
- [ ] 트레잇 보유 선수 hidden 값 명세대로 가산
"@ }

    @{ Key="B.4"; Title="[Domain] Absolute / Relative 스탯 메타"; Type="Task"; Labels="area:domain"; Priority="P1"; Size="XS";
       Body=@"
## 목표
FM 표준 Absolute (인성 기반, 훈련 거의 X) vs Relative (훈련 성장) 분리.

## 명세
- v1.0-tasks.md Stage B / Task B.4
- design-decisions.md #40

## 작업 내용
- [ ] ``Utils/StatMetadata.cs`` 신규 — stat name → Absolute/Relative 매핑 (하드코딩)
- [ ] 10 Absolute stats (Determination / Work Rate / Leadership / Flair / Bravery / Aggression / Concentration / Natural Fitness / Composure / Decisions)

## DoD
- [ ] ``StatMetadata.IsAbsolute(""determination"") == true``
- [ ] ``StatMetadata.IsAbsolute(""passing"") == false``
"@ }

    @{ Key="B.5"; Title="[UI] 항상 세부 스탯 노출 정책"; Type="Task"; Labels="area:ui"; Priority="P1"; Size="S";
       Body=@"
## 목표
``isDebugMode`` 무관 자기 구단 선수 stats 정확 수치 노출. 가챠 화면만 5단계 티어 유지.

## 명세
- v1.0-tasks.md Stage B / Task B.5
- design-decisions.md #39 / v1.0-plan.md §3.14.2

## 작업 내용
- [ ] ``PlayerProfileController.cs`` 갱신 — 자기 구단 정확 수치
- [ ] 타 구단 → 스카우트 명단 진입 정도에 따라 노출 (Stage E 연동, 일단 정확 수치 임시)
- [ ] Squad 화면 stat 카테고리별 표시 갱신

## DoD
- [ ] 가챠 화면만 5단계 티어
- [ ] 나머지 정확 수치 1-100 표시
"@ }

    # ─── Stage C: 트레잇 재정의 (3) ──────────────────────────
    @{ Key="C.1"; Title="[Domain] TraitSO 효과 시스템 (TraitEffect)"; Type="Feature"; Labels="area:domain"; Priority="P0"; Size="S";
       Body=@"
## 목표
TraitSO 에 ``effects: List<TraitEffect>`` 추가. 라벨만 부여하던 V0.1 → 본격 효과.

## 명세
- v1.0-tasks.md Stage C / Task C.1
- design-decisions.md #41

## 작업 내용
- [ ] ``TraitEffect.cs`` + ``TraitEffectType`` enum (MatchModifier / GrowthRateModifier / InjuryRateModifier / MoralePropensity / MarketValueModifier)
- [ ] ``TraitSO.effects`` 필드 추가

## DoD
- [ ] TraitSO 인스펙터에서 effects 편집 가능
"@ }

    @{ Key="C.2"; Title="[Data] V1.0 신규 트레잇 ~14 정의"; Type="Task"; Labels="area:data"; Priority="P1"; Size="S";
       Body=@"
## 목표
V0.1 6 트레잇 + V1.0 ~14 신규 = ~20 카탈로그.

## 명세
- v1.0-tasks.md Stage C / Task C.2
- design-decisions.md #41

## 작업 내용
- [ ] V1.0 신규 트레잇 ~14 (클러치 / 무리한패스 / 와이드플레이 / 자국인우대 / 유리몸 / 철인 / 멘탈약자 / 슈퍼유망주 / 멀티포지션 / 골결정력 / 수비형윙백 / 정신적리더 / 페널티스페셜리스트 / 프리킥마이스터)
- [ ] 각 trait effects 매핑
- [ ] SeedV10Data 시드 스크립트 추가

## DoD
- [ ] ~20 TraitSO asset, 각 effects 채워짐
"@ }

    @{ Key="C.3"; Title="[Data] 트레잇 충돌 그룹 확장 (Group 2, 3)"; Type="Task"; Labels="area:data"; Priority="P1"; Size="XS";
       Body=@"
## 목표
충돌 그룹 V0.1 1개 → V1.0 3개.

## 명세
- v1.0-tasks.md Stage C / Task C.3
- design-decisions.md #41

## 작업 내용
- [ ] Group 2 (Durability — 유리몸 / 철인) 신규
- [ ] Group 3 (PressureMentality — 빅매치형 / 멘탈약자) 신규
- [ ] TraitSO.exclusionGroupId 갱신

## DoD
- [ ] PlayerGenerator 트레잇 추첨 시 같은 그룹 X
"@ }

    # ─── Stage D: 시설 시스템 (5) ────────────────────────────
    @{ Key="D.1"; Title="[Data] FacilityType 8종 + FacilityLevelSO 80 asset"; Type="Feature"; Labels="area:data"; Priority="P0"; Size="M";
       Body=@"
## 목표
FacilityType V0.1 3종 → V1.0 8종 확장. FacilityLevelSO 80 asset (8 × 10).

## 명세
- v1.0-tasks.md Stage D / Task D.1
- design-decisions.md #49

## 작업 내용
- [ ] ``enum FacilityType`` 8종 (Scout / Training / YouthCoach / YouthRecruitment / YouthFacility / Medical / Stadium / Gym)
- [ ] FacilityLevelSO 80 asset
- [ ] 비용 곡선 ``baseCost × pow(level, 2.5)``
- [ ] 효과 필드 정의 (Training.growthRateBonus / Medical.recoverySpeedBonus / Stadium.incomeMultiplier 등)
- [ ] Editor 시드 스크립트 갱신

## DoD
- [ ] 80 asset 자동 생성 + 인스펙터 정확
"@ }

    @{ Key="D.2"; Title="[Domain] Facilities 8 필드 분리"; Type="Task"; Labels="area:domain"; Priority="P0"; Size="S";
       Body=@"
## 목표
V0.1 ``scoutLevel / trainingLevel / youthLevel`` → V1.0 8 필드.

## 명세
- v1.0-tasks.md Stage D / Task D.2 / design-decisions.md #49

## 작업 내용
- [ ] ``Facilities`` 8 필드 (모두 디폴트 1)
- [ ] ClubGenerator 새 구단 생성 시 8 필드 채움

## DoD
- [ ] 모든 신규 구단 8 필드 정상 초기화
"@ }

    @{ Key="D.3"; Title="[Feature] 시설 병렬 업그레이드"; Type="Feature"; Labels="area:domain"; Priority="P0"; Size="S";
       Body=@"
## 목표
자금만 있으면 N개 시설 동시 업그레이드. 같은 시설은 한 번에 1단계.

## 명세
- v1.0-tasks.md Stage D / Task D.3 / design-decisions.md #49

## 작업 내용
- [ ] ``FacilitySystem.UpgradeFacility`` — 차단 로직 제거
- [ ] ``Facilities.activeUpgrades: List<FacilityUpgrade>`` (V0.1 3 필드 → 가변)
- [ ] ``DailyProcessor.ProcessUpgrades`` 다중 진행 처리

## DoD
- [ ] 자금만 있으면 동시 N개 발주 가능
"@ }

    @{ Key="D.4"; Title="[Feature] 시설 효과 본격 도입 (V0.1 미구현)"; Type="Feature"; Labels="area:domain"; Priority="P0"; Size="L";
       Body=@"
## 목표
V0.1 = 효과 없음. V1.0 = 각 시설 본격 효과.

## 명세
- v1.0-tasks.md Stage D / Task D.4 / design-decisions.md #49

## 작업 내용
- [ ] Training → 성장 시스템 (별도 또는 PlayerGenerator 확장)
- [ ] Medical → MatchPostProcessor 부상 회복 보정
- [ ] Stadium → SeasonEndProcessor 재정 결산 입장료 (Stage M.6)
- [ ] Gym → 피지컬 stat 성장률 + 부상 회복 일부
- [ ] Scout → ScoutingSystem (Stage E 와 짝)
- [ ] YouthCoach / YouthRecruitment / YouthFacility → YouthSystem (Stage L)

## DoD
- [ ] 각 시설 효과 해당 시스템 정상 적용 (T별 검증)
"@ }

    @{ Key="D.5"; Title="[UI] FacilityScene V1.0 갱신"; Type="Feature"; Labels="area:ui"; Priority="P1"; Size="M";
       Body=@"
## 목표
FacilityScene UI 8 시설 + 1-10 등급 + 진행 중 목록 표시.

## 명세
- v1.0-tasks.md Stage D / Task D.5

## 작업 내용
- [ ] 8 시설 표시
- [ ] 등급 1/10 + 다음 등급 효과 + 비용 + 완료일
- [ ] 진행 중 업그레이드 목록

## DoD
- [ ] UI 에서 8 시설 정상 표시 + 발주 가능
"@ }

    # ─── Stage E: 스카우트 시스템 (4) ────────────────────────
    @{ Key="E.1"; Title="[Domain] ScoutReport + Club.scoutingKnowledge"; Type="Task"; Labels="area:domain"; Priority="P0"; Size="S";
       Body=@"
## 목표
Task A.4 의 ScoutReport / CaPaEstimate / HiddenAttributesPartial 본격 활용.

## 명세
- v1.0-tasks.md Stage E / Task E.1
- design-decisions.md #46

## 작업 내용
- [ ] ``Club.scoutingKnowledge`` Dictionary 초기화 로직
- [ ] 자기 구단 자동 등록 (scoutLevel 100)

## DoD
- [ ] ScoutReport 직렬화 라운드트립 OK
"@ }

    @{ Key="E.2"; Title="[Simulation] ScoutingSystem 명단 관리"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="M";
       Body=@"
## 목표
시설 등급별 명단 크기 / 정확도 / 자동 추가.

## 명세
- v1.0-tasks.md Stage E / Task E.2
- design-decisions.md #46

## 작업 내용
- [ ] ``Application/ScoutingSystem.cs`` 신규 — ``UpdateKnowledge(state, balance)``
- [ ] 자기 리그 → Lv2 이상 자동 추가
- [ ] 타 리그 → 시설 + 시간 누적
- [ ] DailyProcessor 매주 호출
- [ ] ``FacilityLevelSO(Scout).scoutPoolSize / scoutAccuracyRange`` 외부화

## DoD
- [ ] Lv5 클럽 → 명단 ~3000명, Lv1 → ~50명
"@ }

    @{ Key="E.3"; Title="[Transfer] SearchPlayers 가시성 분기 (이분법)"; Type="Feature"; Labels="area:transfer"; Priority="P0"; Size="M";
       Body=@"
## 목표
명단 ∈ → 정확 / 명단 ∉ → 정성적 라벨 5단계 (Q4 확정).

## 명세
- v1.0-tasks.md Stage E / Task E.3
- design-decisions.md #46

## 작업 내용
- [ ] 명단 ∈ → 정확 CA/PA + stats + 트레잇 + Hidden 노출
- [ ] 명단 ∉ → 이름/구단/포지션/나이/국적 + 정성적 라벨 (매우높음/높음/중간/낮음/매우낮음). Hidden 비공개.
- [ ] ``requireScouted`` 폐기 — 모든 선수 검색 가능
- [ ] 디버그 모드 무관 모두 정확 노출

## DoD
- [ ] Transfer 검색 결과 명단 ∈ vs ∉ 표시 차이 확인
"@ }

    @{ Key="E.4"; Title="[UI] Transfer 검색 결과 시각화 (명단 ∈ / ∉)"; Type="Feature"; Labels="area:ui,area:transfer"; Priority="P1"; Size="M";
       Body=@"
## 목표
검색 결과 명단 진입 여부 시각적 표시.

## 명세
- v1.0-tasks.md Stage E / Task E.4

## 작업 내용
- [ ] 명단 ∈ = 배경 강조 / 명단 ∉ = 회색조 + 자물쇠 아이콘
- [ ] 정성적 라벨 표시

## DoD
- [ ] UI 명단 가시성 차이 명확
"@ }

    # ─── Stage F: AI 영입 (2) ────────────────────────────────
    @{ Key="F.1"; Title="[Simulation] CpuTransferAi 필요 기반 트리거"; Type="Feature"; Labels="area:simulation,area:transfer"; Priority="P0"; Size="L";
       Body=@"
## 목표
AI 구단 능동 영입 의사결정. 필요 기반 트리거 5종 (Q3).

## 명세
- v1.0-tasks.md Stage F / Task F.1
- algorithms.md V1.0-5
- design-decisions.md #47

## 작업 내용
- [ ] ``Application/CpuTransferAi.cs`` 신규 — ``Run(state, balance)``
- [ ] 5 트리거 (약점 라인 / 부상자 / FA 임박 / 약속 미이행 / 자금 여유)
- [ ] 후보 추첨 + ``TransferSystem.SubmitOffer`` 호출
- [ ] 외부화 ~6 필드 (aiWeaknessRatioThreshold / aiCoreInjuryWeeksThreshold / aiSavingsThreshold 등)

## DoD
- [ ] EditMode T1~T7 (algorithms.md V1.0-5)
"@ }

    @{ Key="F.2"; Title="[Infra] EventScheduler 매주 CpuTransferAi 호출"; Type="Task"; Labels="area:simulation"; Priority="P0"; Size="XS";
       Body=@"
## 목표
EventScheduler 가 매주 (월요일) ``CpuTransferAi.Run`` 호출.

## 명세
- v1.0-tasks.md Stage F / Task F.2

## DoD
- [ ] 시즌 진행 시 AI 구단 오퍼 자연 발생 (state.activeOffers 갱신)
"@ }

    # ─── Stage G: 사기 / 약속 (4) ────────────────────────────
    @{ Key="G.1"; Title="[Simulation] MoraleSystem 변동 트리거 + Hidden 보정"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="L";
       Body=@"
## 목표
Morale + Happiness 분리 + 변동 트리거 본격 도입. Hidden 보정 (loyalty / ambition / professionalism).

## 명세
- v1.0-tasks.md Stage G / Task G.1
- algorithms.md V1.0-6
- design-decisions.md #42

## 작업 내용
- [ ] ``Application/MoraleSystem.cs`` 신규 (Tick / OnMatchFinished / OnTransferCompleted / OnContractRenewed / OnPromiseFulfilled / OnPromiseBroken / OnInterview)
- [ ] 변동 매트릭스 (algorithms.md V1.0-6)
- [ ] MatchPostProcessor 통합 (V0.1 미구현 → V1.0 본격)
- [ ] DailyProcessor Tick 호출

## DoD
- [ ] T1~T8 통과 (algorithms.md V1.0-6)
"@ }

    @{ Key="G.2"; Title="[Simulation] PromiseSystem 4종 + 면담 UI"; Type="Feature"; Labels="area:simulation,area:ui"; Priority="P0"; Size="L";
       Body=@"
## 목표
Promise 4종 (PlaytimeAgreement / TransferIn / Renewal / TransferOut) + 면담 4-6 멘트.

## 명세
- v1.0-tasks.md Stage G / Task G.2
- design-decisions.md #43

## 작업 내용
- [ ] ``Application/PromiseSystem.cs`` 신규 — CheckProgress
- [ ] ``Promise.cs`` 4 종류
- [ ] ``GameState.activePromises / nextPromiseId``
- [ ] DailyProcessor 매주 진행 체크
- [ ] 면담 UI (PlayerProfile 의 [면담] 버튼 + 4-6 멘트)

## DoD
- [ ] Promise 생성 → deadline 도래 → status 확정 → 사기 변동
"@ }

    @{ Key="G.3"; Title="[Simulation] 라커룸 분위기"; Type="Feature"; Labels="area:simulation"; Priority="P1"; Size="S";
       Body=@"
## 목표
``Club.season.dressingRoomMood`` 갱신 + 매치 영향.

## 명세
- v1.0-tasks.md Stage G / Task G.3
- design-decisions.md #42

## 작업 내용
- [ ] 월 1회 갱신 (1군 happiness 평균 + 캡틴 leadership 가산)
- [ ] < 30 → 시즌 폼 -5 보정 (MatchSimulator 입력)

## DoD
- [ ] EditMode T8 (라커룸 < 30 → 매치 영향)
"@ }

    @{ Key="G.4"; Title="[Transfer] TransferRequest 자동 트리거 + 유저 승인"; Type="Feature"; Labels="area:transfer,area:simulation"; Priority="P1"; Size="S";
       Body=@"
## 목표
Happiness < 20 → 자동 ``TransferRequestEvent`` + Dashboard 인박스 + 유저 응답.

## 명세
- v1.0-tasks.md Stage G / Task G.4 (Q9 자동 + 승인 패턴)

## 작업 내용
- [ ] Happiness < 20 → TransferRequestEvent 발행
- [ ] Dashboard 인박스 표시
- [ ] 유저 응답 (수락 / 거절 / 면담)

## DoD
- [ ] Happiness 강제 < 20 → 자동 알림 + UI 표시
"@ }

    # ─── Stage H: 계약 (3) ──────────────────────────────────
    @{ Key="H.1"; Title="[Transfer] RenewContract + AI 응답 (상시 재계약)"; Type="Feature"; Labels="area:transfer"; Priority="P0"; Size="M";
       Body=@"
## 목표
``TransferSystem.RenewContract`` 신규. 시점 제약 X. 사용자 피드백 2.5.

## 명세
- v1.0-tasks.md Stage H / Task H.1
- algorithms.md V1.0-3.1

## 작업 내용
- [ ] ``RenewContract`` 메서드 신규
- [ ] AI 응답 (loyalty / ambition / 잔여 기간 가산점)
- [ ] 사기 회복 (contractRenewalMoraleBoost / contractRenewalHappinessBoost)
- [ ] ``ContractRenewedEvent / ContractRenewalRejectedEvent`` 신규

## DoD
- [ ] 주급 ×1.5 재계약 → 거의 무조건 수락
- [ ] morale +15 / happiness +25
"@ }

    @{ Key="H.2"; Title="[Domain] Contract 옵션 확장 (Bonus + Release)"; Type="Feature"; Labels="area:transfer,area:domain"; Priority="P1"; Size="S";
       Body=@"
## 목표
Contract 보너스 필드 확장 + release clause 활성화.

## 명세
- v1.0-tasks.md Stage H / Task H.2

## 작업 내용
- [ ] ``signingBonus / loyaltyBonus / appearanceBonus / goalBonus`` (Task A.4 와 짝)
- [ ] UI 협상 화면에서 입력
- [ ] release clause 활성화 — amount ≥ clause → 강제 Accepted

## DoD
- [ ] release clause 시나리오 검증
"@ }

    @{ Key="H.3"; Title="[Transfer] 자유계약 (FA) 시장 — 잔여 6개월"; Type="Feature"; Labels="area:transfer"; Priority="P1"; Size="S";
       Body=@"
## 목표
잔여 6개월 이내 선수 → 타 구단 직접 계약 제안 가능 (보스만 룰).

## 명세
- v1.0-tasks.md Stage H / Task H.3

## 작업 내용
- [ ] ``TransferSystem.SubmitFreeAgentContract`` 신규
- [ ] 이적료 0, 사이닝 보너스만

## DoD
- [ ] FA 영입 시나리오 검증
"@ }

    # ─── Stage I: 매치 엔진 재작성 (9, V1.0 최대) ─────────────
    @{ Key="I.1"; Title="[Simulation] 매치 엔진 분 단위 골격 (인터페이스 유지)"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="L";
       Body=@"
## 목표
MatchSimulator 인터페이스 유지 / 내부 분 단위 step 루프 재작성.

## 명세
- v1.0-tasks.md Stage I / Task I.1
- algorithms.md V1.0-2
- design-decisions.md #44

## 작업 내용
- [ ] ``Simulate(match, state, balance) → MatchResult`` 시그니처 유지
- [ ] 분 단위 step 루프 (1~90)
- [ ] 시드 결정성 (algorithms.md V1.0-2 1단계)

## DoD
- [ ] T1 결정성 통과
"@ }

    @{ Key="I.2"; Title="[Simulation] 매치 이벤트 종류 + 발생 확률 + Stat 직접 참조"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="L";
       Body=@"
## 목표
Shot / Save / Foul / Card / Injury / Substitution / Pass / Cross / KeyPass / OffsidesCalled / Goal / Assist. stat 직접 참조 (finishing × composure 등).

## 명세
- v1.0-tasks.md Stage I / Task I.2
- algorithms.md V1.0-2 이벤트 표

## DoD
- [ ] EditMode 시뮬 시 이벤트 분포 명세 범위
"@ }

    @{ Key="I.3"; Title="[Simulation] 부상 / 카드 / 출장 정지 시스템"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="M";
       Body=@"
## 목표
InjuryTypeSO ~15 카탈로그 활용. 옐로 5장 정지. 레드 1-3경기.

## 명세
- v1.0-tasks.md Stage I / Task I.3

## 작업 내용
- [ ] 부상 발생 → InjuryInfo + PlayerInjuredEvent
- [ ] 옐로 5장 → suspendedMatches=1
- [ ] 레드 → suspendedMatches 1-3
- [ ] starting11 선정 시 injury / suspendedMatches 제외

## DoD
- [ ] T2 부상 빈도 / T3 카드 누적 / T4 정지 검증
"@ }

    @{ Key="I.4"; Title="[Simulation] 평점 시스템"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="S";
       Body=@"
## 목표
이벤트별 가산점 (골 +1.0 / 어시 +0.5 / 옐로 -0.3 / 레드 -1.5). 기본 6.5.

## 명세
- v1.0-tasks.md Stage I / Task I.4 / algorithms.md V1.0-2 평점 표

## DoD
- [ ] T5 평점 계산 정확
"@ }

    @{ Key="I.5"; Title="[Simulation/UI] 매치 텍스트 이벤트 (유저 매치 한정)"; Type="Feature"; Labels="area:simulation,area:ui"; Priority="P0"; Size="M";
       Body=@"
## 목표
유저 구단 매치 한정 텍스트 이벤트 ~15-20 (Q5 핵심만).

## 명세
- v1.0-tasks.md Stage I / Task I.5

## 작업 내용
- [ ] Match.events 본격 채움 (유저 매치)
- [ ] 핵심 이벤트만 (Shot / Goal / Card / Injury / Sub / KeyPass)
- [ ] MatchEvent.textKey / textArgs String Table 활용
- [ ] LocalizationSO 매치 이벤트 텍스트 ~30 키 추가

## DoD
- [ ] T9 텍스트 이벤트 분량 검증
"@ }

    @{ Key="I.6"; Title="[Simulation] SubstitutionAI 자동 교체"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="M";
       Body=@"
## 목표
fatigue 70+ / Injury / 스코어 상황 기반 자동 교체.

## 명세
- v1.0-tasks.md Stage I / Task I.6

## 작업 내용
- [ ] ``Application/SubstitutionAI.cs`` 신규 — DecideSubstitution
- [ ] PlayerMatchStat.minutesPlayed 가변

## DoD
- [ ] EditMode 매치 시뮬 시 자동 교체 발생
"@ }

    @{ Key="I.7"; Title="[Simulation] SimulateLite 경량 경로 (비활성 매치)"; Type="Feature"; Labels="area:simulation"; Priority="P1"; Size="S";
       Body=@"
## 목표
비활성 구단 매치 = V0.1 단순 Poisson + 라인 가중 재활용. events 비움.

## 명세
- v1.0-tasks.md Stage I / Task I.7

## 작업 내용
- [ ] ``MatchSimulator.SimulateLite`` 분리
- [ ] BackgroundSimulator.SimulateDay 분기 (활성/비활성)
- [ ] ``MatchPostProcessor.Process(..., publishEvent: false)`` 옵션

## DoD
- [ ] T4 events 비움 / playerStats 22명만
"@ }

    @{ Key="I.8"; Title="[Simulation] 외부 영향 (form / morale / fatigue) 매치 보정"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="S";
       Body=@"
## 목표
effectiveCA 곱셈 보정 적용.

## 명세
- v1.0-tasks.md Stage I / Task I.8 / algorithms.md V1.0-2 외부 영향

## 작업 내용
- [ ] effectiveCA = CA × (1+form-50/200) × (1+morale-50/200) × max(0.5, 1-fatigue/200)
- [ ] formCoefficient / moraleCoefficient / fatigueCoefficient 외부화

## DoD
- [ ] T7 외부 영향 검증
"@ }

    @{ Key="I.9"; Title="[Simulation] strengthExponent 폐기"; Type="Task"; Labels="area:simulation"; Priority="P1"; Size="XS";
       Body=@"
## 목표
V0.1 임시 변통 폐기 (design-decisions.md #33 보강).

## 명세
- v1.0-tasks.md Stage I / Task I.9

## 작업 내용
- [ ] ``GameBalanceSO.strengthExponent`` 제거
- [ ] algorithms.md #33 보강 → 폐기 표시
- [ ] 기존 외부화 자산 reimport

## DoD
- [ ] strengthExponent 참조 0건
- [ ] 매치 결과 결정성 유지
"@ }

    # ─── Stage J: 전술 (7) ──────────────────────────────────
    @{ Key="J.1"; Title="[Data] FormationSO 추출 + 5-6 카탈로그"; Type="Task"; Labels="area:data"; Priority="P0"; Size="S";
       Body=@"
## 목표
``GameBalanceSO.formation`` nested → ``FormationSO`` 추출. 5-6 카탈로그.

## 명세
- v1.0-tasks.md Stage J / Task J.1
- design-decisions.md #45

## 작업 내용
- [ ] FormationSO 추출
- [ ] 5-6 카탈로그 (4-4-2 / 4-3-3 / 3-5-2 / 4-2-3-1 / 4-4-1-1 / 5-3-2)
- [ ] ``LeagueConfigSO.availableFormations`` 검토

## DoD
- [ ] 5-6 FormationSO asset
"@ }

    @{ Key="J.2"; Title="[Simulation] PlayerRoleSO ~40 + Tactic 도메인"; Type="Feature"; Labels="area:simulation,area:domain"; Priority="P0"; Size="L";
       Body=@"
## 목표
PlayerRoleSO ~40 + Club.tactic 본격 활용.

## 명세
- v1.0-tasks.md Stage J / Task J.2
- design-decisions.md #45

## 작업 내용
- [ ] PlayerRoleSO ~40 카탈로그 (Task A.5 와 짝)
- [ ] 각 Role.eventModifiers 채움 (Poacher.Shot=1.5 등)
- [ ] Tactic 도메인 본격 활용
- [ ] ClubGenerator 자동 Tactic 생성 (디폴트 4-4-2 + 자동 Role)

## DoD
- [ ] Tactic 직렬화 라운드트립 + 매치 시뮬 입력 가능
"@ }

    @{ Key="J.3"; Title="[Simulation] Mentality 7단계 + 외부화"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="S";
       Body=@"
## 목표
``enum Mentality`` 7단계 + 매치 시뮬 곱셈 보정. Team Instructions 는 V1.x.

## 명세
- v1.0-tasks.md Stage J / Task J.3

## 작업 내용
- [ ] enum 7단계 (VeryDefensive ~ VeryAttacking)
- [ ] ``GameBalanceSO.mentalityModifiers[7]``
- [ ] Team Instructions 는 V1.x placeholder 만

## DoD
- [ ] Mentality 7단계 매치 시뮬 입력 정상
"@ }

    @{ Key="J.4"; Title="[Simulation] TacticImpact (Role × Duty × Mentality × Stat)"; Type="Feature"; Labels="area:simulation"; Priority="P0"; Size="M";
       Body=@"
## 목표
``Application/TacticImpact.cs`` — Tactic + Stats → 이벤트 가중치.

## 명세
- v1.0-tasks.md Stage J / Task J.4
- algorithms.md V1.0-7

## 작업 내용
- [ ] ``ComputeEventWeight(tactic, playerId, state, eventType, balance)``
- [ ] Role × Duty × Mentality × Stat 곱셈
- [ ] MatchSimulator 통합 — 이벤트 주체 선수 추첨 시 호출

## DoD
- [ ] T1~T4 (algorithms.md V1.0-7)
"@ }

    @{ Key="J.5"; Title="[UI] LineupScene + TacticScene 신규"; Type="Feature"; Labels="area:ui"; Priority="P0"; Size="L";
       Body=@"
## 목표
Formation / Role / Duty / Mentality / Set Pieces UI.

## 명세
- v1.0-tasks.md Stage J / Task J.5

## 작업 내용
- [ ] TacticScene — Formation + 11 슬롯 Role/Duty + Mentality
- [ ] LineupScene — 11명 수동 배정
- [ ] 자동 라인업 버튼 (Role 호환 + top CA + 폼/사기 가산 + 부상/정지 제외)
- [ ] Set Pieces 담당자 UI

## DoD
- [ ] 사용자가 Tactic 편집 → MatchSimulator 적용
"@ }

    @{ Key="J.6"; Title="[Simulation] 캡틴 / 부캡틴 시스템"; Type="Feature"; Labels="area:simulation"; Priority="P1"; Size="S";
       Body=@"
## 목표
캡틴 / 부캡틴 자동 + 수동 변경 + 라커룸 효과.

## 명세
- v1.0-tasks.md Stage J / Task J.6
- design-decisions.md #45

## 작업 내용
- [ ] ``Club.season.captainPlayerId / viceCaptainPlayerId``
- [ ] 자동 (leadership + age + 계약 잔여) / 수동 변경
- [ ] 라커룸 분위기 +5 가산점
- [ ] Squad 화면 [캡틴 임명] 버튼

## DoD
- [ ] 캡틴 변경 가능 + 라커룸 효과 검증
"@ }

    @{ Key="J.7"; Title="[Gacha] 초기 스쿼드 가챠 시 포메이션 랜덤화"; Type="Feature"; Labels="area:gacha"; Priority="P1"; Size="S";
       Body=@"
## 목표
ClubGenerator 또는 StartingSquadGacha 가 포메이션 추첨.

## 명세
- v1.0-tasks.md Stage J / Task J.7
- design-decisions.md #32, #45

## 작업 내용
- [ ] 포메이션 추첨 (빅클럽 = 화려한 ↑ / 약체 = 견고 ↑)
- [ ] ClubGen 분배표 = 추첨된 FormationSO 기준

## DoD
- [ ] 20 구단 포메이션 다양성 검증 (EditMode T)
"@ }

    # ─── Stage K: 협상 / 임대 (5) ────────────────────────────
    @{ Key="K.1"; Title="[Transfer] CounterOffer + 다중 라운드 협상"; Type="Feature"; Labels="area:transfer"; Priority="P0"; Size="M";
       Body=@"
## 목표
AI 응답 4분기 (Accepted / CounterOffer / Rejected / Mocking) + 다중 라운드 (최대 3).

## 명세
- v1.0-tasks.md Stage K / Task K.1
- algorithms.md V1.0-3.1

## 작업 내용
- [ ] ``OfferStatus.CounterOffer`` 활성화
- [ ] AiRespondToOffer 4분기
- [ ] ``RespondToCounterOffer`` 메서드
- [ ] ``maxNegotiationRounds = 3``

## DoD
- [ ] CounterOffer 시나리오 검증
"@ }

    @{ Key="K.2"; Title="[Transfer] 선수 개인 협상 (Negotiating)"; Type="Feature"; Labels="area:transfer"; Priority="P0"; Size="M";
       Body=@"
## 목표
AI 판매 Accepted → ``OfferStatus.Negotiating`` 단계 도입 (V0.1 자동 통과 → V1.0 단계).

## 명세
- v1.0-tasks.md Stage K / Task K.2

## 작업 내용
- [ ] 선수 응답 (loyalty / ambition / 주급 / 출전시간 약속)
- [ ] ``EstimateInitialWage`` 헬퍼

## DoD
- [ ] EditMode T (loyalty 80 vs 20 거절률 차이)
"@ }

    @{ Key="K.3"; Title="[Transfer/Domain] 임대 시스템 (Loan)"; Type="Feature"; Labels="area:transfer,area:domain"; Priority="P0"; Size="L";
       Body=@"
## 목표
임대 영입 / 종료 / 자동 복귀.

## 명세
- v1.0-tasks.md Stage K / Task K.3
- algorithms.md V1.0-3.1
- design-decisions.md #48

## 작업 내용
- [ ] ``TransferOffer.isLoan / loanFee / loanWageShare / loanEndDate``
- [ ] ``Player.parentClubId / loanEndDate`` (Task A.4 와 짝)
- [ ] ``TransferSystem.SubmitLoanOffer``
- [ ] CompleteTransfer 분기 (임대 vs 영구)
- [ ] DailyProcessor 임대 종료 자동 복귀
- [ ] ``LoanReturnedEvent`` 신규

## DoD
- [ ] 임대 → 종료 → 자동 복귀 라운드트립
"@ }

    @{ Key="K.4"; Title="[Transfer] 트랜스퍼 리스트 활성화"; Type="Feature"; Labels="area:transfer"; Priority="P1"; Size="S";
       Body=@"
## 목표
``Player.state.transferListed`` 본격 활용 + 시장가 ×0.7 + CpuTransferAi 우선순위 ↑.

## 명세
- v1.0-tasks.md Stage K / Task K.4

## DoD
- [ ] TransferRequest → 유저 수락 → transferListed → AI 영입 가시화
"@ }

    @{ Key="K.5"; Title="[UI] NegotiationScene 협상 화면"; Type="Feature"; Labels="area:ui,area:transfer"; Priority="P0"; Size="M";
       Body=@"
## 목표
협상 진행 + 라운드 / amount / status 표시 + CounterOffer 응답.

## 명세
- v1.0-tasks.md Stage K / Task K.5

## 작업 내용
- [ ] 협상 진행 화면
- [ ] CounterOffer 응답 옵션 (수락 / 거절 / 재역제안)
- [ ] 선수 협상 단계 표시

## DoD
- [ ] UI 협상 전 흐름 동작
"@ }

    # ─── Stage L: 유스 (7) ──────────────────────────────────
    @{ Key="L.1"; Title="[Youth] 유스 CA 캡 + PA 분포 조정 (사용자 피드백)"; Type="Task"; Labels="area:youth"; Priority="P0"; Size="S";
       Body=@"
## 목표
유스 CA 캡 ~95. 사용자 피드백 2.2 "아무리 높아도 100 정도".

## 명세
- v1.0-tasks.md Stage L / Task L.1
- algorithms.md V1.0-4
- design-decisions.md #50

## 작업 내용
- [ ] ``GameBalanceSO.youthMinCa = 30 / youthMaxCa = 95 / youthCaGapMean = 60``
- [ ] YouthSystem.GenerateIntake — CA 캡 적용
- [ ] PlayerGenerator ``forceCa / forcePa`` 파라미터 추가

## DoD
- [ ] EditMode T (유스 100명 → 모든 CA ≤ 95)
"@ }

    @{ Key="L.2"; Title="[Youth/UI] 풀 전체 영입 + 시설 인원 제한 (사용자 피드백)"; Type="Feature"; Labels="area:youth,area:ui"; Priority="P1"; Size="S";
       Body=@"
## 목표
풀 전체 영입 가능. 인원 제한 = 시설 등급 비례. 사용자 피드백 2.2.

## 명세
- v1.0-tasks.md Stage L / Task L.2

## 작업 내용
- [ ] ``YouthSystem.SignPlayers`` — maxSign 검증
- [ ] UI YouthScene "전체 선택" 버튼

## DoD
- [ ] Lv5 시설 = 풀 전체 / Lv1 = 풀 33% 만
"@ }

    @{ Key="L.3"; Title="[Youth/Data] 유스 시설 3분리 활용 (Coach / Recruitment / Facility)"; Type="Task"; Labels="area:youth,area:data"; Priority="P0"; Size="S";
       Body=@"
## 목표
Task A.5 / D.1 의 분리된 시설을 YouthSystem 에서 각각 활용.

## 명세
- v1.0-tasks.md Stage L / Task L.3
- design-decisions.md #50

## 작업 내용
- [ ] YouthCoach → 평균 PA + 고급 트레잇 가중치
- [ ] YouthRecruitment → 풀 사이즈 + 인스펙션 빈도 (Lv7+ 보조 검토)
- [ ] YouthFacility → 유스 성장률 + 1군 적응

## DoD
- [ ] 각 시설 효과 분리 검증
"@ }

    @{ Key="L.4"; Title="[Youth/Simulation] Mentoring 시스템"; Type="Feature"; Labels="area:youth,area:simulation"; Priority="P0"; Size="L";
       Body=@"
## 목표
베테랑 ↔ 유스 묶음 → Hidden Attributes 수렴.

## 명세
- v1.0-tasks.md Stage L / Task L.4
- algorithms.md V1.0-4 Mentoring
- design-decisions.md #50

## 작업 내용
- [ ] ``Application/MentoringSystem.cs`` 신규 — RunMentoring
- [ ] Hidden Attributes 수렴 (월 1회)
- [ ] MentoringGroup 도메인 활용
- [ ] DailyProcessor 매월 1일 호출
- [ ] MentoringScene UI (그룹 만들기 / 해체)

## DoD
- [ ] Mentor 80 + Mentee 20 → 6개월 후 mentee professionalism +30 정도
"@ }

    @{ Key="L.5"; Title="[Youth/Simulation] 1군 콜업 자동 트리거 + 유저 승인"; Type="Feature"; Labels="area:youth,area:simulation"; Priority="P0"; Size="M";
       Body=@"
## 목표
18세 + CA ≥ 클럽 평균 70% → 자동 알림 + 유저 응답 (Q9).

## 명세
- v1.0-tasks.md Stage L / Task L.5

## 작업 내용
- [ ] ``YouthSystem.CheckPromotionCandidates``
- [ ] ``YouthPromotionSuggestedEvent``
- [ ] Dashboard 인박스 + PlayerProfile [1군 승격] / [거절]

## DoD
- [ ] 18세 + 임계 → 자동 알림 + 유저 응답
"@ }

    @{ Key="L.6"; Title="[Youth/Simulation] 미영입 후보 → AI 다른 구단 영입"; Type="Feature"; Labels="area:youth,area:simulation"; Priority="P1"; Size="S";
       Body=@"
## 목표
``youthRejectedToOtherClubRatio = 0.3`` 확률로 AI 영입.

## 명세
- v1.0-tasks.md Stage L / Task L.6 / algorithms.md V1.0-4

## 작업 내용
- [ ] ``YouthSignedByOtherEvent`` 신규
- [ ] SignPlayers 갱신 — rejected 일부 AI 영입

## DoD
- [ ] 미영입 ~30% AI 다른 구단 영입 / rejectedPlayerIds 보존
"@ }

    @{ Key="L.7"; Title="[Youth] 라운드별 포지션 가중치"; Type="Feature"; Labels="area:youth"; Priority="P1"; Size="S";
       Body=@"
## 목표
V0.1 균등 → V1.0 라운드별 변동.

## 명세
- v1.0-tasks.md Stage L / Task L.7

## 작업 내용
- [ ] ``youthPositionWeightVolatility = 0.5`` 외부화
- [ ] SamplePositionWeights 헬퍼

## DoD
- [ ] 인스펙션마다 포지션 분포 다양성
"@ }

    # ─── Stage M: 시즌 (9) ──────────────────────────────────
    @{ Key="M.1"; Title="[Season] Player.career 시즌 통계 저장"; Type="Feature"; Labels="area:season"; Priority="P0"; Size="S";
       Body=@"
## 목표
사용자 피드백 2.8 — 시즌 통계 저장.

## 명세
- v1.0-tasks.md Stage M / Task M.1

## 작업 내용
- [ ] ``SeasonEndProcessor.Run`` 신규 단계 — 시즌 통계 → Player.career.Add
- [ ] ``SeasonStat`` 확장 (yellowCards / redCards / minutesPlayed / competition)
- [ ] PlayerProfile UI 시즌별 표시

## DoD
- [ ] 1 시즌 완주 후 ``Player.career.Count > 0``
"@ }

    @{ Key="M.2"; Title="[Season] 시상 시스템 (7 AwardType)"; Type="Feature"; Labels="area:season"; Priority="P0"; Size="L";
       Body=@"
## 목표
LeagueMVP / TopScorer / TopAssist / YoungPlayer / BestEleven / GoldenGlove / ManagerOfSeason.

## 명세
- v1.0-tasks.md Stage M / Task M.2
- algorithms.md V1.0-9
- design-decisions.md #51

## 작업 내용
- [ ] ``Application/SeasonAwardSystem.cs`` 신규
- [ ] AwardType 7종
- [ ] ``state.activeAwards / nextAwardId``
- [ ] ``League.history: List<SeasonHistory>``
- [ ] 수상 시 morale / happiness 가산
- [ ] ``AwardWonEvent`` 신규

## DoD
- [ ] EditMode T1~T6 (algorithms.md V1.0-9)
"@ }

    @{ Key="M.3"; Title="[Season] 월간 어워드 (Manager / Player of the Month)"; Type="Feature"; Labels="area:season"; Priority="P1"; Size="M";
       Body=@"
## 목표
매월 1일 직전 월 통계 → 어워드.

## 명세
- v1.0-tasks.md Stage M / Task M.3

## 작업 내용
- [ ] ``SeasonAwardSystem.ComputeMonthlyAwards``
- [ ] DailyProcessor 매월 1일 호출
- [ ] boardConfidence +5 / morale +10

## DoD
- [ ] 월 1회 어워드 + 효과 적용
"@ }

    @{ Key="M.4"; Title="[Season] 보드 평가 + 경질"; Type="Feature"; Labels="area:season"; Priority="P0"; Size="M";
       Body=@"
## 목표
boardConfidence 본격 변동. < 10 → Game Over.

## 명세
- v1.0-tasks.md Stage M / Task M.4
- design-decisions.md #51

## 작업 내용
- [ ] ``Club.season.boardConfidence`` 변동 트리거
- [ ] 매월 평가 / 매치 평가 / 약속 미이행
- [ ] < 30 → ``BoardWarningEvent`` / < 10 → ``ManagerSackedEvent``
- [ ] V1.0 = Game Over

## DoD
- [ ] 강제 < 10 → Game Over 화면
"@ }

    @{ Key="M.5"; Title="[Season] 보드 약속 (시즌 시작)"; Type="Feature"; Labels="area:season"; Priority="P0"; Size="M";
       Body=@"
## 목표
시즌 목표 순위 / 영입 예산 / 매각 예산.

## 명세
- v1.0-tasks.md Stage M / Task M.5

## 작업 내용
- [ ] ``BoardPromise`` 도메인 / ``BoardPromiseType`` enum
- [ ] ``Club.season.boardPromises``
- [ ] NewSeasonProcessor 신규 단계 — 보드 약속 생성 + 매니저 수락/거절 UI
- [ ] 거절 시 boardConfidence -10

## DoD
- [ ] 새 시즌 시작 시 보드 약속 UI 표시
"@ }

    @{ Key="M.6"; Title="[Season] 재정 결산 (입장료 + TV + 상금)"; Type="Feature"; Labels="area:season"; Priority="P0"; Size="M";
       Body=@"
## 목표
시즌 종료 재정 결산. 시설 효과 연동 (Stadium).

## 명세
- v1.0-tasks.md Stage M / Task M.6

## 작업 내용
- [ ] SeasonEndProcessor 신규 단계
- [ ] 입장료 (stadium level × reputation × homeMatches)
- [ ] TV 중계권 (reputation × leaguePosition)
- [ ] 상금 (리그 순위별 차등)
- [ ] Club.finance / transferBudget / wageBudget 갱신

## DoD
- [ ] 시즌 종료 후 클럽 자금 변동 검증
"@ }

    @{ Key="M.7"; Title="[Season/Save] Match 데이터 압축"; Type="Task"; Labels="area:season,area:save"; Priority="P1"; Size="S";
       Body=@"
## 목표
직전 시즌 외 Match events / playerStats 비움.

## 명세
- v1.0-tasks.md Stage M / Task M.7 / design-decisions.md #8

## 작업 내용
- [ ] SeasonEndProcessor 신규 단계
- [ ] 우승 / 강등 / 시상만 보존 (League.history)

## DoD
- [ ] 3 시즌 플레이 후 세이브 파일 크기 ↓ 검증
"@ }

    @{ Key="M.8"; Title="[Season] 매니저 평판 (단순)"; Type="Feature"; Labels="area:season"; Priority="P1"; Size="S";
       Body=@"
## 목표
``GameState.managerReputation`` 단순 도입. boardConfidence 가산.

## 명세
- v1.0-tasks.md Stage M / Task M.8 / design-decisions.md #51

## 작업 내용
- [ ] managerReputation 0-100
- [ ] 변동 트리거 (우승 +20 / 승격 +15 / 보드 약속 이행 +5 / 경질 -30 / 월간 +5)
- [ ] boardConfidence 가산 효과

## DoD
- [ ] 우승 후 +20 검증
"@ }

    @{ Key="M.9"; Title="[UI/Season] SeasonSummaryScene"; Type="Feature"; Labels="area:ui,area:season"; Priority="P0"; Size="M";
       Body=@"
## 목표
시즌 종료 시 자동 표시 — 시상 / 보드 평가 / 재정 결산 / 다음 목표.

## 명세
- v1.0-tasks.md Stage M / Task M.9

## DoD
- [ ] 5/15 도래 시 자동 표시
"@ }

    # ─── Stage N: UI (7) ────────────────────────────────────
    @{ Key="N.1"; Title="[UI] PlayerNameLinkController (모든 선수 이름 클릭)"; Type="Feature"; Labels="area:ui"; Priority="P0"; Size="M";
       Body=@"
## 목표
사용자 피드백 2.10 — 어디서든 선수 이름 클릭 → PlayerProfile 점프.

## 명세
- v1.0-tasks.md Stage N / Task N.1

## 작업 내용
- [ ] ``UI/PlayerNameLinkController.cs`` 신규
- [ ] 클릭 → PlayerProfile + PlayerPrefs ("SelectedPlayerId" + "PreviousScene")
- [ ] Standings / Schedule / Transfer / PlayerProfile 시즌 통계 / 매치 결과 / 라커룸 모두 적용

## DoD
- [ ] 모든 씬에서 선수 이름 클릭 + 뒤로가기 컨텍스트 보존
"@ }

    @{ Key="N.2"; Title="[UI] Dashboard 인박스 + 다음 매치 + 사기 요약"; Type="Feature"; Labels="area:ui"; Priority="P0"; Size="M";
       Body=@"
## 목표
Dashboard 종합 정보 표시.

## 명세
- v1.0-tasks.md Stage N / Task N.2

## 작업 내용
- [ ] 인박스 (Promise / Board / 매치 알림 / TransferRequest / YouthPromotion)
- [ ] 다음 매치 정보 (상대 폼 + 직전 결과 + 상호 전적)
- [ ] 사기 요약 (불만 선수 목록)
- [ ] 부상 / 정지 명단

## DoD
- [ ] Dashboard 종합 정보 표시
"@ }

    @{ Key="N.3"; Title="[UI/Simulation] MatchTextScene 매치 텍스트 표시"; Type="Feature"; Labels="area:ui,area:simulation"; Priority="P0"; Size="M";
       Body=@"
## 목표
유저 매치 시 자동 진입 + 가속 ×1 / ×2 / ×4 / 스킵 (Q6).

## 명세
- v1.0-tasks.md Stage N / Task N.3

## 작업 내용
- [ ] MatchTextScene 신규
- [ ] Match.events 분 순서 표시
- [ ] 가속 4단계
- [ ] 결과 화면 (스코어 + 득점자 + 평점)

## DoD
- [ ] 유저 매치 자동 진입 + 가속 동작
"@ }

    @{ Key="N.4"; Title="[UI/Transfer] NegotiationScene 통합 (Stage K.5 와 짝)"; Type="Task"; Labels="area:ui,area:transfer"; Priority="P0"; Size="XS";
       Body=@"
## 목표
Stage K.5 의 NegotiationScene 과 Dashboard 인박스 / Transfer 검색 화면 연동.

## 명세
- v1.0-tasks.md Stage N / Task N.4 (K.5 와 짝)

## DoD
- [ ] Stage K 통합 검증
"@ }

    @{ Key="N.5"; Title="[UI] PromiseInboxScene (Dashboard 통합 옵션)"; Type="Feature"; Labels="area:ui"; Priority="P1"; Size="S";
       Body=@"
## 목표
active promises 목록 + 진행률 표시.

## 명세
- v1.0-tasks.md Stage N / Task N.5

## 작업 내용
- [ ] PromiseInboxScene 또는 Dashboard 패널
- [ ] 진행률 표시 (출전시간 35% / 목표 50%)

## DoD
- [ ] Promise 진행 UI 동작
"@ }

    @{ Key="N.6"; Title="[UI/Transfer] Squad / Transfer 검색 / 필터 강화"; Type="Feature"; Labels="area:ui,area:transfer"; Priority="P1"; Size="S";
       Body=@"
## 목표
포지션 / CA 범위 → + 연령 / 국적 / 트레잇 / 시장가 / 계약 잔여.

## 명세
- v1.0-tasks.md Stage N / Task N.6

## DoD
- [ ] 7-8 필터 옵션 동작
"@ }

    @{ Key="N.7"; Title="[UI] 신규 씬 EditorBuildSettings 등록"; Type="Task"; Labels="area:ui"; Priority="P1"; Size="XS";
       Body=@"
## 목표
V1.0 신규 씬 (Lineup / Tactic / MatchText / Negotiation / Mentoring / SeasonSummary / PromiseInbox) 빌드 포함.

## 명세
- v1.0-tasks.md Stage N / Task N.7

## DoD
- [ ] 모든 씬 빌드 포함
"@ }

    # ─── Stage O: 통합 테스트 (4) ────────────────────────────
    @{ Key="O.1"; Title="[Simulation] IntegrationTests V1.0 갱신"; Type="Task"; Labels="area:simulation"; Priority="P0"; Size="M";
       Body=@"
## 목표
V1.0 시스템 통합 시나리오 — 한 시즌 + 사기 + Promise + 매치 텍스트 + 시상.

## 명세
- v1.0-tasks.md Stage O / Task O.1

## 작업 내용
- [ ] V0.1 T1~T3 보강
- [ ] 신규 V1.0 통합 시나리오

## DoD
- [ ] 한 시즌 V1.0 전체 시스템 통합 동작
"@ }

    @{ Key="O.2"; Title="[Save] SaveMigration 라운드트립 테스트"; Type="Task"; Labels="area:save"; Priority="P0"; Size="S";
       Body=@"
## 목표
V1.0 라운드트립 + V0.1 무효 메시지.

## 명세
- v1.0-tasks.md Stage O / Task O.2

## DoD
- [ ] V1.0 신규 필드 라운드트립 정확
- [ ] V0.1 세이브 로드 → NotSupportedException
"@ }

    @{ Key="O.3"; Title="[Simulation] 매치 텍스트 이벤트 결정성 테스트"; Type="Task"; Labels="area:simulation"; Priority="P1"; Size="S";
       Body=@"
## 목표
같은 시드 매치 → 같은 텍스트 시퀀스.

## 명세
- v1.0-tasks.md Stage O / Task O.3

## DoD
- [ ] 결정성 검증
"@ }

    @{ Key="O.4"; Title="[Simulation] 사기 / 약속 시나리오 테스트"; Type="Task"; Labels="area:simulation"; Priority="P1"; Size="S";
       Body=@"
## 목표
PlaytimeAgreement 미달 → Happiness 변동 → TransferRequest.

## 명세
- v1.0-tasks.md Stage O / Task O.4

## DoD
- [ ] EditMode T 통과
- [ ] loyalty 80 vs 20 변동폭 차이
"@ }

    # ─── Stage P: V1.0 빌드 (3) ─────────────────────────────
    @{ Key="P.1"; Title="[Docs] V1.0 빌드 노트 작성"; Type="Task"; Labels="area:docs"; Priority="P1"; Size="S";
       Body=@"
## 목표
``docs/v1.0-build-notes.md`` (V0.1 패턴).

## 명세
- v1.0-tasks.md Stage P / Task P.1

## DoD
- [ ] V1.0 빌드 노트 작성
"@ }

    @{ Key="P.2"; Title="[Docs] V1.0 빌드 결과 보고서"; Type="Task"; Labels="area:docs"; Priority="P1"; Size="S";
       Body=@"
## 목표
``docs/v1.0-build-report.md`` — 빌드 환경 / 산출물 / 테스트 / 발견 이슈.

## 명세
- v1.0-tasks.md Stage P / Task P.2

## DoD
- [ ] V1.0 빌드 보고서 작성
"@ }

    @{ Key="P.3"; Title="[Docs] README + 스크린샷 갱신"; Type="Task"; Labels="area:docs"; Priority="P1"; Size="S";
       Body=@"
## 목표
포트폴리오 준비. README V1.0 + 스크린샷.

## 명세
- v1.0-tasks.md Stage P / Task P.3

## DoD
- [ ] README V1.0 + 스크린샷 (메인 / 매치 텍스트 / 시즌 시상)
"@ }
)

# ── 처리 함수 ─────────────────────────────────────────────────

function Process-Task {
    param($Task)

    Write-Host "[$($Task.Key)] $($Task.Title)" -ForegroundColor Cyan

    if ($DryRun) {
        Write-Host "  (dry-run, skip)" -ForegroundColor Gray
        return
    }

    # 1. 이슈 생성 (gh issue create) — Body 임시 파일
    $tmpBody = New-TemporaryFile
    Set-Content -Path $tmpBody -Value $Task.Body -Encoding UTF8 -NoNewline

    $url = gh issue create `
        --title $Task.Title `
        --label $Task.Labels `
        --milestone "V1.0" `
        --assignee "@me" `
        --body-file $tmpBody

    Remove-Item $tmpBody -Force

    if (-not $url) { throw "Issue create failed for $($Task.Key)" }

    # 이슈 번호 추출 (.../issues/N)
    $issueNum = ([uri]$url).Segments[-1]
    Write-Host "  Issue #$issueNum created: $url" -ForegroundColor Green

    # 2. Type 설정 + node_id (정규식 추출 — 한글 mojibake 회피)
    $patchJson = (gh api -X PATCH "repos/$Repo/issues/$issueNum" -f "type=$($Task.Type)") -join "`n"
    if ($patchJson -match '"node_id"\s*:\s*"([^"]+)"') {
        $nodeId = $matches[1]
        Write-Host "  Type=$($Task.Type) set" -ForegroundColor DarkGray
    } else {
        Write-Host "  WARN: node_id not found" -ForegroundColor Yellow
        return
    }

    # 3. 보드 #50 추가
    $addJson = (gh api graphql -f "p=$ProjectId" -f "c=$nodeId" -f "query=$AddQuery") -join "`n"
    if ($addJson -match '"id"\s*:\s*"(PVTI_[^"]+)"') {
        $itemId = $matches[1]
    } else {
        Write-Host "  WARN: itemId not obtained (resp: $addJson)" -ForegroundColor Yellow
        return
    }
    Write-Host "  Board #50 added (item $itemId)" -ForegroundColor DarkGray

    # 4. Priority + Size
    $priOpt = $PriorityOpts[$Task.Priority]
    $sizeOpt = $SizeOpts[$Task.Size]

    $priJson  = (gh api graphql -f "p=$ProjectId" -f "i=$itemId" -f "f=$PriorityFieldId" -f "o=$priOpt"  -f "query=$UpdQuery") -join "`n"
    $sizeJson = (gh api graphql -f "p=$ProjectId" -f "i=$itemId" -f "f=$SizeFieldId"     -f "o=$sizeOpt" -f "query=$UpdQuery") -join "`n"

    $priOK  = $priJson  -match [regex]::Escape($itemId)
    $sizeOK = $sizeJson -match [regex]::Escape($itemId)

    if ($priOK -and $sizeOK) {
        Write-Host "  Priority=$($Task.Priority) Size=$($Task.Size) set" -ForegroundColor DarkGray
    } else {
        Write-Host "  WARN: priOK=$priOK sizeOK=$sizeOK" -ForegroundColor Yellow
    }
}

# ── 실행 ─────────────────────────────────────────────────────

$started = ($StartFrom -eq "")
$count = 0

foreach ($task in $Tasks) {
    if (-not $started) {
        if ($task.Key -eq $StartFrom) { $started = $true }
        else { continue }
    }

    try {
        Process-Task $task
        $count++
    }
    catch {
        Write-Host "  ERROR processing $($task.Key): $_" -ForegroundColor Red
        break
    }

    Start-Sleep -Milliseconds 200  # API rate limit 여유
}

Write-Host ""
Write-Host "총 처리: $count 이슈" -ForegroundColor Cyan
