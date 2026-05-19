# Algorithms

각 알고리즘의 정확한 명세. 구현 시 이 문서를 참조하여 작성한다.

밸런싱 수치는 코드에 박지 않고 `GameBalanceSO`로 외부화한다.

---

## Template

새 알고리즘 추가 시 아래 템플릿 사용.

```markdown
## N. Algorithm Name

### Purpose
- 무엇을 계산하는가
- 어떤 시점에 호출되는가

### Inputs
- 필요한 데이터

### Outputs
- 반환 데이터

### Logic
의사코드 또는 단계별 설명.

### Balancing Parameters (→ GameBalanceSO)
- 외부화될 수치들

### Edge Cases
- 0 나누기, 음수, 클램프 등

### Test Scenarios
- 정상 입력/결과 예시
```

---

## Priority Order

V0.1 시작 전 정해야 할 알고리즘 (우선순위):

1. **선수 생성 (Player Generation)** ★★★★★
2. **구단 생성 (Club Generation)** ★★★★★
3. **경기 결과 계산 (Match Simulation)** ★★★★★
4. **선수 가치 계산 (Market Value)** ★★★★
5. **유스 풀 생성 (Youth Pool Generation)** ★★★★

> 섹션 번호(`## N.`)는 작성 순서 기준이고, 우선순위와 1:1 일치하지 않는다. ClubGen 은 PlayerGen 이후 작성되어 섹션 `## 5` 이지만 호출 흐름상 PlayerGen 다음 차례.

V0.1 진행 중 정해도 OK:

5. 선수 성장 (Player Growth)
6. 시즌 사이클 처리
7. AI 구단 의사결정 (단순 버전)

V1.0 들어갈 때:

8. 부상 시스템
9. 사기/불만 계산
10. 보드 신뢰도
11. 경기 텍스트 이벤트
12. 전술 상성
13. 스카우팅 알고리즘 상세

---

## 1. Player Generation

### Purpose

- 단일 `Player` 인스턴스를 생성한다. 호출자가 포지션 · 나이 · 국적 · 구단을 지정하면, 그에 맞는 능력치 · 트레잇 · 인적사항이 채워진 완성된 객체를 반환.
- 호출 시점:
  - **초기 스쿼드 생성** (`SquadGenerator` 가 25명분 호출)
  - **유스 인스펙션** (`YouthSystem.GenerateIntake` 가 풀 사이즈만큼 호출)
  - **리그 외 NPC 구단 시드**
  - **레전** (V1.0+)

### Inputs

| Param | Type | Note |
| --- | --- | --- |
| `rng` | `System.Random` | 시드 고정된 인스턴스. UnityEngine.Random 사용 금지 (전역 상태). |
| `clubReputation` | `int` (0~100) | CA/PA 분포 시프트. 빅클럽일수록 우상향. |
| `targetPosition` | `Position` (enum) | 1차 포지션. 2차 포지션은 내부에서 결정. |
| `age` | `int` | 만 나이. PA-CA 갭과 직결. |
| `nationalityCode` | `string` | 호출자가 분포 굴려서 결정해서 넘김. |
| `clubId` | `int` | `currentClubId` 에 저장. |
| `youthClubId` | `int` | 유스 데뷔 구단 ID. 외부 영입이면 -1. |
| `origin` | `PlayerOrigin` | InitialRoster / YouthIntake / Regen |
| `currentDate` | `DateTime` | 생일/계약 시작일 계산용 (`GameTime.CurrentDate` 전달). |
| `balanceSO` | `GameBalanceSO` | 모든 수치 외부화. |
| `db` | `GameDatabase` | PositionSO, TraitSO 풀, NamePoolSO 조회용. |

> **국적 분배 책임** [V0.1]: 호출자(`SquadGenerator`)가 `LeagueConfigSO.countryCode` + `balanceSO.primaryNationalityRatio` 로 분포 굴려서 넘긴다. Player Generation 은 받은 코드 그대로 사용 — 단일 책임 분리.

### Outputs

- 완성된 `Player` 객체 1개. 모든 필드 non-null. `career` 는 빈 리스트.
- `Player.id` 는 호출자가 부여. V0.1 단순 정책: SquadGenerator/YouthSystem 이 자체 카운터로 일련번호 할당 후 `GameState.AddPlayer` 호출. (별도 IdAllocator 헬퍼는 V1.0+ 검토.)

### Logic

전체 순서:

```
1. CA 결정      (구단 명성 + 나이 + 노이즈)
2. PA 결정      (CA + 나이 기반 갭)
3. 스탯 분배    (CA → 카테고리별 평균 → 포지션 가중 → 노이즈)
4. 트레잇 추첨  (충돌 그룹 회피, 개수 제한 없음)
5. 인적사항    (이름, 생일, 발, 2차 포지션, faceSeed)
6. 계약·상태 기본값
```

> **RNG 유틸**: 이 알고리즘이 사용하는 `rng.NextNormal(mu, sigma)` (Box-Muller) / `rng.WeightedSample(items, weightFn)` 헬퍼는 `Utils/RngExtensions.cs` 에 별도 PR(Sub-PR C)로 구현한다. PlayerGenerator 본 구현(Sub-PR D) 전에 분포 평균/표준편차 EditMode 테스트로 헬퍼 정확성 먼저 검증.

#### 1단계: CA 결정

`clubReputation` 기준 평균을 잡고 정규분포 노이즈, 청년 페널티 적용.

