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

1. **선수 생성 (Player Generation)** ★★★★★ — `## 1` 작성 완료
2. **구단 생성 (Club Generation)** ★★★★★ — `## 5` 작성 완료
3. **스타팅 스쿼드 가챠 (Starting Squad Gacha)** ★★★★ — `## 6` 작성 완료
4. **경기 결과 계산 (Match Simulation)** ★★★★★ — `## 2` 작성 완료
5. **선수 가치 계산 (Market Value)** ★★★★ — `## 3` 미작성
6. **유스 풀 생성 (Youth Pool Generation)** ★★★★ — `## 4` 미작성

> 섹션 번호(`## N.`)는 작성 순서 기준이고, 우선순위와 1:1 일치하지 않는다. ClubGen 은 PlayerGen 이후 작성되어 섹션 `## 5` 이지만 호출 흐름상 PlayerGen 다음 차례. Gacha 는 섹션 `## 6` 으로 끝에 추가. Match Simulation 은 섹션 `## 2` 자리에 후속 작성.

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

### Purpose

- 단일 `Match` 의 결과 (`MatchResult`) 산출. 호출자가 `Match` 와 `GameState` 를 넘기면 시드 고정 → 양 팀 전력 → 골수 → 득점자 순으로 결정.
- 호출 시점:
  - **유저 구단 경기** — UI 라인업/전술 확정 후 (`data-flows.md` #3 [3])
  - **비활성 구단 경기** — `BackgroundSimulator` 가 라운드 일괄 처리 (Task 9.3)
- 단일 책임: `MatchResult` 산출만. 결과 적용 (`Match.result =`, 순위 갱신, 이벤트 발행) 은 `MatchPostProcessor` (Task 9.2).

### Inputs

| Param | Type | Note |
| --- | --- | --- |
| `match` | `Match` | id / homeClubId / awayClubId / type 사용. result 는 read X. |
| `state` | `GameState` | `GetClub` / `GetPlayer` / `randomSeed` 사용. 변경 X. |
| `balance` | `GameBalanceSO` | 모든 수치 외부화. |

> **Stateless 원칙 (`design-decisions.md` #3)**: 시뮬레이터 자체는 필드 보유 X. 같은 입력 → 같은 출력.

### Outputs

```csharp
public MatchResult {
    public int homeScore;
    public int awayScore;
    public List<int> homeStarting11;            // 11명 playerId (V0.1: top-by-CA)
    public List<int> awayStarting11;            // 11명 playerId
    public List<PlayerMatchStat> playerStats;   // 22명 (양 팀 starting11 합)
}

public PlayerMatchStat {
    public int   playerId;
    public int   minutesPlayed;   // V0.1: 90 고정 (교체 X)
    public int   goals;           // 4단계 알고리즘 결과
    public int   assists;         // V0.1: 0 (어시스트 시스템 V1.0+)
    public float rating;          // V0.1: 0  (평점 시스템 V1.0+)
    public int   yellowCards;     // V0.1: 0 (카드 시스템 V1.0+)
    public int   redCards;        // V0.1: 0
}
```

> **결과 적용 책임 분리**: `MatchSimulator.Simulate` 는 **순수 함수** — `match.result` 에 쓰지 않고 `MatchResult` 반환만. Task 9.2 `MatchPostProcessor` 가 `match.result = result` + 순위 갱신 + `MatchFinishedEvent` 발행.

### Logic

전체 순서:

```
1. 시드 고정         (rng = new Random(match.id ^ state.randomSeed))
2. starting11 선정   (양 팀 top-11 by CA, 부족 시 가용 인원)
3. 양 팀 전력 계산   (starting11 CA 합)
4. 골수 결정         (λ 계산 → Poisson 샘플링, 홈 어드밴티지 가산)
5. 득점자 선정       (포지션 라인 가중치 × CA/100 비례 추첨)
6. PlayerMatchStat 빌드 (22명, V0.1 은 goals + minutesPlayed=90 만)
```

#### 1단계: 시드 고정

```
rng = new System.Random(match.id ^ state.randomSeed)
```

- **결정성 (`design-decisions.md` #17)**: 같은 매치 + 같은 게임 시드 → 항상 같은 결과. 세이브/로드 일관성.
- `match.id` 는 `ScheduleGenerator` 가 시즌 시작 시 단조증가로 부여 (전 매치 unique).
- `state.randomSeed` 는 게임 생성 시 고정 (`GameInitializer`).

#### 2단계: starting11 선정 (V0.1)

V0.1 에선 라인업 결정 시스템 / UI 없음 (Task 13.2~13.4 까지 미구현). 시뮬레이터가 자동 선정.

```
SelectStartingEleven(club, state) → List<int>:
    candidates = club.seniorSquadIds
        .Select(id => state.GetPlayer(id))
        .Where(p => p.state.injury.injuryTypeId == -1)    # 부상자 제외
        .OrderByDescending(p => p.currentAbility)
        .Take(11)
        .Select(p => p.id)
        .ToList()
    return candidates
```

- **포지션 무시 (V0.1)**: top-11 by CA 단순. 포메이션 충족 검증 / 자동 라인업 알고리즘은 V1.0+.
- **부상자 제외**: `injuryTypeId == -1` 만 출전 가능 (`PlayerGenerator` 6단계 sentinel).
- **결과 개수**: 정상 시 11. 부족 시 (스쿼드 < 11 또는 부상자 다수) 가용 인원 그대로 — Edge Case 처리.

> **V1.0+ 전환 트리거**: 라인업 결정 시스템 / 포메이션 / 전술 프리셋 도입 시 호출자가 starting11 을 인자로 전달하고 시뮬레이터는 받기만 — `Simulate(match, state, homeXI, awayXI)` 오버로드 검토.

#### 3단계: 양 팀 전력 계산

```
homeStrength = SUM(state.GetPlayer(id).currentAbility for id in homeStarting11)
awayStrength = SUM(state.GetPlayer(id).currentAbility for id in awayStarting11)
```

- **단순 CA 합 (V0.1, `design-decisions.md` #24)**: 라인별 가중치 / 개별 stats / 포지션 적합도 / 폼·사기·피로 보정 모두 V0.1 스코프 외.
- **starting11 만 카운트**: 벤치 무관. 강팀이 약한 백업 잔뜩 있어도 출전 11 에만 의존.

#### 4단계: 골수 결정 — Poisson

```
totalStrength = homeStrength + awayStrength
if totalStrength == 0:
    strengthRatio = 0.5    # edge: 양 팀 starting11 모두 비어있음 폴백
else:
    strengthRatio = homeStrength / totalStrength    # 0..1

totalLambda = balance.avgGoalsPerMatch              # 2.7 (EPL 평균)

homeLambda = totalLambda * strengthRatio + balance.homeAdvantageGoalBonus
awayLambda = totalLambda * (1 - strengthRatio)

homeScore = rng.NextPoisson(homeLambda)
awayScore = rng.NextPoisson(awayLambda)
```

- **Poisson 분포 선택 이유**: 실제 축구 골 분포의 학계 표준 (Dixon-Coles 1997 등). 강팀 vs 약팀 시 양쪽 모두 자연스러운 분산 — 약팀이 가끔 강팀에 이변 가능, 강팀도 무득점 경기 가능.
- **λ (lambda) 의미**: 평균 골수. 같은 λ 라도 매번 다른 값. 예) λ=0.8 → 0골 45% / 1골 36% / 2골 14% / 3골 4% / 4골 1%.
- **strengthRatio 분배**: 양 팀 strength 비율로 totalLambda 를 나눠 가짐. CA 합 60:40 → home 1.62골 평균, away 1.08골 평균 (홈 보정 전).
- **홈 어드밴티지**: `homeAdvantageGoalBonus` 만큼 home λ 에만 가산 (away 감산 X). EPL 통계 근사 (홈 ~46% / 무 ~26% / 원정 ~28%).
- **결정성**: `rng.NextPoisson` 이 inverse-CDF 방식이라 같은 rng 상태 → 같은 결과.

> **`rng.NextPoisson(lambda)` 헬퍼**: `Utils/RngExtensions.cs` 에 추가 (Sub-PR B). Knuth 알고리즘 (작은 λ) — `L = exp(-lambda); k = 0; p = 1; while (p > L) { k++; p *= rng.NextDouble(); } return k - 1;`. PlayerGen 의 `NextNormal` 과 같은 패턴. 분포 평균/분산 EditMode 테스트로 헬퍼 정확성 먼저 검증 후 본 구현 (Sub-PR C).

#### 5단계: 득점자 선정

각 골마다 starting11 중 가중치 추첨.

```
PickScorers(starting11, state, totalGoals, balance, rng) → Dictionary<int, int>:
    goalsByPlayer = {}
    if totalGoals == 0: return goalsByPlayer
    
    pool = starting11
    weights = pool.Select(id =>
        let p = state.GetPlayer(id)
        let line = LineOf(p.info.primaryPosition)
        let lineWeight = balance.scoringWeightByLine[(int)line]   # GK=0, DF=0.4, MF=1.5, AT=5.0
        return lineWeight * (p.currentAbility / 100.0))
    
    for goal in 0..totalGoals:
        scorerId = rng.WeightedSample(pool, weights)
        goalsByPlayer[scorerId] = goalsByPlayer.GetValueOrDefault(scorerId, 0) + 1
    
    return goalsByPlayer


LineOf(Position) → Line:
    # algorithms.md #6 1단계와 동일 분류 (재사용)
    GK = { GK }
    DF = { CB, LB, RB, WB }
    MF = { DM, CM, AM, LM, RM }
    AT = { LW, RW, ST, CF }
```

- **포지션 라인 가중치 (가장 큰 결정 요인)**: 공격수가 ~60% 득점 (현실 분포). GK 는 0 — V0.1 페널티/코너 GK 골 없음.
- **CA 보정**: `cm/100.0` 으로 같은 라인 내 에이스가 더 자주 득점 (강팀 ST 가 약팀 ST 보다 자주 득점).
- **라인 분류 재사용**: `algorithms.md` #6 1단계 정의와 동일 (Gacha 평가 / 득점자 선정 일관성).
- **결정성**: 골수 결정과 같은 rng 사용. 양 팀 각자 별도 추첨 (home → away 순).

> **가중치 모두 0 폴백**: pool 의 모든 가중치 합 = 0 (예: 11명 모두 GK 인 비정상) → `WeightedSample` 균등 분포 폴백 (`algorithms.md` #1 트레잇 weight 폴백과 동일).

#### 6단계: PlayerMatchStat 빌드 (V0.1)

```
playerStats = []
foreach id in homeStarting11.Concat(awayStarting11):
    playerStats.Add(new PlayerMatchStat {
        playerId       = id,
        minutesPlayed  = 90,                                # 교체 X
        goals          = goalsByPlayer.GetValueOrDefault(id, 0),
        assists        = 0,                                 # V1.0+
        rating         = 0f,                                # V1.0+
        yellowCards    = 0,                                 # V1.0+
        redCards       = 0,                                 # V1.0+
    })

return new MatchResult {
    homeScore = homeScore,
    awayScore = awayScore,
    homeStarting11 = homeStarting11,
    awayStarting11 = awayStarting11,
    playerStats = playerStats,
}
```

> **V0.1 채우지 않는 필드**: 어시스트 / 평점 / 카드 / 교체 미니츠. V1.0+ 텍스트 이벤트 시스템 / 평점 시스템 / 카드 시스템 도입 시 채움.

### Balancing Parameters → GameBalanceSO

```csharp
// === Match Simulation ===
public float avgGoalsPerMatch        = 2.7f;      // EPL 평균 (실제 ~2.7-2.9)
public float homeAdvantageGoalBonus  = 0.3f;      // homeLambda 에 가산
public float[] scoringWeightByLine   = { 0.0f, 0.4f, 1.5f, 5.0f };
//                                        GK    DF    MF    AT  (Line enum 순서와 일치)
```

> **외부화 원칙 (`design-decisions.md` #11)**: 매직 넘버 금지. avgGoalsPerMatch 등은 플레이테스트로 조정.

### Edge Cases

| Case | 처리 |
| --- | --- |
| `state.GetClub(homeClubId/awayClubId)` 가 null | `ArgumentException` throw (호출자 책임). |
| 스쿼드 크기 < 11 | 가용 인원만 starting11 에 포함. starting11.Count < 11 가능. 부족 측 strength ↓ → λ ↓ → 골 적게 — 알고리즘 자체는 동작. |
| 부상자 다수로 starting11 = 0명 | Edge: 양 팀 모두 0명 → strengthRatio = 0.5 폴백, totalStrength = 0 → 양 팀 모두 lambda ≈ 0 → 0-0 무승부 확률 높음. 경고 로그 1회. |
| `totalStrength == 0` | strengthRatio = 0.5 폴백 (위와 동일). |
| `match.type != League` (FA Cup, Carabao Cup) | V0.1 호출 경로 없음. 받으면 League 와 동일 처리 + 경고 로그. 컵 연장전/승부차기는 V1.0+. |
| `scoringWeightByLine.Length != 4` | Assert (Line enum 과 길이 불일치는 데이터 오류). |
| 모든 starting11 이 GK (포지션 분배표 비정상) | scoringWeightByLine[GK] = 0 → WeightedSample 가중치 합 0 → 균등 분포 폴백. GK 가 골 넣는 비정상 결과 가능하지만 분배표 비정상이 근본 원인. |
| 골수 = 0 | 득점자 선정 단계 스킵, goalsByPlayer 빈 채로 진행. |
| Poisson sampling 으로 극단치 (10골 이상) | 클램프 안 함. 자연 분포 그대로. λ=3 에서 10골 확률 ~0.001%. |
| 같은 starting11 에서 동일 선수 여러 골 | 정상 동작 (해트트릭 등). goalsByPlayer 가 누적. |
| 동점 (homeScore == awayScore) | 리그 무승부 그대로. `MatchPostProcessor` 가 `StandingEntry.drawn += 1` 처리. |

### Test Scenarios

`Random(seed: 42)` 고정. 통계 테스트는 100~1000 매치 batch.

**T1. 결정성**
- 같은 `match.id` + 같은 `state.randomSeed` → 모든 필드 동일 (`homeScore`, `awayScore`, `homeStarting11`, `awayStarting11`, 각 `PlayerMatchStat`).
- 다른 `match.id` → 다른 결과 (높은 확률).

**T2. starting11 선정**
- 25명 스쿼드 → starting11.Count == 11.
- top-11 by CA: starting11 의 최저 CA ≥ 벤치(스쿼드 - starting11) 의 최고 CA.
- 부상자 (`injuryTypeId != -1`) 가 5명 → starting11 의 CA 합이 부상자 제외 top-11 과 일치.
- 가용 인원 < 11 (부상자 다수) → starting11.Count = 가용 인원 (Edge case).

**T3. 강팀 승률 (100 매치 batch, 강팀 vs 약팀)**
- home CA 합 ~1700 / away CA 합 ~900 (강팀 vs 약팀) → 강팀(home) 승률 **≥ 70%** (홈 어드밴티지 포함).
- 동일 조건 home/away 스왑 (약팀 홈) → 강팀(away) 승률 **≥ 60%** (홈 어드밴티지 보정 후에도 강팀이 자주 이김).
- 비고: `Task 9.1` 완료 조건의 "강팀 승률 60% 이상" 충족.

**T4. 동급 팀 — 무승부 / 홈 어드밴티지 (1000 매치 batch)**
- 양 팀 CA 합 같음 → home 승률 ~45% / draw ~26% / away 승률 ~29% 근처 (EPL 통계 근사).
- 홈 승률 > 원정 승률 (홈 어드밴티지 확인).

**T5. 골 분포 통계 (1000 매치 batch, 동급 팀)**
- 평균 골수 (home + away) ≈ `avgGoalsPerMatch + homeAdvantageGoalBonus` (= 3.0 ±0.15).
- 무득점 경기 비율 ≈ 8~10%.
- 5골 이상 경기 비율 ≈ 12~18%.
- 최대 골수: 0..8 범위 대부분 (10골 이상은 극히 드문).

**T6. 득점자 분포 (1000 골 batch, 동급 팀)**
- AT 라인 득점 비율 ≈ 55~65% (`scoringWeightByLine` 가중치 + 라인 인원 비율 반영).
- MF 라인 ≈ 20~30%.
- DF 라인 ≈ 5~15%.
- GK 라인 = 0% (가중치 0).
- 같은 라인 내 CA 높은 선수가 자주 득점 (e.g. 라인 최고 CA 선수 득점 ≥ 라인 최저 CA 선수 득점).

**T7. PlayerMatchStat 정확성**
- 모든 starting11 (22명) 의 PlayerMatchStat 존재.
- `goals` 합 == `homeScore + awayScore`.
- `minutesPlayed` 모두 90.
- `assists / rating / yellowCards / redCards` 모두 0 (V0.1).

### V1.0+ Migration Notes

V0.1 → V1.0 진행 시 손댈 가능성 있는 부분. 각 항목의 영향 범위를 명시해 V1.0 작업 진입 시 폭발 반경 확인 가능하게.

| 항목 | V0.1 동작 | V1.0+ 변경 후보 | 영향 범위 |
| --- | --- | --- | --- |
| **전력 산출 — 단순 CA 합** | starting11 CA 합 | 라인별 가중 / 포지션 적합도 / 폼·사기·피로 보정 / 개별 stats 도입 | 3단계 + `design-decisions.md` #24 V1.0 트리거 |
| **starting11 자동 선정** | top-11 by CA (포지션 무시) | UI 라인업 결정 시스템 + 포메이션 충족 / 전술 프리셋 | 2단계 → 호출자가 starting11 전달, 시뮬레이터는 받기만 |
| **결과 우선 → 이벤트 시퀀스 전환** | 스코어/득점자 한 번에 결정 (`design-decisions.md` #17 의 "결과 우선" 모델) | **분 단위 이벤트 시뮬레이션** — 옐로 카드 누적 / 부상 발생 / 교체 (AI 자동) / 외침 등이 차후 이벤트에 영향. 누적 결과가 최종 스코어 | **전면 재작성**. 인터페이스 `Simulate(match, state) → MatchResult` 는 유지 (호출자 영향 없음). `design-decisions.md` #34 의 진화 경로 참조. |
| **컵 연장전 + 승부차기** | V0.1 호출 경로 없음 (League 만) | `Match.type == FACup/CarabaoCup` 분기 — 동점 시 연장전 (λ_extraTime) → 그래도 동점이면 승부차기 (별도 5+ 라운드) | 4단계 + Edge Cases + 새 balance 필드 (`extraTimeLambda`, `penaltyShootoutPlayerWeight`) |
| **어시스트 / 평점** | 0 고정 | 어시스트: 득점자 추첨 후 같은 팀 내 2차 추첨. 평점: 골/어시/카드/팀 결과 기반 V1.0 공식 | 5/6단계 |
| **부상 / 카드** | 0 고정 | 분 단위 이벤트 시뮬레이션 도입 시 자연스럽게 발생 — 옐로 2장 = 퇴장 (10명으로 strength ↓), 부상 = 교체 (벤치 strength) | 이벤트 시퀀스 전환과 함께. `PlayerInjuredEvent` 발행 (`event-bus-catalog.md`) |
| **교체** | 미구현 (90분 고정) | AI 자동 교체 (피로/부상/전술 기반). 유저 수동 교체는 V1.x | 새 시스템. 시뮬레이터 내부 또는 별도 `SubstitutionAI`. `PlayerMatchStat.minutesPlayed` 가 가변. |
| **비활성 구단 경량 시뮬 (`SimulateLite`)** | V0.1 에선 폐기 — 단일 `Simulate` 메서드. 이벤트 발행만 `BackgroundSimulator` 가 생략 (`MatchFinishedEvent` X) | 이벤트 시퀀스 시스템 도입 후 비활성 구단은 스코어만 산출하는 경량 경로 분리 검토 | 새 메서드 + `data-flows.md` #3 갱신 |
| **시드 결정성** | `match.id ^ randomSeed` 한 번 | 이벤트 시퀀스 도입 시 매 이벤트 step rng 상태 누적 — 같은 시드 → 같은 시퀀스 → 같은 결과. `design-decisions.md` #17 정신은 보존. | 1단계 + 내부 구조 변화 (인터페이스 동일) |
| **개별 stats 사용** | 사용 X (CA 만) | 슈팅 → finishing / 패스 → passing / 태클 → tackling 등 분기. `design-decisions.md` #24 의 "V1.0 변경 트리거" | 3~5단계 + Player stats 직접 참조 |
| **외부 영향 — 사기 / 폼 / 피로** | 미반영 | 이벤트 시퀀스 도입 후 strength 계산 시 곱셈 보정 | 3단계 |

### Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2026-05-19 | All | Initial spec for V0.1. 단순 CA 합 + Poisson 골 분포 + 홈 어드밴티지 가산 + 포지션 라인 가중 득점자. starting11 = top-11 by CA (V0.1 라인업 결정 시스템 부재 임시 단순화). V1.0+ Migration Notes 에 이벤트 시퀀스 진화 경로 / 컵 연장전 / 비활성 구단 분기 / 어시스트·평점·카드 등 정리. `design-decisions.md` #33 (V0.1 정책) / #34 (V1.0+ 진화) 와 연동. |

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

## 6. Starting Squad Gacha

### Purpose

- 구단의 초기 스쿼드 (ClubGen 산출물 25명) 를 **4라인 × 5단계 티어** 로 평가하여 유저가 결정 가능한 형태로 표시.
- **명성 대비 상대평가** (`design-decisions.md` #15) — 빅클럽의 "Average" ≈ 중위권의 "Strong".
- 유저가 **리롤** 호출 시 해당 구단 25명 전체 재생성 (`state.rerollTokens -= 1`).
- 호출 시점: `GameInitializer` 가 구단 생성 + 유저 구단 선택 후. `data-flows.md` #1 [4].

### Inputs

| Param | Type | Note |
| --- | --- | --- |
| `club` | `Club` | 평가 대상 구단. 25명 스쿼드 보유. |
| `state` | `GameState` | `allPlayers` 조회용. Reroll 시 `rerollTokens` / `nextPlayerId` 갱신. |
| `balance` | `GameBalanceSO` | 평가 컷 / Formation / Reroll 정책. |
| `db` | `GameDatabase` | PositionSO 조회 (라인 분류용). |
| `rng` | `System.Random` | Reroll 시 새 시드 인스턴스 (호출자가 derived seed 부여). |

### Outputs

```csharp
public class SquadEvaluation {
    public TierGrade gk;          // Elite / Strong / Average / Weak / Poor
    public TierGrade df;
    public TierGrade mf;
    public TierGrade at;
    public Line acePosition;      // 최고 CA 선수의 라인 (GK / DF / MF / AT)
    public int aceLineCA;         // 디버그용 — 표시는 라인만
}

public enum TierGrade { Poor, Weak, Average, Strong, Elite }
public enum Line { GK, DF, MF, AT }
```

> **UI 표시 정책 (`design-decisions.md` #14)**: 평균 CA 절댓값 / 점수는 숨김. 티어 라벨만 노출. 디버그 모드 (`isDebugMode = true`) 일 때만 raw 수치 표시.

### Logic

```
1. 4라인 분류         (PositionSO 기반)
2. 라인 평균 CA       (각 라인 출전 선수 평균)
3. 명성 대비 정규화    (ratio = lineCA / expectedMeanCA → 5단계 컷)
4. ACE 마커          (전체 선수 중 최고 CA → 그 선수의 라인)
```

#### 1단계: 4라인 분류

```
Line.GK = { Position.GK }
Line.DF = { Position.CB, Position.LB, Position.RB, Position.WB }
Line.MF = { Position.DM, Position.CM, Position.AM, Position.LM, Position.RM }
Line.AT = { Position.LW, Position.RW, Position.ST, Position.CF }
```

> **고정 분류**: V0.1 에선 알고리즘 내 하드코딩. V1.0 에서 PositionSO 에 `lineCategory` 필드 도입 검토 (`design-decisions.md` #30 참조).

#### 2단계: 라인 평균 CA

```
foreach line in [GK, DF, MF, AT]:
    membersInLine = club.seniorSquadIds
        .Select(id => state.GetPlayer(id))
        .Where(p => p.info.primaryPosition in line.positions)
    
    if membersInLine.empty:
        lineCA[line] = 0       # edge case: 분배표가 비정상이면 0 → Poor
    else:
        lineCA[line] = avg(p.currentAbility for p in membersInLine)
```

#### 3단계: 명성 대비 정규화 → 5단계 티어

```
expectedMeanCA = balance.caRepBase + balance.caRepCoeff * club.reputation
                                    # algorithms.md #1 1단계와 동일 공식

foreach line:
    ratio = lineCA[line] / expectedMeanCA
    
    if   ratio >= balance.tierEliteRatio    : tier = Elite     # 1.20
    elif ratio >= balance.tierStrongRatio   : tier = Strong    # 1.05
    elif ratio >= balance.tierAverageRatio  : tier = Average   # 0.90
    elif ratio >= balance.tierWeakRatio     : tier = Weak      # 0.75
    else                                    : tier = Poor
```

**예시 (`design-decisions.md` #15 의 "빅클럽 평범 ≈ 중위권 훌륭" 구현 검증)**:
- 빅클럽 (rep=90, expectedMean=132) + 라인 평균 CA 140 → ratio 1.06 → **Strong**
- 중위권 (rep=50, expectedMean=100) + 라인 평균 CA 110 → ratio 1.10 → **Strong**
- 표면 수치는 다르지만 같은 평가. 작은 구단으로 시작해도 "우리 공격진 훌륭함!" 만족감 확보.

#### 4단계: ACE 마커

```
allPlayers = club.seniorSquadIds.Select(id => state.GetPlayer(id))
acePlayer  = allPlayers.OrderByDescending(p => p.currentAbility).First()
acePosition = LineOf(acePlayer.info.primaryPosition)
aceLineCA   = acePlayer.currentAbility
```

> ACE 는 단일. 동률 시 첫 번째 매치 (정렬 안정성). UI 는 "🌟 ACE in {Line}" 같은 식으로 단일 라벨 표시.

### Reroll 정책

```
RerollSquad(club, state, balance, rng) → SquadEvaluation:
    # 1. 토큰 차감
    if state.rerollTokens <= 0: throw "No tokens"
    state.rerollTokens -= 1
    
    # 2. 기존 25명 제거
    foreach playerId in club.seniorSquadIds.ToList():
        state.RemovePlayer(playerId)
    club.seniorSquadIds.Clear()
    
    # 3. 같은 club 으로 ClubGen 의 3단계 (스쿼드 생성) 만 호출
    # (1단계 명성, 2단계 Club 인스턴스 는 유지. 명성 / 재정 / 시설 / 창단년도 보존)
    nextId = state.nextPlayerId
    foreach (pos, count) in ResolveSquadComposition(club, balance, rng):
        for j in 0..count:
            player = PlayerGenerator.Generate(rng, club.reputation, pos, ...,
                                              currentDate, balance)
            player.id = nextId++
            state.AddPlayer(player)
            club.seniorSquadIds.Add(player.id)
    state.nextPlayerId = nextId
    
    # 4. 재평가
    return EvaluateSquad(club, state, balance, db)
```

> **새 id 부여 정책 (`design-decisions.md` #31)**: 기존 player id 재사용 X. `state.nextPlayerId` 카운터 단조증가. 디버그 / 세이브 일관성.

> **시드 정책**: Reroll 마다 다른 결과 보장 위해 호출자가 `rng = new Random(state.randomSeed ^ club.id ^ club.rerollsUsed)` 같은 derived seed 부여. ClubGen 의 Reroll 추적은 `Club` 에 `int rerollsUsed` 필드 추가 검토 (`v0.1-tasks.md` Task 13.3 시점 결정).

### Balancing Parameters → GameBalanceSO

```csharp
// === Gacha 평가 컷 (라인별 5단계 티어) ===
public float tierEliteRatio    = 1.20f;
public float tierStrongRatio   = 1.05f;
public float tierAverageRatio  = 0.90f;
public float tierWeakRatio     = 0.75f;
// < tierWeakRatio → Poor

// === Reroll ===
// initialRerollTokens / maxRerollStockpile 는 기존 필드 재사용.
```

> **분배표 (`FormationConfig`)** 는 `design-decisions.md` #28 갱신 항목 — 별도 헤더 (Club Gen — Formation). 여기 Gacha 명세에서는 평가 컷만 정의.

### Edge Cases

| Case | 처리 |
| --- | --- |
| 라인에 선수 0명 (분배표 비정상) | lineCA = 0 → 자동 Poor. 경고 로그. |
| `expectedMeanCA == 0` (rep=0 + caRepBase=0 같은 극단) | ratio = lineCA / max(1, expectedMean). 0 나누기 방어. |
| 모든 라인 CA 동일 (e.g. 시드 결정성 테스트) | ACE 는 첫 번째 매치 (정렬 안정성). |
| `state.rerollTokens == 0` 에서 Reroll 호출 | `InvalidOperationException`. UI 단에서 미리 버튼 비활성화로 막아야 함. |
| Reroll 중 ClubGen 실패 | 트랜잭션 정책 X (V0.1 단순화). 부분 실패 시 `seniorSquadIds` 가 빈 상태로 남음 — Assert + 경고. |

### Test Scenarios

`Random(seed: 42)` 고정.

**T1. 라인 분류 정확성**
- 모든 PositionSO 가 정확히 1개 라인 매핑 (GK 단독, DF 4개, MF 5개, AT 4개 = 14)
- AM 은 MF, WB 는 DF, GK 는 단독

**T2. 명성 대비 비율 — Strong 케이스**
- rep=85 club, GK 라인 평균 CA 140 → ratio ≈ 1.0 → Average (대조군)
- rep=85 club, GK 라인 평균 CA 165 → ratio ≈ 1.18 → Strong

**T3. 명성 대비 — 중위권 만족도 검증** (`design-decisions.md` #15)
- rep=50 club, AT 라인 평균 CA 120 → ratio = 1.20 → Elite ✓
- 같은 평균 CA 120 이지만 rep=90 club → ratio ≈ 0.91 → Average
- 즉 **같은 절대 CA 도 명성에 따라 다른 평가** — 디자인 의도

**T4. ACE 마커**
- 25명 중 한 선수의 CA 를 인위적으로 +50 → 그 선수의 라인이 acePosition
- 동률 시 첫 번째 매치 (deterministic)

**T5. Reroll 결정성**
- 같은 seed + 같은 club + 같은 rerollIndex → 같은 SquadEvaluation
- 다른 seed → 다른 결과 (높은 확률)
- Reroll 후 기존 25명 id 가 state.allPlayers 에서 사라짐

**T6. Reroll 토큰 부족**
- state.rerollTokens = 0 → InvalidOperationException

**T7. 라인 비어있음 (edge case)**
- 의도적으로 분배표 망가뜨려 GK 0명 → gk = Poor
- 경고 로그 1회

### V1.0+ 보완 포인트

| 항목 | V0.1 동작 | V1.0 변경 후보 | 영향 범위 |
| --- | --- | --- | --- |
| **포메이션 다양성** | 4-4-2 단일 (`FormationConfig` nested) | `FormationSO` 추출 + 5~6개 포메이션. 가챠 시 랜덤 선택. | 1단계 + ClubGen 분배표 + SO 카탈로그 |
| **출전 시간 카테고리** | 미구현 (V0.1 단순) | `Player.agreedPlaytime: PlaytimeTier` (주전/서브/비상후보) — PlayerGen 이 CA 기반 + 노이즈로 부여. "인기 선수 / 중요 선수" 자동 식별. 사기 시스템 연동. | Player 도메인 필드 + PlayerGen + 사기 시스템 |
| **사기 시스템 연동** | 평가만 표시 | 약속 출전시간 미달자 → 사기 ↓, 보드 신뢰도 영향 | Player.state.morale + Board 시스템 |
| **티어 표시 정교화** | 5단계만 | 라인 내 "Star Player / Backup / Spare" 단위 표시 | UI 레이어 |
| **명성 대비 정규화 방식** | `lineCA / expectedMeanCA` 단순 비율 | z-score (`(lineCA - expectedMean) / caStdDev`) 도입 검토 — 더 통계적으로 정확 | 3단계 |
| **라인 분류 외부화** | 하드코딩 | `PositionSO.lineCategory: Line` 필드 추가 | 1단계 + PositionSO + 시드 |
| **다중 ACE / 라인별 ACE** | 단일 ACE | 라인별 최고 CA 선수 마커 (4명) | 4단계 |

### Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2026-05-19 | All | Initial spec for V0.1. 4라인(GK/DF/MF/AT) + 명성 대비 비율 + 5단계 티어 + ACE 마커 + Reroll 재생성 정책. 출전 시간 시스템은 V1.0+ 보완 포인트로 명세만 기록. |

---

## Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2025-05-15 | All | Template created, sections empty (to be filled per design session) |
| 2026-05-19 | Priority Order + #5 | ClubGen 우선순위 ★★★★★ 로 격상, `## 5. Club Generation` 섹션 작성. 섹션 번호와 우선순위 1:1 불일치 명시. |
| 2026-05-19 | Priority Order + #6 | Starting Squad Gacha 우선순위 ★★★★ 추가, `## 6. Starting Squad Gacha` 섹션 작성. 4라인 평가 + 명성 대비 비율 + Reroll 재생성. |
| 2026-05-19 | Priority Order + #2 | Match Simulation `## 2` 섹션 신규 작성 (Task 9.1 Sub-A, #109). 단순 CA 합 + Poisson + 홈 어드밴티지 + 포지션 라인 가중 득점자. starting11 = top-11 by CA. `design-decisions.md` #33 (V0.1 정책) / #34 (V1.0+ 이벤트 시퀀스 진화) 와 연동. |