```
repNormalized = clubReputation / 100.0
meanCA        = balance.caRepBase + balance.caRepCoeff * repNormalized * 100
                                    # rep=0:60 / rep=50:100 / rep=100:140

caNoise = rng.NextNormal(mu=0, sigma=balance.caStdDev)
rawCA   = meanCA + caNoise

# 어릴수록 CA 낮음 (페널티는 V0.1 에서 청년 한정. 노장 페널티는 V1.0)
if age < balance.caPeakAge:
    youngBlend = (age - balance.minAge) / (balance.caPeakAge - balance.minAge)  # 0..1
    rawCA *= Lerp(balance.caYoungMultiplier, 1.0, youngBlend)

CA = Clamp(round(rawCA), balance.minCA, balance.maxCA)
```

#### 2단계: PA 결정

```
ageBlend = Clamp01((age - balance.minAge) / (balance.paGapZeroAge - balance.minAge))
meanGap  = Lerp(balance.paGapMaxMean, 0, ageBlend)
gapNoise = rng.NextNormal(mu=0, sigma=balance.paGapStdDev)
rawPA    = CA + Max(0, meanGap + gapNoise)

PA = Clamp(round(rawPA), CA, balance.maxPA)    # 하한 = CA. PA<CA 절대 불가
```

#### 3단계: 스탯 분배

CA → 카테고리별 "기본 평균" 으로 변환, 포지션 강조 카테고리에 보너스, 각 필드 노이즈.

```
caNormalized = CA / 200.0
baseStatMean = Lerp(balance.statMeanAtCAFloor,
                    balance.statMeanAtCACeil,
                    caNormalized)              # CA 30→~5, CA 200→~17

position = db.GetPosition(targetPosition)

if position.isGoalkeeper:
    FillGkProfile(player, baseStatMean, rng, balance)
else:
    FillOutfieldProfile(player, baseStatMean, position, rng, balance)


# ---- 골키퍼 ----
FillGkProfile(player, mean, rng, balance):
    foreach stat in player.stats.gk:        # 메인 카테고리
        stat = ClampStat(rng.NextNormal(mu=mean, sigma=balance.statStdDev))

    secondary = mean - balance.gkSecondaryStatPenalty
    foreach stat in player.stats.mental:    # 멘탈 정상 (포지셔닝/집중력 중요)
        stat = ClampStat(rng.NextNormal(mu=secondary, sigma=balance.statStdDev))

    foreach stat in player.stats.physical:  # 피지컬 정상 (점프/민첩 중요)
        stat = ClampStat(rng.NextNormal(mu=secondary, sigma=balance.statStdDev))

    foreach stat in player.stats.technical: # 테크니컬은 매우 낮음
        stat = ClampStat(rng.NextNormal(
                          mu=mean - balance.gkOutfieldStatPenalty,
                          sigma=balance.statStdDev))


# ---- 필드 플레이어 ----
FillOutfieldProfile(player, mean, position, rng, balance):
    techMean = mean + (position.emphasizesTechnical
                       ? balance.statEmphasisBonus
                       : -balance.statEmphasisPenalty)
    mentMean = mean + (position.emphasizesMental
                       ? balance.statEmphasisBonus
                       : -balance.statEmphasisPenalty)
    physMean = mean + (position.emphasizesPhysical
                       ? balance.statEmphasisBonus
                       : -balance.statEmphasisPenalty)

    foreach stat in player.stats.technical: stat = ClampStat(rng.NextNormal(mu=techMean, sigma=balance.statStdDev))
    foreach stat in player.stats.mental:    stat = ClampStat(rng.NextNormal(mu=mentMean, sigma=balance.statStdDev))
    foreach stat in player.stats.physical:  stat = ClampStat(rng.NextNormal(mu=physMean, sigma=balance.statStdDev))

    foreach stat in player.stats.gk:        # 필드 플레이어의 GK 스탯은 낮은 N
        stat = ClampStat(rng.NextNormal(
                          mu=balance.outfieldGkStatBase,
                          sigma=balance.statStdDev * 0.5))


ClampStat(x): return Clamp(round(x), 1, 20)
```

> [V0.1] **CA-Stats 분리 운영** (`design-decisions.md` #24): 위 분배 결과의 스탯 가중합과 `CA` 는 정확히 일치하지 않는다. CA 는 generation 결정 진실값, stats 는 디스플레이/캐릭터성용. 매치 시뮬레이션은 V0.1 에서 팀 CA 합 기반. 다만 둘이 폭주하지 않게 T5 통계 테스트로 sanity check (상관계수). V1.0 에서 매치가 개별 stats 사용 시 `Player.DeriveCAFromStats(pos)` derived 전환 검토.

#### 4단계: 트레잇

```
selectedTraits = []
usedGroups    = {}

# 첫 트레잇 시도. 실패하면 추가 트레잇 시도도 안 함 (분포 sanity).
if rng.NextDouble() < balance.traitProbabilityPerPlayer:    # 0.30
    AddTrait(selectedTraits, usedGroups, db, rng)

    # 추가 트레잇 — 풀 고갈 시 자동 종료
    while rng.NextDouble() < balance.additionalTraitProbability:  # 0.15
        if !AddTrait(selectedTraits, usedGroups, db, rng): break

player.traitIds = selectedTraits


AddTrait(selected, usedGroups, db, rng) → bool:
    pool = db.AllTraits
        .Where(t => t.id not in selected)
        .Where(t => t.exclusionGroupId == 0 || t.exclusionGroupId not in usedGroups)
    if pool.empty: return false
    chosen = rng.WeightedSample(pool, t => t.weight)
    selected.add(chosen.id)
    if chosen.exclusionGroupId != 0:
        usedGroups.add(chosen.exclusionGroupId)
    return true
```

**분포** (위 의사코드 / 실제 구현 모두 동일 결과):
- 0개 = 70% (첫 시도 fail)
- 1개 = 30% × 85% = 25.5% (첫 받고 추가 fail)
- 2개 = 30% × 15% × 85% = 3.825%
- 3개+ < 1%
- 평균 ≈ 0.36개

> **주석 (V0.1)**: 초기 명세는 `while` 루프를 `if` 밖에 둬서 첫 시도 실패한 선수도 추가 트레잇을 시도하는 구조였으나, P(≥1) = 40.5% 가 되어 위 분포와 불일치. PR #74 구현 시 `while` 을 `if` 안으로 이동해 분포(70/25.5/3.8) 와 일치시킴 — 의사코드도 이번에 동기화.

**충돌 그룹 정의 (현재)**:
- Group 1 (DevelopmentSpeed): 늦깎이형, 조숙형 — 동시 부여 불가

새 트레잇 추가 시 의미상 모순되는 것끼리 같은 group ID 부여.

> [V0.1] **트레잇은 라벨만 부여, 효과는 V1.0+ 시스템**: 생성 시점엔 `traitIds` 채우기만 한다. 트레잇이 성장 곡선·매치 결과·부상 확률 등에 미치는 영향은 V1.0 성장 시스템 / 매치 시뮬레이션 / 부상 시스템에서 처리. V0.1 에선 UI 표시용으로만 활용.

#### 5단계: 인적사항

```
namePool  = db.GetNamePool(nationalityCode)
firstName = rng.Choice(namePool.firstNames)
lastName  = rng.Choice(namePool.lastNames)

birthYear  = currentDate.Year - age
birthMonth = rng.Range(1, 13)              # 1..12
birthDay   = rng.Range(1, 29)              # 1..28 (28일까지로 안전하게)
birthDate  = new DateTime(birthYear, birthMonth, birthDay)

preferredFoot = rng.WeightedSample({
    Right: balance.footRightRatio,         # 0.62
    Left:  balance.footLeftRatio,          # 0.30
    Both:  balance.footBothRatio,          # 0.08
})

secondaryPositions = GenerateSecondaryPositions(targetPosition, rng, db, balance)
faceSeed           = rng.Next()


GenerateSecondaryPositions(primaryPos, rng, db, balance) → List<Position>:
    primary = db.GetPosition(primaryPos)
    # GK 는 2차 포지션 없음 — 필드 포지션과 어피니티가 의미적으로 성립하지 않음.
    if primary.isGoalkeeper: return []

    secondaries = []
    if rng.NextDouble() < balance.secondaryPositionProbability:    # 0.40
        pick = PickSecondary(primaryPos, exclude={primaryPos}, rng, db)
        secondaries.add(pick)

        # 2차 있을 때만 3차 시도
        if rng.NextDouble() < balance.thirdPositionProbability:    # 0.15
            pick2 = PickSecondary(primaryPos,
                                  exclude={primaryPos, pick},
                                  rng, db)
            secondaries.add(pick2)
    return secondaries


PickSecondary(primaryPos, exclude, rng, db) → Position:
    primary = db.GetPosition(primaryPos)
    # GK 는 2차 후보에서도 제외 (필드→GK 이중 포지션 비현실적).
    pool    = AllPositions.Where(p => !exclude.Contains(p) && !db.GetPosition(p).isGoalkeeper)
    weights = pool.Select(p =>
        primary.affinities.FirstOrDefault(a => a.position == p)?.weight
        ?? primary.fallbackAffinityWeight)
    return rng.WeightedSample(pool, weights)
```

**분포**: 2차 없음 60% / 2차 1개 34% / 2차 2개 6% (필드 플레이어 기준). 무관 포지션 뚫리는 확률 ≈ 2~3% (fallback 0.05 기준). GK 는 100% 2차 없음.

> [V1.x] **적응도 시스템**: 플레이 중 일정 시간 다른 포지션 출전 → 적응도 누적 → 임계치 도달 시 secondaryPositions 자동 추가. V0.1 스코프 외.

#### 6단계: 계약 · 상태 기본값

```
contract = new Contract {
    weeklyWage    = EstimateInitialWage(CA, age, balance),
    startDate     = currentDate,
    endDate       = currentDate.AddYears(rng.Range(1, 5)),    # 1~4년
    releaseClause = 0     # V0.1 미사용
}

state = new PlayerState {
    fatigue = 0, morale = 50, form = 50,
    injury = new InjuryInfo { injuryTypeId = -1 },            # 부상 없음 sentinel
    transferListed = false,
    seasonAppearances = 0
}

career = []     # 신규 선수, 커리어 비어있음


EstimateInitialWage(CA, age, balance):
    base = balance.wageBaseAtMinCA + (CA - balance.minCA) * balance.wagePerCAPoint
    # V0.1 단순 함수. V1.0 에서 노장 디스카운트 / 시장가치 연동
    return Max(balance.wageFloor, round(base / 100) * 100)    # 100단위 반올림
```

### Balancing Parameters (→ GameBalanceSO)

```csharp
// === CA 결정 ===
public int   caRepBase = 60;
public float caRepCoeff = 0.8f;
public float caStdDev = 18f;
public int   caPeakAge = 27;
public float caYoungMultiplier = 0.55f;
public int   minAge = 15;
public int   maxAge = 40;
// minCA/maxCA/minPA/maxPA 는 기존 필드 재사용

// === PA 결정 ===
public float paGapMaxMean = 50f;
public int   paGapZeroAge = 28;
public float paGapStdDev = 15f;

// === 스탯 분배 ===
public float statMeanAtCAFloor = 4f;
public float statMeanAtCACeil  = 17f;
public float statEmphasisBonus = 2f;
public float statEmphasisPenalty = 2f;
public float statStdDev = 2.5f;
public float gkSecondaryStatPenalty = 1f;     // GK 의 멘탈/피지컬 평균 감점
public float gkOutfieldStatPenalty = 8f;       // GK 의 테크니컬 평균 감점
public float outfieldGkStatBase = 3f;          // 필드 플레이어의 GK 스탯 평균

// === 트레잇 ===
// traitProbabilityPerPlayer = 0.30  (기존)
public float additionalTraitProbability = 0.15f;

// === 2차 포지션 ===
public float secondaryPositionProbability = 0.40f;
public float thirdPositionProbability = 0.15f;

// === 인적사항 ===
public float footRightRatio = 0.62f;
public float footLeftRatio  = 0.30f;
public float footBothRatio  = 0.08f;

// === 국적 (호출자가 사용) ===
public float primaryNationalityRatio = 0.70f;

// === 계약 ===
public int   wageBaseAtMinCA = 500;
public float wagePerCAPoint = 350f;
public int   wageFloor = 500;
```

### Edge Cases

| Case | 처리 |
| --- | --- |
| `clubReputation` 범위 밖 (음수, >100) | 호출 진입 시 `Clamp(0, 100)` |
| `age` 범위 밖 (<15, >40) | Assert + 경고 로그, 그래도 진행 |
| `NamePoolSO` 에 해당 국가 없음 | ENG 폴백 + 경고 로그 |
| CA 가 1단계에서 음수로 떨어짐 | `Clamp(minCA, maxCA)` 가 처리 |
| PA < CA (있을 수 없으나 방어) | `Clamp(CA, maxPA)` 가 처리 |
| stat 0 또는 21 | `ClampStat(1, 20)` 가 처리 |
| 트레잇 풀이 비어있음 | 트레잇 없이 진행, 경고 로그 |
| 트레잇 weight 합 = 0 | 균등 분포 폴백 |
| 같은 시드로 두 번 호출 | 동일 `rng` 인스턴스라면 동일 결과 보장 — 호출자 책임 |
| GK 의 2차 포지션 | 5단계에서 명시적으로 빈 리스트 반환 |
| 2차 포지션이 1차와 같음 | `WeightedSample` 시 exclude 적용 |
| Position.affinities 가 비어있음 | 모든 포지션이 fallback weight 로 균등 추첨 |

### Test Scenarios

테스트는 `Random(seed: 42)` 고정으로. 단일 선수 테스트(T1~T3)는 **범위** 어서션, 통계 테스트(T5~T7)는 1000명/100명 batch 평균.

**T1. 빅클럽 베테랑 ST (범위 어서션)**
- Input: rep=85, age=27, ST, ENG
- Expect:
  - CA 범위 in [120, 155]
  - PA = CA + (0~10)
  - Technical / Physical 평균 ≥ 14
  - GK 스탯 평균 in [1, 4]

**T2. 작은 구단 신예 CM (범위 어서션)**
- Input: rep=25, age=17, CM, ESP
- Expect:
  - CA 범위 in [50, 90]
  - PA 범위 in [95, 150] (큰 갭)
  - 이름이 ESP NamePool 에 포함됨

**T3. GK 생성 (범위 어서션)**
- Input: rep=50, age=24, GK, ITA
- Expect:
  - GoalkeepingStats 평균 in [10, 14]
  - TechnicalStats 평균 in [1, 4]
  - MentalStats 평균 in [8, 12]
  - **`secondaryPositions.Count == 0`** (GK 는 2차 없음)

**T4. 결정성**
- 동일 input + 동일 seed → 동일 Player (모든 필드)
- 다른 seed → 다른 결과

**T5. 분포 통계 (1000명 batch, 필드 플레이어만)**
- rep=50, age 균등(17~30), 비-GK 포지션 균등 → 1000명 생성:
  - CA 평균 in [75, 97] — age 17~26 youngMultiplier 적용 후 실측 기대값 ≈85 (아래 주석 참고)
  - PA-CA 갭 평균 in [14, 32] — ageBlend 반영 실측 기대값 ≈20
  - 트레잇 보유 비율 30% ±5%
  - 늦깎이형 + 조숙형 동시 보유 = 0건 (충돌 그룹 검증)
  - **CA 와 stats 가중합의 상관계수 > 0.6** (CA-Stats sanity check, design-decisions.md #24)

> **CA 평균 주석**: 초안 "100 ±5" 는 prime-age(27+) 기준 오기. age 17~30 균등 시 caYoungMultiplier(0.55) 적용으로 평균 multiplier ≈ 0.85 → 기대 CA ≈ 85. age penalty 고려하지 않은 spec 오류였으며 구현 검증 후 수정.

**T6. 2차 포지션 affinity 검증 (1000명 batch, ST 전용)**
- ST 1000명 생성 시 2차 포지션 분포:
  - LW/RW 합계 35~50% (강 affinity)
  - AM 10~20%
  - CB/LB/RB 합계 < 5% (fallback)
  - GK = 0건 (GK 제외 규칙 검증)

**T7. GK 2차 포지션 = 0 (100명 batch)**
- GK 100명 생성 → 모든 선수의 `secondaryPositions.Count == 0`
- (T3 가 단일 케이스, T7 은 통계 확정)

### V1.0 Migration Notes

V0.1 → V1.0 진행 시 손댈 가능성 있는 부분 모음. 이 알고리즘 안에 박힌 가정들의 출구.

| 항목 | V0.1 동작 | V1.0 변경 후보 | 영향 범위 |
| --- | --- | --- | --- |
| **CA-Stats 정합성** | Option A 분리 (CA 진실, stats 별개) | Option B 정합 (CA = stats 가중합 derived) | 3단계 분배 로직 + 성장 시스템 + 매치 시뮬레이션 |
| **노장 페널티** | 미적용 | `caPeakAge` 이후 페널티 곡선 도입 | 1단계 |
| **임금 함수** | CA 선형 함수 | 노장 디스카운트 + 시장가치 연동 | 6단계 + `EstimateInitialWage` |
| **국적 분배** | 호출자가 결정해서 넘김 | `LeagueConfigSO.nationalityDistribution: List<{code, weight}>` 표 도입 | 호출자(SquadGenerator) 측 |
| **2차 포지션 적응도** | 생성 시점 고정 (GK 는 강제 빈 리스트) | 플레이 중 출전 적응도로 동적 추가 | 5단계 + 새 `Player.positionFamiliarity` 필드 |
| **트레잇 충돌 그룹** | 1개 그룹(DevelopmentSpeed)만 정의 | 새 트레잇 추가 시 그룹 확장 | TraitSO 데이터 — 알고리즘 자체는 변화 없음 |
| **트레잇 효과** | 라벨만 부여, 효과 없음 | 성장·매치·부상 시스템에서 트레잇 분기 처리 | 별도 시스템들, generation 은 그대로 |
| **매치 시뮬레이션과 stats** | 매치는 CA 만 사용, stats 는 표시용 | stats 가 매치 결과에 직접 영향 | #2 알고리즘 — 그 시점에 #1 의 CA-Stats 정합성 재검토 트리거 |
| **레전(Regen)** | `origin=Regen` enum 만 존재, 호출 경로 없음 | 시즌 사이클에 레전 생성 호출 추가 | 새 호출자(`RegenSystem`). 알고리즘 자체는 그대로 사용 가능. |
| **ID 할당** | 호출자가 직접 카운터 관리 | `IdAllocator` 헬퍼 또는 `GameState.NewPlayerId()` 도입 검토 | 호출자 + GameState |

### Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2026-05-18 | All | Initial spec for V0.1. CA-Stats 분리 운영, 트레잇 충돌 그룹, 2차 포지션 affinity 결정. GK 2차 포지션 강제 빈 리스트, gkSecondaryStatPenalty 외부화, RNG 헬퍼 위치 명시, T5 CA-Stats sanity check + T7 GK 통계 추가. |
| 2026-05-19 | 4단계 의사코드 | `while` 루프를 `if` 안으로 이동 — 분포(70/25.5/3.8) 와 일치시킴. PR #74 구현이 이미 이 형태였고 명세 의사코드만 미동기였음. |

---

## 2. Match Simulation

*(미작성)*

---

## 3. Market Value

*(미작성)*

---

## 4. Youth Pool Generation

*(미작성)*

---

## 5. Club Generation

### Purpose

- 리그 1개 분량(20구단) 의 `Club` 인스턴스 + 각 구단별 25명 스쿼드 일괄 생성.
- 호출 시점: `GameInitializer` (새 게임 시작 — `data-flows.md` #1).
- 단일 책임: Club 인스턴스 빌드 + `PlayerGenerator.Generate` 호출. **id 할당 / GameState 등록 / userClub 선정은 호출자**.

### Inputs

| Param | Type | Note |
| --- | --- | --- |
| `rng` | `System.Random` | 시드 고정. UnityEngine.Random 금지. |
| `leagueConfig` | `LeagueConfigSO` | clubCount / playersPerClub / countryCode / clubNames 사용. |
| `balance` | `GameBalanceSO` | 모든 수치 외부화. |
| `db` | `GameDatabase` | NamePool / Position / Country / Trait 풀 접근. |
| `currentDate` | `DateTime` | 창단년도 / 계약 시작일 계산. |
| `leagueId` | `int` | 모든 `Club.leagueId`. |
| `startClubId` | `int` | 클럽 id 시작값. id = startClubId + i. |
| `startPlayerId` | `int` | 첫 번째 선수 id. 이후 단조증가. |

### Outputs

```csharp
public class ClubGenerationResult {
    public List<Club> clubs;        // Count == leagueConfig.clubCount (V0.1: 20)
    public List<Player> players;    // Count == clubCount × playersPerClub (V0.1: 500)
}
```

- 모든 `Player.currentClubId` 가 `clubs` 중 하나의 id.
- 모든 `Club.seniorSquadIds.Count == playersPerClub`.
- `player.id` / `club.id` 모두 고유, 시작값부터 단조증가.

### Logic

```
1. 명성 분배         (4티어 계단 → 클럽별 rep)
2. Club 인스턴스 빌드 (Finance, Facilities — 명성 약상관 + 노이즈)
3. 스쿼드 생성        (포지션 분배표 → 연령/국적 추첨 → PlayerGenerator 호출)
```

#### 1단계: 명성 분배

4티어 계단 (`design-decisions.md` #27). **가변 `clubCount` 대응** — 카운트는 ratio 로 외부화:

```
tier  | ratio | clubCount=20 → count | repRange
------|-------|---------------------|---------
Top4  | 0.20  |          4          | 85..95
Euro  | 0.30  |          6          | 65..80
Mid   | 0.35  |          7          | 45..60
Rel   | 0.15  |          3          | 25..40
                  Σratio = 1.00
```

각 구단의 rep 은 자기 티어 구간에서 균등 추첨. `leagueConfig.clubNames[i]` 가 명성 내림차순으로 정렬되어 있다는 전제 — i=0 이 최상위.

```
tierCounts = AllocateTierCounts(balance.tierClubRatios, clubCount)
# tierCounts.Sum() == clubCount 보장 (round-off 잔여 보정 후)

ranks = []
for tierIdx in 0..tierCounts.Length:
    for _ in 0..tierCounts[tierIdx]:
        ranks.add(rng.Next(balance.tierRepMin[tierIdx],
                           balance.tierRepMax[tierIdx] + 1))
# ranks.Count == clubCount, ranks[i] 가 i 번째 구단 명성


AllocateTierCounts(ratios, n) → int[]:
    # round 잔여를 fractional part 큰 티어부터 +1
    raw       = ratios.Select(r => r * n).ToList()
    counts    = raw.Select(x => (int)floor(x)).ToList()
    remainder = n - counts.Sum()
    if remainder > 0:
        order = raw.Select((x, i) => (i, frac: x - counts[i]))
                   .OrderByDescending(p => p.frac)
                   .Select(p => p.i)
                   .ToList()
        for k in 0..remainder: counts[order[k]] += 1
    elif remainder < 0:
        # ratio 합 > 1.0 같은 비정상. 끝 티어부터 -1.
        for k in 0..(-remainder): counts[counts.Count - 1 - k] = max(0, counts[...] - 1)
    return counts
```

> **외부화**: `tierClubRatios: float[]` (합 ≈ 1.0), `tierRepMin: int[]`, `tierRepMax: int[]`. ratio 합 ≠ 1.0 → 경고 후 `AllocateTierCounts` 가 라운드 보정으로 흡수. `clubNames.Count < clubCount` → `$"Club {i+1}"` 폴백 + 경고.

> **clubCount=20 외에도 동작**: clubCount=10 / 12 / 24 등 가변 입력 시 `AllocateTierCounts` 가 ratio 기반으로 분배. 예) clubCount=10 → Top2 / Euro3 / Mid4 / Rel1.

#### 2단계: Club 인스턴스 빌드

```
for i in 0..clubCount:
    rep      = ranks[i]
    moneyMu  = balance.financeBaseMoney + balance.financeRepCoeff * rep
    money    = Max(balance.financeFloor,
                   round(moneyMu + rng.NextNormal(0, moneyMu * balance.financeNoiseSigma)))

    repLv    = rep / 20.0          # rep=100 → 5
    scoutLv  = ClampLevel(round(repLv + rng.NextNormal(0, balance.facilityNoiseSigma)))
    trainLv  = ClampLevel(round(repLv + rng.NextNormal(0, balance.facilityNoiseSigma)))
    youthLv  = ClampLevel(round(repLv + rng.NextNormal(0, balance.facilityNoiseSigma)))

    foundYr  = currentDate.Year - rng.Next(balance.clubMinAgeYears,
                                           balance.clubMaxAgeYears + 1)

    clubs[i] = new Club {
        id = startClubId + i,
        name = leagueConfig.clubNames[i],          # 부족 시 $"Club {i+1}" 폴백
        foundedYear = foundYr,
        leagueId = leagueId,
        reputation = rep,
        finance = new Finance {
            money = money,
            debt = 0,
            transferBudget = round(money * balance.transferBudgetRatio),
            wageBudget     = round(money * balance.wageBudgetRatio),
        },
        facilities = new Facilities {
            scoutLevel = scoutLv,
            trainingLevel = trainLv,
            youthLevel = youthLv,
        },
        seniorSquadIds = [],     # 3단계에서 채움
        youthSquadIds  = [],
        intakeHistory  = [],
        season = new SeasonState {
            targetLeaguePosition = i + 1,                       # 명성 순위 = 기본 목표
            cupTarget            = CupTarget.None,
            boardConfidence      = balance.initialBoardConfidence,   # 50
        },
        isActiveSimulation = false,    # userClub 결정 후 호출자가 true 갱신
    }


ClampLevel(x): Clamp(round(x), balance.minFacilityLevel, balance.maxFacilityLevel)
```

> [V0.1] **약상관 정책 (design-decisions.md #27)**: 시설 등급은 `rep/20` 직접 매핑이지만 `NextNormal(σ=1)` 노이즈로 한두 단계 출렁. 자금은 base + repCoeff×rep + 15% σ 노이즈. → 빅클럽인데 시설 평범 / 중위권인데 유스 강한 캐릭터성 살아남.

#### 3단계: 스쿼드 25명 생성

**고정 포지션 분배표 (`design-decisions.md` #28):**

```
SquadComposition = [
    (GK, balance.squadGK = 3),
    (CB, balance.squadCB = 4),
    (LB, balance.squadLB = 2),
    (RB, balance.squadRB = 2),
    (DM, balance.squadDM = 2),
    (CM, balance.squadCM = 3),
    (AM, balance.squadAM = 2),
    (LM, balance.squadLM = 1),
    (RM, balance.squadRM = 1),
    (LW, balance.squadLW = 1),
    (RW, balance.squadRW = 1),
    (ST, balance.squadST = 2),
    (CF, balance.squadCF = 1),
]   # 합계 25
```

```
nextPlayerId = startPlayerId

for each club in clubs:
    for each (pos, count) in SquadComposition:
        for j in 0..count:
            age         = SampleAge(rng, balance)
            nat         = SampleNationality(rng, leagueConfig.countryCode, db, balance)
            homegrown   = rng.NextDouble() < balance.homegrownRatio   # 0.20
            youthClubId = homegrown ? club.id : -1

            player = PlayerGenerator.Generate(
                rng, club.reputation, pos, age, nat,
                club.id, youthClubId, PlayerOrigin.InitialRoster,
                currentDate, balance)

            player.id = nextPlayerId++
            players.Add(player)
            club.seniorSquadIds.Add(player.id)


SampleAge(rng, balance) → int:
    bucket = rng.WeightedSample(
        ["youth", "prime", "veteran"],
        b => b switch {
            "youth"   => balance.youthAgeRatio,    # 0.20
            "prime"   => balance.primeAgeRatio,    # 0.60
            "veteran" => balance.veteranAgeRatio,  # 0.20
        })
    return bucket switch {
        "youth"   => rng.Next(balance.youthAgeMin,   balance.youthAgeMax   + 1),  # 16..21
        "prime"   => rng.Next(balance.primeAgeMin,   balance.primeAgeMax   + 1),  # 22..28
        "veteran" => rng.Next(balance.veteranAgeMin, balance.veteranAgeMax + 1),  # 29..35
    }


SampleNationality(rng, leagueCountry, db, balance) → string:
    if rng.NextDouble() < balance.primaryNationalityRatio:    # 0.70
        return leagueCountry
    others = db.AllCountries.Where(c => c.code != leagueCountry).ToList()
    if others.Count == 0: return leagueCountry                # 폴백
    return others[rng.Next(others.Count)].code                # 균등 추첨
```

> **국적 분배 책임 (algorithms.md #1 와 일치)**: ClubGenerator 가 `primaryNationalityRatio` 분포로 굴려 PlayerGenerator 에 코드 전달. PlayerGenerator 는 받은 코드 그대로 사용 — 단일 책임 분리.

> **연령 분포 단순화 (V0.1)**: 모든 구단 동일 비율. V1.0 에서 빅클럽 veteran ratio ↑ / 중하위권 youth ratio ↑ 차등 검토.

### Balancing Parameters → GameBalanceSO

```csharp
// ─── Reputation Tiers ───
// ratio 합은 ≈ 1.0. round-off 잔여는 AllocateTierCounts 가 흡수.
// clubCount 가변 대응 (10/12/20/24 등 어떤 값이든 동작).
public float[] tierClubRatios = { 0.20f, 0.30f, 0.35f, 0.15f };  // Top4/Euro/Mid/Rel
public int[]   tierRepMin     = {  85,    65,    45,    25   };
public int[]   tierRepMax     = {  95,    80,    60,    40   };

// ─── Finance ───
public int   financeBaseMoney    = 5_000_000;     // £5M floor at rep=0
public float financeRepCoeff     = 4_000_000f;    // rep=50 → 205M, rep=95 → 385M
public float financeNoiseSigma   = 0.15f;         // 15% σ
public int   financeFloor        = 1_000_000;
public float transferBudgetRatio = 0.20f;
public float wageBudgetRatio     = 0.50f;

// ─── Facilities ───
public float facilityNoiseSigma  = 1.0f;          // ±1 등급 정도
public int   minFacilityLevel    = 1;
public int   maxFacilityLevel    = 5;

// ─── Squad Composition ───
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

// ─── Age Distribution ───
public float youthAgeRatio   = 0.20f;
public float primeAgeRatio   = 0.60f;
public float veteranAgeRatio = 0.20f;
public int   youthAgeMin     = 16, youthAgeMax = 21;
public int   primeAgeMin     = 22, primeAgeMax = 28;
public int   veteranAgeMin   = 29, veteranAgeMax = 35;

// ─── Foundation Year ───
public int clubMinAgeYears = 50;
public int clubMaxAgeYears = 150;

// ─── Homegrown ───
public float homegrownRatio = 0.20f;

// ─── Board ───
public int initialBoardConfidence = 50;

// primaryNationalityRatio 는 algorithms.md #1 에서 이미 정의됨 (0.70).
```

### Edge Cases

| Case | 처리 |
| --- | --- |
| `tierClubRatios` 합 ≠ 1.0 | 경고. `AllocateTierCounts` 라운드 보정으로 흡수 (잔여 +1 또는 끝 티어 -1). |
| `tierClubRatios.Length` ≠ `tierRepMin/Max.Length` | Assert (배열 차원 불일치는 데이터 오류). |
| `clubCount` 가 작아서 일부 티어 count=0 | 허용. 해당 티어 추첨 자체 스킵 (예: clubCount=4 + ratios={0.2,0.3,0.35,0.15} → 1/1/1/1, Rel ratio 작아도 round 잔여로 1 확보). |
| `leagueConfig.clubNames.Count < clubCount` | 부족분 `$"Club {i+1}"` 폴백 + 경고. |
| 클럽명 중복 | 허용 (LeagueConfigSO 설계 책임). |
| `Σ(squadGK..squadCF)` ≠ `leagueConfig.playersPerClub` | 경고, **SquadComposition 합 기준**으로 진행 (실제 생성 인원 = 분배표 합). |
| `db.AllCountries` 가 leagueCountry 하나뿐 | 외국인 추첨 시 leagueCountry 폴백. |
| `youthAgeRatio + primeAgeRatio + veteranAgeRatio` ≠ 1.0 | `WeightedSample` 가 자동 정규화 (#1 트레잇 weight 폴백과 동일). |
| `clubCount == 0` | 빈 결과 반환 + 경고. |
| `startClubId / startPlayerId` 음수 | 호출자 책임. Assert 만. |

### Test Scenarios

`Random(seed: 42)` 고정. 통계 테스트는 `clubCount=20` × `playersPerClub=25` = 500명 batch.

**T1. 결정성**
- 같은 seed + 같은 LeagueConfig + 같은 GameBalance → 모든 Club/Player 동일.
- 검증 필드: 모든 `club.id/name/reputation/finance.money/facilities.*`, 모든 `player.id/CA/PA/info.firstName/info.primaryPosition`.

**T2. 명성 4티어 분포 (clubCount=20 기준)**
- 20구단 reputation 정렬:
  - 85~95 ∈ 4구단 (Top4)
  - 65~80 ∈ 6구단 (Euro)
  - 45~60 ∈ 7구단 (Mid)
  - 25~40 ∈ 3구단 (Rel)

**T2b. 가변 clubCount 분포**
- clubCount=10 → 티어 카운트가 `AllocateTierCounts` 로 round-off 보정되어 합 10 (예상: Top2/Euro3/Mid4/Rel1).
- clubCount=24 → 합 24 (예상: Top5/Euro7/Mid8/Rel3 또는 round 잔여 처리에 따른 인접 분포).
- 어떤 clubCount 든 `Σtier == clubCount` 보장.

**T3. 스쿼드 인원 / 포지션 분배표 일치**
- 모든 구단 `seniorSquadIds.Count == 25`.
- 각 구단의 포지션별 카운트가 SquadComposition 과 정확히 일치 (GK 3 / CB 4 / ... / CF 1).

**T4. 연령 분포 (500명 batch)**
- 평균 age in `[23, 27]`.
- min age >= 16, max age <= 35.
- 16~21 비율 ≈ 20% (±5%), 22~28 ≈ 60% (±5%), 29~35 ≈ 20% (±5%).

**T5. 국적 분포 (500명 batch, `leagueCountry="ENG"`)**
- ENG 비율 ≈ 70% (±5%).
- 외국 국적 합 ≈ 30% (±5%).

**T6. 재정 / 시설 — 명성 약상관 (20구단)**
- corr(rep, money) ∈ `[0.5, 0.9]` (강한 양상관, 노이즈로 1.0 미만).
- corr(rep, scoutLevel) ∈ `[0.4, 0.9]`.
- 시설 등급 분포: 1 또는 5 한 곳에 60% 이상 몰리지 않음 (다양성 검증).

**T7. ID 유일성 / 단조증가**
- 모든 `player.id` ∈ `[startPlayerId, startPlayerId + 500)`, 중복 없음.
- 모든 `club.id` ∈ `[startClubId, startClubId + 20)`, 중복 없음.
- `club.seniorSquadIds` 가 정확히 그 구단 선수들의 id 만 포함.

### V1.0 Migration Notes

| 항목 | V0.1 동작 | V1.0 변경 후보 | 영향 범위 |
| --- | --- | --- | --- |
| **티어 ratio / repRange** | 단일 ratio 표 (모든 리그 동일) | 리그별 다른 분포 (ESP=빅2+중상위 강세, GER=빅3+분데스 평준화 등) — LeagueConfigSO 로 이전 | 1단계 + LeagueConfigSO |
| **포지션 분배표 가변화** | int 13 필드 + playersPerClub 가변 대응 (분배표 합 기준 진행) | float ratio 13 필드 → playersPerClub 와 자동 정합 | 3단계 SquadComposition |
| **포지션 분배표 구단별 색깔** | 모든 구단 동일 표 | 4-3-3 / 4-4-2 / 3-5-2 등 전술 프리셋별 분배표 | 3단계 + 새 TacticPresetSO |
| **연령 분포 명성 차등** | 모든 구단 동일 (20/60/20) | 빅클럽=veteran ↑, 강등권=youth ↑ | 3단계 SampleAge |
| **국적 분배 가중표** | leagueCountry 70% + 외국 균등 30% | `LeagueConfigSO.nationalityDistribution: List<{code, weight}>` (#1 V1.0 노트와 일치) | 3단계 SampleNationality |
| **재정 다양성** | 15% σ 정규분포만 | 부채 / 스폰서 / 적자 구단 등 스토리텔링 요소 | 2단계 + Finance |
| **시즌 목표 동적화** | 명성 순위 = 목표 (Rel 도 i+1) | 보드 신뢰도·예산 조합 기반 동적 목표 | 2단계 + Board 시스템 |
| **userClub 선정** | ClubGen 후 호출자가 `isActiveSimulation` 갱신 | UI 구단 선택 화면 → `data-flows.md` #1 [4] | data-flows.md #1 |
| **homegrown 시설 연동** | 모든 구단 20% 고정 | 유스 시설 등급 → 비율 ↑ (Lv5 → 35%, Lv1 → 10%) | 3단계 |
| **다중 리그 동시 생성** | 단일 리그 호출 (caller loop 가능) | 다중 LeagueConfigSO 일괄 처리 + 명성 통합 ranking (이적 시장 연동) | GameInitializer + 1단계 |

### Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2026-05-19 | All | Initial spec for V0.1. 4티어 명성 분포(design-decisions.md #27), 고정 스쿼드 분배표(#28), 명성 약상관 재정/시설, 연령 3구간 분포, 국적 분배(자국 70% + 외국 균등 30%). |
| 2026-05-19 | 1단계 / Balancing / Edge / Test | 가변 `clubCount` 대응: `tierClubCounts:int[]` → `tierClubRatios:float[]` + `AllocateTierCounts` 라운드 보정. T2b 추가. |

---

## Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2025-05-15 | All | Template created, sections empty (to be filled per design session) |
| 2026-05-19 | Priority Order + #5 | ClubGen 우선순위 ★★★★★ 로 격상, `## 5. Club Generation` 섹션 작성. 섹션 번호와 우선순위 1:1 불일치 명시. |
