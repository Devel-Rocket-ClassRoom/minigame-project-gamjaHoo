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
5. **선수 가치 계산 (Market Value)** ★★★★ — `## 3` 작성 완료
6. **유스 풀 생성 (Youth Pool Generation)** ★★★★ — `## 4` 작성 완료

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
    k  = balance.strengthExponent              # 비선형 지수 (기본 1.5)
    sh = pow(homeStrength, k)
    sa = pow(awayStrength, k)
    strengthRatio = sh / (sh + sa)              # 0..1

totalLambda = balance.avgGoalsPerMatch          # 2.7 (EPL 평균)

homeLambda = totalLambda * strengthRatio + balance.homeAdvantageGoalBonus
awayLambda = totalLambda * (1 - strengthRatio)

homeScore = rng.NextPoisson(homeLambda)
awayScore = rng.NextPoisson(awayLambda)
```

- **Poisson 분포 선택 이유**: 실제 축구 골 분포의 학계 표준 (Dixon-Coles 1997 등). 강팀 vs 약팀 시 양쪽 모두 자연스러운 분산 — 약팀이 가끔 강팀에 이변 가능, 강팀도 무득점 경기 가능.
- **λ (lambda) 의미**: 평균 골수. 같은 λ 라도 매번 다른 값. 예) λ=0.8 → 0골 45% / 1골 36% / 2골 14% / 3골 4% / 4골 1%.
- **strengthRatio 비선형화 (strengthExponent k)**: 단순 선형 ratio (`s_h / (s_h + s_w)`) 는 CA 차이를 골수 차이로 충분히 반영 못 함 (CA 1.89배 차이 → 골 1.43~2.23배 차이만 → 강팀 원정 승률 51%, 디자인 의도 대비 낮음). `pow(s, k)` 변환으로 강팀 우월함 증폭. k=1 이면 선형 (V0.1 초기 동작), k=1.5 (기본) 면 강팀 홈 ~72% / 원정 ~59% — EPL 1위 팀 시즌 승률 (73~79%) 근사.
- **홈 어드밴티지**: `homeAdvantageGoalBonus` 만큼 home λ 에만 가산 (away 감산 X). EPL 통계 근사 (홈 ~46% / 무 ~26% / 원정 ~28%).
- **동급 팀에서 k 무관**: `s_h == s_w` 면 어떤 k 든 strengthRatio = 0.5. T4/T5/T6 영향 없음. k 의 효과는 양 팀 차이가 있을 때만 발현.
- **결정성**: `rng.NextPoisson` 이 inverse-CDF 방식이라 같은 rng 상태 → 같은 결과.

> **V0.1 임시 변통 (V1.0+ 폐기 예정)**: `strengthExponent` 는 단순 CA 합 모델의 결정력 부족을 보강하는 **V0.1 한정 임시 보정**. V1.0+ 매치 엔진 재작성 시 (`design-decisions.md` #34 이벤트 시퀀스) finishing / composure / decisions 등 개별 stats 가 슈팅 변환률을 직접 결정하므로 비선형 ratio 보정 불필요. k=1 회귀 또는 알고리즘 자체 폐기.

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
public float strengthExponent        = 1.5f;      // strengthRatio 비선형 지수 (V0.1 임시 보정, V1.0+ 폐기)
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

통계 테스트는 200~1000 매치 batch. **매치 시드는 `new Random(globalSeed)` 기반 `seedGen.Next()` 로 매번 well-distributed 하게 생성** — 단순 `(seedBase + i) ^ i` 패턴은 i 가 seedBase 의 lowest set bit 보다 작으면 XOR collision 발생해 같은 시드가 반복됨 (결과 클러스터링).

**T1. 결정성**
- 같은 `match.id` + 같은 `state.randomSeed` → 모든 필드 동일 (`homeScore`, `awayScore`, `homeStarting11`, `awayStarting11`, 각 `PlayerMatchStat`).
- 다른 `match.id` → 다른 결과 (높은 확률).

**T2. starting11 선정**
- 25명 스쿼드 → starting11.Count == 11.
- top-11 by CA: starting11 의 최저 CA ≥ 벤치(스쿼드 - starting11) 의 최고 CA.
- 부상자 (`injuryTypeId != -1`) 가 5명 → starting11 에서 제외.
- 가용 인원 < 11 (부상자 다수 / 스쿼드 부족) → starting11.Count = 가용 인원 (Edge case).

**T3. 강팀 승률 (200 매치 batch, k=1.5 기준)**

강팀 CA 합 ~1700 (starting11 평균 ~155) / 약팀 CA 합 ~900 (starting11 평균 ~82).

`strengthExponent=1.5` 기준 정규근사 (Skellam):
- `s_h^1.5 = 1700^1.5 ≈ 70 100`, `s_w^1.5 = 900^1.5 ≈ 27 000`
- `strengthRatio ≈ 70100 / 97100 ≈ 0.722`

| 케이스 | λ_strong | λ_weak | E[D] | P(strong wins) 정규근사 | 임계치 |
| --- | --- | --- | --- | --- | --- |
| 강팀 홈 | 2.25 (=2.7×0.722+0.3) | 0.75 (=2.7×0.278) | 1.50 | ~72% | **≥ 65%** |
| 강팀 원정 | 1.95 (=2.7×0.722) | 1.05 (=2.7×0.278+0.3) | 0.90 | ~59% | **≥ 50%** |

- σ ≈ √(λ_strong + λ_weak) ≈ √3.0 ≈ 1.73 공통.
- P(D ≥ 1) ≈ Φ((E[D] - 0.5) / σ) — 연속성 보정 -0.5.
- 표본오차 (200매치 std err ~3.5%, 99% CI ±9%) 마진 ≥ 7%p.
- 비고: `Task 9.1` 완료 조건 "강팀 승률 60% 이상" 충족 (홈 케이스 기준).
- **k 변경 시 임계치 재조정 필요** — strengthExponent 가 GameBalanceSO 외부화돼 있어 플레이테스트 조정 후 본 테스트도 함께 갱신.

**T4. 동급 팀 — 홈 어드밴티지 (1000 매치 batch)**
- λ_home = 1.65 / λ_away = 1.35.
- Skellam 정규근사 기대 분포: home 승률 ~45% / draw ~22% / away 승률 ~33%.
- 검증: **홈 승률 > 원정 승률** (홈 어드밴티지 존재 확인).

**T5. 골 분포 통계 (1000 매치 batch, 동급 팀)**
- 평균 골수 (home + away) ≈ `avgGoalsPerMatch + homeAdvantageGoalBonus` = 3.0 ±0.2.
- 무득점 경기 비율 ≈ 2~10% (이론 `P(h=0)*P(a=0) = e^(-3.0) ≈ 5%`).
- 5골 이상 경기 비율 ≈ 8~25%.
- 최대 골수: 대부분 0..8 범위 (10골 이상은 극히 드문).

**T6. 득점자 분포 (500 매치 batch, 라인 다양 분포 GK 3 / DF 8 / MF 8 / AT 6)**
- 라인별 가중치 합 (인원 × `scoringWeightByLine` × avg CA/100, avg CA ≈ 107):
  - AT: 6 × 5.0 × 1.07 ≈ 32
  - MF: 8 × 1.5 × 1.07 ≈ 13
  - DF: 8 × 0.4 × 1.07 ≈ 3.4
  - GK: 0
- 기대 비율: AT ~67% / MF ~27% / DF ~7% / GK 0%.
- 검증: AT > MF > DF / GK = 0% / AT 비율 55~80% 범위.

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
| **strengthExponent (k) 비선형 보정** | `pow(s, 1.5)` 로 CA 차이 골수 차이로 증폭 — V0.1 단순 CA 합이 결정력 부족 (k=1 시 강팀 원정 51%) 보강 위한 임시 변통 | V1.0+ 매치 엔진 재작성 시 finishing / composure 등 개별 stats 가 슈팅 변환률 직접 결정 → k=1 회귀 또는 알고리즘 자체 폐기 | 4단계 + Balancing + T3 임계치 |
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

### Purpose

- 단일 `Player` 의 **시장 가치 (Market Value)** 산출. 이적 협상의 기준점.
- 호출 시점:
  - **유저 검색 시 (이적시장)** — `TransferMarket.SearchPlayers` 결과에 가치 표시
  - **AI 응답 시** — `TransferSystem.ProcessOffers` 가 오퍼 금액 vs 시장가치 비교
  - **V1.0+ AI 영입 의사결정** — 다른 클럽 AI 가 영입 가치 평가

### Inputs

| Param | Type | Note |
| --- | --- | --- |
| `player` | `Player` | CA / PA / info.primaryPosition / state.injury / contract |
| `currentDate` | `DateTime` | 계약 잔여 기간 계산 |
| `balance` | `GameBalanceSO` | 모든 수치 외부화 |

### Outputs

`int marketValue` — 100,000 단위 반올림 (가독성).

### Logic — 6 요소 곱셈 공식

```
caFactor       = pow(CA / 100.0, balance.marketValueCaExponent)         # 기본 4.0 — 슈퍼스타 압도적
paGapBonus     = max(0, PA - CA) * balance.marketValuePaCoeff           # PA-CA 갭 가치 (잠재력)
ageFactor      = AgeCurve(age, balance)                                  # 연령별 가치 곡선
contractFactor = ContractCurve(remainingYears, balance)                  # 계약 잔여 기간
positionFactor = PositionFactor(line, balance)                            # 4라인 (GK/DF/MF/AT)
injuryFactor   = (player.state.injury.injuryTypeId == -1) ? 1.0 : balance.marketValueInjuryFactor

rawValue   = (balance.marketValueBase * caFactor + paGapBonus)
             * ageFactor * contractFactor * positionFactor * injuryFactor

marketValue = Round100k(max(0, rawValue))    # 100,000 단위 반올림
```

#### AgeCurve

연령별 가치 곡선 — 16~21 = 0.85 / 22~28 = 1.20 (피크) / 29~33 = 0.75 / 34+ = 0.35.

```
AgeCurve(age, balance) → float:
    if age <= 21:        return balance.marketValueAgeCurve[0]      # 0.85 (유망주)
    elif age <= 28:      return balance.marketValueAgeCurve[1]      # 1.20 (피크)
    elif age <= 33:      return balance.marketValueAgeCurve[2]      # 0.75 (노장)
    else:                return balance.marketValueAgeCurve[3]      # 0.35 (말년)
```

#### ContractCurve

계약 잔여 1년 = 0.50 (마지막 해, 자유 이적 임박 → 헐값) / 2년 = 0.80 / 3년 = 1.00 / 4+년 = 1.05.

```
ContractCurve(remainingYears, balance) → float:
    idx = clamp(remainingYears - 1, 0, balance.marketValueContractCurve.Length - 1)
    return balance.marketValueContractCurve[idx]
```

#### PositionFactor

`StartingSquadGacha.LineOf(position)` 재활용 (4 라인 분류). GK=0.75 / DF=0.85 / MF=1.00 / AT=1.20.

```
PositionFactor(line, balance) → float:
    return balance.marketValuePositionFactor[(int)line]    # Line enum 순서
```

### 예시 검증

| 선수 | 공식 | marketValue |
| --- | --- | --- |
| **평범** CA 100 PA 100, 25세 CM 잔여 3년 | 500k × 1.0 × 1.20 × 1.00 × 1.00 × 1.0 | **600k** |
| **강한** CA 150 PA 170, 24세 ST 잔여 4년 | (500k × 5.06 + 1M) × 1.20 × 1.05 × 1.20 × 1.0 | **5.3M** |
| **슈퍼스타** CA 180 PA 200, 22세 ST 잔여 5년 | (500k × 10.5 + 1M) × 1.20 × 1.05 × 1.20 × 1.0 | **9.5M** |
| **절정** CA 200 PA 200, 22세 ST 잔여 5년 | (500k × 16) × 1.20 × 1.05 × 1.20 × 1.0 | **14.5M** |
| **베테랑** CA 90 PA 95, 32세 CB 잔여 1년 | (500k × 0.656 + 250k) × 0.75 × 0.50 × 0.85 × 1.0 | **180k** |
| **부상자** CA 150 PA 170, 24세 ST 잔여 4년 / injury | 5.3M × 0.50 | **2.7M** |

**평범 vs 슈퍼스타 = 15.7배** 차이. 디자인 의도 "비교도 안 되게" 충족.

**ClubGen 자금과 균형**:
- 빅클럽 자금 ~9M / transferBudget 20% ≈ 1.8M
- 슈퍼스타 9~15M → 빅클럽도 한두 시즌 모아야 영입 가능

### Balancing Parameters → GameBalanceSO

```csharp
public int     marketValueBase            = 500_000;         // CA=100 기준점
public float   marketValueCaExponent      = 4.0f;            // pow 지수
public float   marketValuePaCoeff         = 50_000f;         // PA-CA 갭 1 = 50k
public float[] marketValueAgeCurve        = { 0.85f, 1.20f, 0.75f, 0.35f };  // 16~21 / 22~28 / 29~33 / 34+
public float[] marketValueContractCurve   = { 0.50f, 0.80f, 1.00f, 1.05f };  // 잔여 1/2/3/4+년
public float[] marketValuePositionFactor  = { 0.75f, 0.85f, 1.00f, 1.20f };  // GK/DF/MF/AT (Line enum 순서)
public float   marketValueInjuryFactor    = 0.50f;
```

### Edge Cases

| Case | 처리 |
| --- | --- |
| `CA == 0` (생성 오류) | `caFactor = 0`, `paGapBonus` 만으로 가치 계산. 최소 100k 보장 |
| `PA < CA` (있을 수 없으나 방어) | `paGapBonus = 0` (max 처리) |
| `remainingYears < 0` (계약 만료) | `ContractCurve` index 0 (잔여 1년 취급) + 경고. V1.0+ 자유계약 처리 |
| `remainingYears > Curve.Length` | Clamp 마지막 index |
| `age < 16 or > 40` | AgeCurve 가 마지막 index (말년) — 비정상 데이터 알림 |
| `positionFactor.Length != 4` | Assert |
| `player.state == null` | injuryFactor = 1.0 폴백 |
| 음수 rawValue | `max(0, rawValue)` |

### Test Scenarios

`Random(seed: 42)` 고정. 통계 테스트는 PlayerGen 1000명 batch + Market Value 계산 → 분포 검증.

**T1. 결정성**
- 같은 Player + 같은 currentDate + 같은 balance → 같은 marketValue.
- PlayerGen 의 결정성 (`#17`) 과 짝.

**T2. 슈퍼스타 vs 평범 — 차이 ≥ 10배**
- CA 180 PA 200 22세 ST 잔여 5년 / CA 100 PA 100 25세 CM 잔여 3년 → ratio ≥ 10.

**T3. AgeCurve — 피크 시기 = 가장 비싼**
- 같은 CA/PA, 25세 (피크) > 19세 (유망주) > 30세 (노장) > 35세 (말년)
- 22~28 구간이 최댓값.

**T4. ContractCurve — 잔여 1년이 가장 싸**
- 같은 선수, 잔여 1년 = 0.50 / 잔여 4년 = 1.05 (2.1배 차이).

**T5. PositionFactor — ST > MF > DF > GK**
- 같은 CA/PA, ST > MF > DF > GK.

**T6. Injury — 50% 디스카운트**
- 같은 선수, 부상자 = 정상의 0.50.

**T7. Round100k**
- 마이너 단위 무시. 530,000 / 9,500,000 처럼 100k 단위.

### V1.0+ Migration Notes (Market Value)

V0.1 단순 공식. V1.0+ 에서 정교화 가능한 항목 모두 기록:

| 항목 | V0.1 동작 | V1.0+ 변경 후보 | 영향 범위 |
| --- | --- | --- | --- |
| **선수 reputation** | 미반영 | `player.reputation: int` 신규 필드 — 빅네임 프리미엄 (`× pow(rep/50, 1.5)` 같은) | 새 도메인 필드 + 공식 |
| **club reputation** | 미반영 | 현 소속 클럽 명성 곱셈 보정 — 빅클럽 선수 가치 ↑ | 공식 |
| **form 보정** | 미반영 (V0.1 form=50 정적) | `state.form/50` 곱셈 (0.5~1.5) | 공식 + 시즌 시스템 |
| **morale 보정** | 미반영 (V0.1 morale=50 정적) | `state.morale/50` 곱셈 (0.7~1.3) | 공식 + 사기 시스템 |
| **이번 시즌 통계** | 미반영 | 현재 시즌 골/어시/평점 → 호황 시 가치 ↑ | `Player.career` 시즌 stat 사용 |
| **시장 수요** | 미반영 | 해당 포지션 부족 + 다른 클럽 관심 → 가치 ↑ | 새 시스템 (PositionDemandSystem) |
| **AskingPrice (보드 의지)** | 미반영 | 판매 의향 X → 1.5~3.0배 부풀림 | Board 시스템과 연동 |
| **에이전트 수수료** | 미반영 | 별도 외부화 — 이적료의 5~15% | 새 시스템 |
| **계약 만료 자유이적** | 잔여 0년 폴백 | 잔여 ≤ 6개월 → 자유이적 임박 (보스만 룰 #용어집) → 가격 폭락 / 자유이적 가능 | `EstimateInitialWage` 연동 |
| **포지션별 시장 prepay** | 4 라인 단순 | 14개 포지션 세분 (GK 특수 / ST 프리미엄 등) | 공식 |
| **세컨더리 포지션 가치** | 미반영 | secondaryPositions 보유 시 + 5~10% (다재다능) | 공식 |
| **트레잇 가치** | 미반영 | 트레잇별 가치 보정 (빅매치형 +, 부상취약 -) | 공식 + Trait 시스템 |
| **AgeCurve 정밀화** | 4 구간 | 매년 별도 곡선 (16=0.7 17=0.8 ... 28=1.30 29=1.20 30=1.05 31=0.90 ...) | 공식 + 외부화 |
| **시즌 인플레이션** | 미반영 | 시즌 진행에 따라 시장 인플레 (보드 자금 늘면 가치 ↑) | 새 시스템 |
| **국가 / 리그 차등** | 미반영 | EPL 선수 vs 하위 리그 선수 가치 차등 | LeagueConfigSO 필드 |

---

## 3.1. Transfer Flow (이적 흐름)

### Purpose

이적시장 / 오퍼 / AI 응답 / 체결의 전체 흐름. **이적시장 (검색·오퍼·협상)** 과 **이적시장 활성화 기간 (체결만)** 명확히 구분.

> **용어 정정**: "이적창" 모호 → **"이적시장 활성화 기간"** (Transfer Window 의 한국어 표현). 영어 코드 변수명은 `transferWindow*` 그대로 (도메인 표준 용어).

### Inputs / Outputs

`TransferSystem` 의 메서드 시그니처 (Sub-B 구현):

```csharp
public static class TransferSystem {
    public static int          CalculateMarketValue(Player p, GameState state, GameBalanceSO balance);
    public static TransferOffer SubmitOffer(int playerId, int fromClubId, int toClubId,
                                            int amount, Contract proposed,
                                            GameState state, GameBalanceSO balance);
    public static void          ProcessOffers(GameState state, GameBalanceSO balance);
    public static bool          IsTransferWindowOpen(DateTime date, GameBalanceSO balance);
    public static List<Player>  SearchPlayers(TransferSearchFilter filter, GameState state);
}
```

### Logic

전체 흐름:

```
[1] TransferMarket.SearchPlayers(filter, state)
    - 시점 제약 X — 언제든지 호출 가능 (이적시장 활성화 기간 무관)
    - V0.1: 모든 선수 정확한 CA/PA 노출 (스카우트 시스템 V1.0+)

[2] SubmitOffer(playerId, fromClubId, toClubId, amount, contract, state, balance)
    - 시점 제약 X — 활성화 기간 외에도 가능 (미리 협상)
    - 사전 검증: 양 구단 존재, 선수가 fromClubId 소속, amount > 0, contract 유효
    - TransferOffer 생성: status = Pending, id = state.nextOfferId++
    - state.activeOffers 추가
    - EventBus.Publish(new OfferSubmittedEvent { offerId })

[3] ProcessOffers(state, balance)  — DailyProcessor.Run 안에서 매일 호출
    foreach offer in state.activeOffers:
        switch offer.status:
            case Pending:
                AiRespondToOffer(offer, state, balance)
            case Accepted:
                if IsTransferWindowOpen(state.currentDate, balance):
                    CompleteTransfer(offer, state)
            // Rejected / Completed: skip

[3-a] AiRespondToOffer(offer, state, balance):
    rng = new Random(state.randomSeed ^ offer.id ^ state.currentDate.Ticks)
    marketValue = CalculateMarketValue(player, state, balance)
    
    # AI 평가 ±10% noise — 결정성 유지하며 정확한 가치 평가 어려움 표현
    noise = rng.NextNormal(1.0, balance.aiValueNoiseSigma)
    aiPerceivedValue = marketValue * max(0.5, noise)
    
    ratio = offer.amount / aiPerceivedValue
    if ratio >= balance.aiAcceptRatio:        # 1.20
        offer.status = Accepted
    else:
        offer.status = Rejected
    
    EventBus.Publish(new OfferRespondedEvent { offerId, newStatus })

[4] CompleteTransfer(offer, state):
    player = state.GetPlayer(offer.playerId)
    fromClub = state.GetClub(offer.fromClubId)
    toClub = state.GetClub(offer.toClubId)
    
    # 선수 이적
    player.currentClubId = toClub.id
    fromClub.seniorSquadIds.Remove(offer.playerId)
    toClub.seniorSquadIds.Add(offer.playerId)
    player.contract = offer.proposed     # 새 계약 적용
    
    # 자금 이동
    fromClub.finance.money += offer.amount
    toClub.finance.money -= offer.amount
    
    offer.status = Completed
    EventBus.Publish(new TransferCompletedEvent {
        offerId, playerId, fromClubId, toClubId, amount
    })
```

#### IsTransferWindowOpen

```
IsTransferWindowOpen(date, balance) → bool:
    summerStart = new DateTime(year, balance.transferWindowSummerStartMonth, balance.transferWindowSummerStartDay)
    summerEnd   = new DateTime(year, balance.transferWindowSummerEndMonth,   balance.transferWindowSummerEndDay)
    winterStart = new DateTime(year, balance.transferWindowWinterStartMonth, balance.transferWindowWinterStartDay)
    winterEnd   = new DateTime(year, balance.transferWindowWinterEndMonth,   balance.transferWindowWinterEndDay)
    return (date >= summerStart && date <= summerEnd) || (date >= winterStart && date <= winterEnd)
```

기본값:
- 여름: 6/1 ~ 8/31 (시즌 종료 직후)
- 겨울: 1/1 ~ 1/31 (시즌 중간)

#### TransferSearchFilter (Task 11.2)

```csharp
public class TransferSearchFilter {
    public Position? position;        // null = 전체
    public int minAge, maxAge;
    public int minCA, maxCA;
    public bool excludeUserClub = true;
}
```

```
SearchPlayers(filter, state) → List<Player>:
    return state.allPlayers
        .Where(p => filter.position == null || p.info.primaryPosition == filter.position)
        .Where(p => p.GetAge(state.currentDate) >= filter.minAge && p.GetAge(state.currentDate) <= filter.maxAge)
        .Where(p => p.currentAbility >= filter.minCA && p.currentAbility <= filter.maxCA)
        .Where(p => !filter.excludeUserClub || p.currentClubId != state.userClubId)
        .ToList()
```

> **V0.1 단순화**: 시점 제약 X / 정확도 100%. V1.0+ 스카우트 범위 / 정확도 / 시설 등급 영향.

### Edge Cases (이적 흐름)

| Case | 처리 |
| --- | --- |
| 같은 선수에 여러 오퍼 동시 | 허용. 각자 독립 처리. V1.0+ 선수가 최선 오퍼 선택 |
| `amount` < 0 또는 0 | `ArgumentException` |
| `fromClubId` 가 선수 소속 아님 | `ArgumentException` |
| `toClubId == fromClubId` | `ArgumentException` |
| `toClub.finance.money < amount` | V0.1 허용 (자금 부족 후 적자). V1.0+ Reject |
| 활성화 기간 외에 `Accepted` 오퍼가 쌓임 | `state.activeOffers` 에 보관 — 활성화 기간 시 일괄 체결 |
| Accepted 상태에서 player.currentClubId 변경 (다른 이적) | CompleteTransfer 가 fromClubId 검증 — 불일치 시 status = Rejected (또는 Completed 스킵) |
| `Completed` 상태 오퍼 후 처리 | `ProcessOffers` 가 skip (switch default) |
| `Rejected` 상태 오퍼 | `state.activeOffers` 에 잔존 (UI history 용). V1.0+ archive 검토 |

### Test Scenarios (이적 흐름)

`Random(seed: 42)` 고정.

**T1. 결정성 — AI 응답**
- 같은 offer.id / state.randomSeed / currentDate → 같은 AI 응답 (Accepted or Rejected).

**T2. SubmitOffer — 정상 흐름**
- offer 추가 / Pending / activeOffers 길이 +1 / OfferSubmittedEvent 발행.

**T3. AI 응답 — Acceptance**
- amount = marketValue × 1.30 → Accepted.
- amount = marketValue × 1.00 → Rejected (< 1.20).
- amount = marketValue × 1.20 (경계) → Accepted (이론, noise 영향 가능).

**T4. 체결 — 활성화 기간 안**
- Accepted 오퍼 + currentDate 8/15 (여름 활성화 기간) → ProcessOffers 호출 시 Completed.
- 선수 currentClubId / squad 갱신 / 자금 이동 / TransferCompletedEvent 발행.

**T5. Accepted 대기 — 활성화 기간 외**
- Accepted 오퍼 + currentDate 11/15 (활성화 기간 외) → status = Accepted 유지.
- currentDate 1/1 도달 시 다음 ProcessOffers → Completed.

**T6. 활성화 기간 검증**
- IsTransferWindowOpen(6/1) = true / 9/1 = false / 1/15 = true / 2/1 = false.

**T7. 시점 제약 X — 오퍼 / 검색**
- 11/15 currentDate 에 SubmitOffer 호출 → 정상 (Pending).
- 11/15 SearchPlayers 호출 → 정상 (시점 무관).

**T8. 검색 필터**
- position=ST / minAge=18 / maxAge=25 / minCA=120 → 조건 일치 선수만 반환.
- excludeUserClub=true → 유저 클럽 선수 제외.

**T9. 같은 선수 여러 오퍼**
- 두 클럽이 같은 player 에 오퍼 → 둘 다 Accepted 가능. 그 후 첫 체결 시 player.currentClubId 변경. 두 번째 체결 시 fromClubId 불일치 → 스킵.

### V1.0+ Migration Notes (Transfer Flow)

| 항목 | V0.1 동작 | V1.0+ 변경 후보 | 영향 범위 |
| --- | --- | --- | --- |
| **AI 협상 / 역제안** | 단일 라운드 (Accept/Reject) | CounterOffer status — 시장가치 × 1.3 역제안 + 유저 응답 라운드 | ProcessOffers + 새 status |
| **선수 개인 협상** | V0.1 자동 통과 | `Negotiating` 단계 — 주급 / 명성 / 출전시간 기대 / 야망 평가 | 새 시스템 |
| **AI 구단 영입 행동** | 미구현 (사용자 클럽만 오퍼) | AI 클럽이 자체 영입 결정 — 약점 포지션 / 자금 여유 / 명성 기준 | 새 AI 시스템 (CpuTransferAi) |
| **스카우트 시스템** | V0.1 정확도 100% | `Club.facilities.scoutLevel` 영향 — PA 추정치 범위 / 트레잇 노출 정도 / 검색 가능 영역 (국내 / 글로벌) | SearchPlayers + 새 ScoutingSystem |
| **에이전트 / 보너스** | 미구현 | 사이닝 보너스 / 에이전트 수수료 / 충성 보너스 등 추가 비용 | Contract + Finance |
| **이적창 외 협상 보류** | Accepted 대기 자동 체결 | 협상 상태 유지 (Negotiating) → 활성화 기간 진입 시 양 측 재확인 → 체결 / 무산 | ProcessOffers |
| **임대 (Loan)** | 미구현 | 임대 시스템 — 임대료 / 옵션 / 의무 영구 이적 조항 | 새 도메인 + 새 메서드 |
| **계약 갱신** | 미구현 | 만료 6개월 전부터 갱신 협상 (`data-flows.md` #6 시즌 종료 흐름) | 새 시스템 |
| **자유계약 (FA)** | 미구현 | 잔여 0년 선수는 자유이적 (이적료 0, 사이닝 보너스만) | SubmitOffer 분기 |
| **활성화 기간 시기 데이터** | balance.transferWindow* 4 쌍 외부화 | LeagueConfigSO 로 이전 — 리그별 다른 이적창 일정 | balance → LeagueConfigSO |
| **트랜스퍼 리스트 (Transfer Listed)** | `Player.state.transferListed` 필드만 존재, 미사용 | 보드 / 매니저가 선수 트랜스퍼 리스트 등록 → 시장가 × 0.7 거래 가능 | SubmitOffer + UI |
| **다른 클럽 인지 (Interest)** | 미구현 | 어떤 클럽이 어떤 선수에 관심 있는지 시스템 — 다중 오퍼 경쟁 | 새 시스템 |
| **이적 시장 활동량** | 미구현 | 시즌별 시장 활성도 (자금 인플레 / 빅 무브 등) — 시장가치 인플레이션 | 새 시스템 |
| **선수 transferListed 자동 결정** | 미구현 | 사기 낮음 / 출전시간 미달 시 자동 트랜스퍼 리스트 요청 | 사기 시스템 연동 |
| **다중 라운드 협상** | 단일 라운드 | 협상 라운드 수 외부화 + 라운드별 양 측 의지 변화 | ProcessOffers |

### Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2026-05-20 | All | Initial spec for V0.1. Market Value 6 요소 곱셈 공식 (CA pow 4 + PA gap + age curve + contract curve + position factor + injury). 슈퍼스타 vs 평범 ~15.7배 가격 차이. 이적 흐름 단일 라운드 (Submit → AI 응답 → Accepted 대기 → 활성화 기간 시 자동 체결). 이적시장 (검색·오퍼·협상) 상시 / 활성화 기간 (체결) 6/1~8/31 + 1/1~1/31. `design-decisions.md` #37 (V0.1 정책) 와 연동. V1.0+ Migration Notes 30+ 항목 (Market Value 15 + Flow 15+). |

---

## 4. Youth Pool Generation

### Purpose

- 단일 `YouthIntake` 생성 — 후보 선수 N명을 PlayerGenerator 로 만든 후 `GameState.allPlayers` 등록 + `YouthIntake` 객체 빌드.
- 호출 시점:
  - **메인 인스펙션** (6/15) — 시즌 종료 후, 풀 사이즈 ↑
  - **보조 인스펙션** (1/15) — 시즌 중간
  - `EventScheduler` 가 위 날짜 도래 시 `YouthSystem.GenerateIntake(club, state, ...)` 호출
- 추가 책임 (Task 10.2/10.3):
  - **리롤** — 토큰 1개 소모, 미영입 후보 제거 후 새 풀 생성
  - **영입 처리** — 선택된 후보 → `club.youthSquadIds`. 미영입 후보 → `GameState` 제거 (V0.1)

### Inputs

| Param | Type | Note |
| --- | --- | --- |
| `club` | `Club` | 인스펙션 대상 (V0.1: 유저 클럽만). `facilities.youthLevel` 사용. |
| `state` | `GameState` | `nextIntakeId` / `nextPlayerId` / `randomSeed` / `currentDate` / `userClub` 사용. 풀 추가/제거. |
| `balance` | `GameBalanceSO` | 모든 수치 외부화. |
| `db` | `GameDatabase` | `FacilityLevelSO(Youth, club.facilities.youthLevel)` / `PositionSO` / `TraitSO` / `NamePoolSO` 조회. |
| `leagueConfig` | `LeagueConfigSO` | 국적 분배 시 `countryCode` 사용. |

### Outputs

```csharp
public YouthIntake {
    public int id;                              // state.nextIntakeId++ 로 발급
    public int clubId;
    public DateTime intakeDate;                 // currentDate
    public List<int> candidatePlayerIds;        // 풀 사이즈만큼
    public List<int> signedPlayerIds;           // 영입 처리 후 (Task 10.3)
    public List<int> rejectedPlayerIds;         // 영입 처리 후 (Task 10.3)
    public int rerollsUsed;                     // 리롤 시 +1
}
```

- 후보 선수들은 `GameState.allPlayers` 에 등록 (`currentClubId = -1`, `origin = YouthIntake`, `youthClubId = club.id`).
- 영입 안 된 선수는 `signPlayers` 호출 후 GameState 에서 제거 (V0.1) — `rejectedPlayerIds` 에 ID 만 남음 (`design-decisions.md #7` 영구 저장).

### Logic

전체 순서:

```
1. 시드 고정         (state + 시점 + 유저 행동 + intake.id + rerollsUsed)
2. 풀 사이즈 결정     (FacilityLevelSO.youthPoolSize 직접 사용)
3. 후보 선수 N명 생성  (PlayerGenerator 재활용 + 유스 전용 PA/CA/나이/포지션/국적)
4. YouthIntake 빌드 + GameState 등록
```

리롤 / 영입 / 미영입 처리는 별도 메서드 (`Reroll`, `SignPlayers`).

#### 1단계: 시드 고정 — 외부 마이닝 + 직플 영상 공유 둘 다 방어

```
userActionHash = (state.userClubId >= 0)
    ? state.userClub.finance.money
      ^ (state.userClub.seniorSquadIds.Count * 7919)
      ^ (state.userClub.youthSquadIds.Count   * 9973)
      ^ (state.rerollTokens                   * 16007)
    : 0

rng = new Random(
    state.randomSeed
    ^ unchecked((int)state.currentDate.Ticks)   # 시점별 시드 (옵션 2)
    ^ userActionHash                            # 유저 행동 영향 (옵션 3)
    ^ club.id
    ^ intake.id                                 # state.nextIntakeId 발급
    ^ intake.rerollsUsed                        # 리롤마다 다른 풀
)
```

**효과** (`design-decisions.md` #35 V0.1 결정):

- **외부 시드 마이닝 차단** — `currentDate.Ticks` (100-nanosecond 단위, 단일 newgame seed 만으로 미래 시점 예측 어려움)
- **직플 영상 공유 사실상 차단** — `userActionHash` 가 자금 1원 / 영입 1명 / 토큰 1개 차이로도 변동. 완벽히 동일 플레이만 같은 결과.
- **결정성 보존 (`design-decisions.md #17`)** — 같은 newgame seed + 같은 행동 → 같은 결과 (세이브/로드 일관성)
- **리롤 메커닉 자연 발현** — `intake.rerollsUsed` 가 시드에 들어가 토큰 사용 시 자동으로 다른 풀

#### 2단계: 풀 사이즈 결정

```
facility = db.GetFacilityLevel(FacilityType.Youth, club.facilities.youthLevel)
poolSize = facility.youthPoolSize     # 시드 자산에서 외부화 (Lv1=15 / Lv5=30 등)
```

> **V0.1 시설 통합 정책 (`design-decisions.md #35`)**: `FacilityLevelSO(Youth)` 가 V0.1 에선 "유소년 시스템 종합 등급" (시설 + 코치 + 모집 통합). V1.0+ 에서 `Club.youthCoachLevel` / `Club.youthRecruitmentLevel` 분리.

#### 3단계: 후보 선수 N명 생성

```
candidates = []
nextId = state.nextPlayerId

for i in 0..poolSize:
    age         = SampleYouthAge(rng, balance)
    nat         = SampleYouthNationality(rng, leagueConfig.countryCode, db, balance)
    position    = (Position)rng.Next(0, 14)        # V0.1: 균등 랜덤 (14개 포지션)
    pa          = SampleYouthPA(rng, facility, balance)
    
    player = BuildYouthPlayer(rng, nextId, age, nat, position, pa,
                              club.id, currentDate, balance, db)
    
    candidates.add(player.id)
    state.AddPlayer(player)
    nextId++

state.nextPlayerId = nextId
```

**PA 샘플링 — 스타 픽 메커닉 (사용자 의도: 톡톡 튀는 천재 발굴)**:

```
SampleYouthPA(rng, facility, balance) → int:
    isStar = rng.NextDouble() < balance.youthStarPickProbability     # 0.05 (5%)
    mu     = facility.youthAvgPA + (isStar ? balance.youthStarPaBonus : 0)   # +50 if star
    rawPA  = rng.NextNormal(mu, balance.youthPaStdDev)               # σ=15
    return Clamp(round(rawPA), balance.minPA, balance.maxPA)
```

- 시설 Lv1 (avgPA 100) 도 5% 확률 평균 150 PA 천재 등장
- 시설 Lv5 (avgPA 150) 도 5% 확률 평균 200 (상한) 슈퍼 천재
- **재미 디자인**: 시설 구려도 가끔 슈퍼유망주 발굴, 시설 좋아도 평범한 풀 가능

**나이 샘플링 — 가중치 분포 (16/17 위주)**:

```
SampleYouthAge(rng, balance) → int:
    # balance.youthIntakeAgeWeights = [0.40, 0.40, 0.20] (16, 17, 18 순)
    return rng.WeightedSample([16, 17, 18], balance.youthIntakeAgeWeights)
```

**국적 샘플링 — 자국 비율 ClubGen 보다 ↑**:

```
SampleYouthNationality(rng, leagueCountry, db, balance) → string:
    if rng.NextDouble() < balance.youthPrimaryNationalityRatio:    # 0.78 (자국 78%)
        return leagueCountry
    others = db.AllCountries.Where(c => c.code != leagueCountry).ToList()
    return others.empty ? leagueCountry : others[rng.Next(others.Count)].code
```

**선수 빌드 — PlayerGenerator 부분 재활용 + CA-PA 역방향**:

PlayerGen 은 `CA → PA` 방향 (CA 진실값). 유스는 **PA → CA 역방향** (PA 진실값, CA derived).

```
BuildYouthPlayer(rng, id, age, nat, position, pa,
                 clubId, currentDate, balance, db) → Player:
    
    # ── CA 결정 (PA 역방향, CA-PA 의존성 약화) ──
    ageBlend = Clamp01((age - balance.youthIntakeMinAge) /
                       (balance.paGapZeroAge - balance.youthIntakeMinAge))
    caGap    = Lerp(balance.paGapMaxMean, 0, ageBlend)             # 16세 → ~46
    rawCA    = pa - rng.NextNormal(caGap, balance.youthPaGapStdDev) # σ=25 (PlayerGen σ=15 × 1.67)
    ca       = Clamp(round(rawCA), balance.minCA, pa)               # CA ≤ PA 보장
    
    # ── Stats 분배 (PlayerGen 3단계 재활용) ──
    stats    = FillStatsByPlayerGen(rng, ca, position, balance, db)
    
    # ── 트레잇 (PlayerGen 4단계 재활용) ──
    traitIds = SelectTraitsByPlayerGen(rng, balance, db)
    
    # ── 인적사항 ──
    namePool   = db.GetNamePool(nat)
    firstName  = rng.Choice(namePool.firstNames)
    lastName   = rng.Choice(namePool.lastNames)
    birthDate  = SampleBirthDate(rng, currentDate, age)             # PersonalInfo.birthDate 저장 (age 별도 X)
    foot       = SampleFoot(rng, balance)                           # PlayerGen 5단계 재활용
    secondaryPositions = GenerateSecondaryPositions(position, rng, db, balance)
    
    # ── 계약 (V0.1 단순) ──
    contract = new Contract {
        weeklyWage = EstimateInitialWage(ca, age, balance),
        startDate  = currentDate,
        endDate    = currentDate.AddYears(rng.Range(2, 5)),         # 2~4년 (유스는 일반 1~4 보다 약간 길게)
        releaseClause = 0,
    }
    
    return new Player {
        id = id, info = {firstName, lastName, birthDate, nat, position, secondaryPositions, foot},
        stats = stats, currentAbility = ca, potentialAbility = pa,
        traitIds = traitIds,
        currentClubId = -1,           # 미소속 — 영입 시 club.id 로 갱신
        youthClubId = clubId,         # 인스펙션 출처 (영입 안 돼도 기록)
        origin = PlayerOrigin.YouthIntake,
        contract = contract,
        state = new PlayerState {
            fatigue = 0, morale = 50, form = 50,
            injury = new InjuryInfo { injuryTypeId = -1 },
        },
        career = [],
        faceSeed = rng.Next(),
    }


SampleBirthDate(rng, currentDate, age):
    # age 가 currentDate 기준 만 나이가 되도록 birthDate 계산
    birthYear  = currentDate.Year - age
    birthMonth = rng.Range(1, 13)
    birthDay   = rng.Range(1, 29)        # 1..28 (28일까지 안전하게)
    return new DateTime(birthYear, birthMonth, birthDay)
```

> **CA-PA 의존성 약화 (사용자 의도)**: `youthPaGapStdDev=25` (PlayerGen `paGapStdDev=15` 의 1.67배). 같은 PA 라도 CA σ 가 커 PA 추정 어려움.
>
> 예: PA=150 → CA ≈ 100±25 (75~125). PA=120 → CA ≈ 70±25 (45~95). **CA 70 선수가 PA 100~145 어디든 가능** — 단순 CA 만으로 PA 추정 불가.

#### 4단계: YouthIntake 빌드 + GameState 등록

```
intake = new YouthIntake {
    id           = state.nextIntakeId++,
    clubId       = club.id,
    intakeDate   = state.currentDate,
    candidatePlayerIds = candidates,
    signedPlayerIds    = [],
    rejectedPlayerIds  = [],
    rerollsUsed        = 0,
}
club.intakeHistory.Add(intake)
EventBus.Publish(new YouthIntakeAvailableEvent { intakeId = intake.id, clubId = club.id })
```

### Reroll 메커닉 (Task 10.2)

```
UseRerollToken(intake, club, state, balance, db, leagueConfig):
    if state.rerollTokens <= 0:
        throw InvalidOperationException("토큰 부족")
    
    state.rerollTokens -= 1
    intake.rerollsUsed += 1
    
    # 영입 안 된 기존 후보 제거 (signedPlayerIds 는 유지)
    foreach id in intake.candidatePlayerIds.Where(id => id not in intake.signedPlayerIds):
        state.RemovePlayer(id)
    intake.candidatePlayerIds.Clear()
    intake.candidatePlayerIds.AddRange(intake.signedPlayerIds)   # 이미 영입한 선수는 유지
    
    # 새 후보 생성 — 시드는 rerollsUsed 가 +1 됐으므로 자동으로 다른 결과
    # (Logic 3단계 재호출, candidates 에 새 풀 추가)
    [3단계 반복, candidates 를 intake.candidatePlayerIds 에 추가]
    
    EventBus.Publish(new YouthRerolledEvent { intakeId, remainingTokens = state.rerollTokens })
```

### 영입 / 미영입 처리 (Task 10.3)

```
SignPlayers(intake, playerIds, club, state):
    # 영입
    foreach id in playerIds:
        player = state.GetPlayer(id)
        player.currentClubId = club.id
        club.youthSquadIds.Add(id)
        intake.signedPlayerIds.Add(id)
    
    # 미영입 (V0.1 — 모두 GameState 제거, ID 만 보관)
    foreach id in intake.candidatePlayerIds:
        if id not in intake.signedPlayerIds:
            state.RemovePlayer(id)
            intake.rejectedPlayerIds.Add(id)
    
    # candidatePlayerIds 비우기 (영입된 ID 는 signedPlayerIds, 거절된 ID 는 rejectedPlayerIds 에)
    intake.candidatePlayerIds.Clear()
    
    EventBus.Publish(new YouthSignedEvent { intakeId, signedPlayerIds = playerIds })
```

> **V0.1 단순화 (`design-decisions.md #35`)**: 미영입 후보 모두 제거. V1.0+ 에서 일정 확률로 AI 다른 구단 영입.

### Balancing Parameters → GameBalanceSO

```csharp
// === Youth Intake (algorithms.md #4) ===
public float   youthStarPickProbability     = 0.05f;      // 5% 스타 픽
public float   youthStarPaBonus             = 50f;        // 스타 PA 평균 보너스
public float   youthPaStdDev                = 15f;        // PA 분포 σ
public float   youthPaGapStdDev             = 25f;        // CA-PA 갭 σ (PlayerGen 의 1.67배)
public int     youthIntakeMinAge            = 16;
public int     youthIntakeMaxAge            = 18;
public float[] youthIntakeAgeWeights        = { 0.40f, 0.40f, 0.20f };  // 16, 17, 18 순
public float   youthPrimaryNationalityRatio = 0.78f;      // 자국 78% (ClubGen 의 0.70 보다 ↑)
public int     youthIntakeMainMonth         = 6;
public int     youthIntakeMainDay           = 15;         // 메인: 6/15
public int     youthIntakeSecondMonth       = 1;
public int     youthIntakeSecondDay         = 15;         // 보조: 1/15
```

> 기존 PlayerGen 필드 (`paGapMaxMean`, `paGapZeroAge`, `caRepBase`, `caRepCoeff`, `traitProbabilityPerPlayer`, `additionalTraitProbability`, `secondaryPositionProbability`, `thirdPositionProbability`, `footRightRatio/leftRatio/bothRatio`, `wageBaseAtMinCA`, `wagePerCAPoint`, `wageFloor`) 재활용.

### Edge Cases

| Case | 처리 |
| --- | --- |
| `club.facilities.youthLevel` 범위 밖 (Lv 1~5 외) | Assert + 경고. Lv1 폴백. |
| `FacilityLevelSO(Youth, level)` 미등록 | 경고 로그 + Lv1 SO 폴백 + GenerateIntake 진행. |
| `db.AllCountries` 가 leagueCountry 하나뿐 | 외국인 추첨 시 leagueCountry 폴백 (ClubGen 패턴). |
| `state.userClubId < 0` (테스트 등) | userActionHash = 0. 시드 분산 약화하지만 동작 OK. |
| `state.nextIntakeId` 미초기화 (= 0) | 첫 인스펙션 = 1 발급 후 +1 (PlayerGen `nextPlayerId` 패턴). |
| Reroll 시 `state.rerollTokens == 0` | `InvalidOperationException`. UI 단에서 미리 차단해야 함. |
| Reroll 시 `intake.candidatePlayerIds` 비어있음 (모두 영입됨) | 새 풀만 추가. signedPlayerIds 유지. |
| SignPlayers 의 `playerIds` 가 빈 리스트 | 미영입 전체 제거만 수행. EventBus 정상 발행. |
| SignPlayers 의 `playerIds` 에 `candidatePlayerIds` 외 ID 포함 | Assert + 경고. 무시 (skip). |
| age 가 `[youthIntakeMinAge, youthIntakeMaxAge]` 외 | Assert (가중치 정의 오류). |
| Stats 분배 / 트레잇 부여 — PlayerGen 의 모든 edge case 동일하게 적용 |  |

### Test Scenarios

`Random(seed: 42)` 고정. 통계 테스트는 1000명 batch (10 인스펙션 × 100명 각).

**T1. 결정성 — 같은 입력 동일 결과**
- 같은 `state.randomSeed` + 같은 `state.currentDate` + 같은 `userActionHash` (자금/스쿼드/토큰) + 같은 `intake.id` + 같은 `rerollsUsed` → 동일 풀.

**T2. 시드 옵션 검증 — userActionHash 가 시드에 영향**
- 같은 newgame seed 두 fixture. 한쪽 `state.userClub.finance.money` 만 +1 변경.
- → 두 fixture 의 intake 풀이 다름 (대부분 / 100% 보장 X — XOR collision 가능성 있으나 우연 수준).

**T3. 풀 사이즈 — FacilityLevelSO 직접 사용**
- Lv1 시설 → `candidatePlayerIds.Count == facility.youthPoolSize`
- Lv5 시설 → 같은 검증

**T4. PA 분포 — 스타 픽 효과 (1000명 batch)**
- 시설 Lv1 (avgPA=100) 1000명 생성:
  - 평균 PA ≈ 100 + (0.05 × 50) = 102.5 ±5
  - 일반 분포 (95%) 의 σ 검증 + 스타 (5%) 의 PA 평균이 150 근처
  - **PA 140 이상 비율 ≈ 5% (스타 픽 효과 확인)**
- 시설 Lv5 (avgPA=150) → 평균 152.5 / PA 190 이상 비율 ≈ 5%

**T5. CA-PA 의존성 약화 — corr(CA, PA) < 0.85**
- 1000명 batch (시설 등급 다양) → CA / PA 의 피어슨 상관계수 < 0.85
- (PlayerGen 의 derived CA 와 비교 — PlayerGen 은 ~0.7 정도, 유스는 σ 큰 만큼 ~0.6 정도 기대)

**T6. 나이 가중치 분포 (1000명 batch)**
- age 16 비율 ≈ 40% ±5%
- age 17 비율 ≈ 40% ±5%
- age 18 비율 ≈ 20% ±5%

**T7. 국적 분포 (500명 batch, leagueCountry="ENG")**
- ENG 비율 ≈ 78% ±5%
- 외국 합 ≈ 22% ±5%

**T8. 시설 등급별 평균 PA 차이 — 핵심 디자인 검증**
- Lv1 (avgPA=100) batch 1000명 평균 PA < Lv5 (avgPA=150) batch 1000명 평균 PA
- 평균 차이 ≈ 50 (시드 자산의 youthAvgPA 차이) ±5

**T9. Reroll — 토큰 차감 + 풀 변경**
- state.rerollTokens=3 → UseRerollToken → tokens=2
- intake.rerollsUsed=0 → 1
- 새 풀의 candidatePlayerIds 가 기존과 다름 (영입 안 된 ID 제거됨)

**T10. SignPlayers — 영입 / 미영입 처리**
- 풀 15명 중 3명 영입:
  - 영입된 3명: `currentClubId = club.id` / `club.youthSquadIds` 추가 / `intake.signedPlayerIds` 추가
  - 미영입 12명: `state.allPlayers` 에서 제거 / `intake.rejectedPlayerIds` 에 ID 만 보관

### V1.0+ Migration Notes

| 항목 | V0.1 동작 | V1.0+ 변경 후보 | 영향 범위 |
| --- | --- | --- | --- |
| **유스 시설 통합 등급** | `FacilityLevelSO(Youth)` 가 시설 + 코치 + 모집 통합 | `Club.youthCoachLevel` / `Club.youthRecruitmentLevel` 분리. 시설 등급은 다른 효과 (인지도 / 외국 유스 영입 가능) | 2단계 + `Club` 도메인 + 새 SO |
| **포지션 균등 랜덤** | 14개 포지션 균등 | 라운드별 가중치 가챠 — 어떤 인스펙션은 GK 0명, AT 다수 / 다른 인스펙션은 반대 | 3단계 + `youthPositionWeightVolatility` 신규 |
| **미영입 후보 V0.1 단순 제거** | 모두 GameState 제거 | 일정 확률로 AI 다른 구단 영입 → 후속 알림 이벤트 | SignPlayers + 새 AI 시스템 |
| **CA-PA 의존성** | σ=25 로 약화 | finishing / composure / decisions 같은 개별 stats 가 CA 표면적 능력에 가산 → 같은 PA 라도 stats 분포에 따라 CA 다양화 | `algorithms.md #1 V1.0 변경 트리거` (CA-Stats Option B) 와 짝 |
| **트레잇 가중치** | PlayerGen 동일 | 유스 시설 등급별 "고급 트레잇 (빅매치형 등)" 가중치 ↑ | 트레잇 부여 단계 |
| **시드 강화 (옵션 3)** | userActionHash = 4 필드 (finance / squad / youth / tokens) | hash 정교화 — `intakeHistory.Sum(...)` (과거 영입 패턴) / `state.activeOffers.Count` 등 추가 | 1단계 + Sub-A 명세 갱신 |
| **다른 클럽 인스펙션 (AI 영입)** | V0.1: 유저 클럽만 호출 | 시즌 사이클에 AI 클럽도 인스펙션 → 다른 클럽 유스 영입 결정 | 새 호출자 + AI 의사결정 |
| **추가 스카우트 (data-flows.md #4 [3-c])** | V0.1: 정보 정확도 시스템 부재 | 비용 차감 + UI 정보 정확도 ↑ (PA 추정치 범위 좁힘 / 트레잇 노출) | 새 시스템 |
| **인스펙션 시기 데이터** | 6/15 / 1/15 (월/일만 외부화) | LeagueConfigSO 로 이전 — 리그별 다른 인스펙션 일정 | balance → LeagueConfigSO |
| **계약 기간** | 2~4년 균등 | 시설 / 나이 / PA 에 따라 차등 (천재는 짧게, 잠재력 낮으면 길게) | BuildYouthPlayer + Contract |

### Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2026-05-20 | All | Initial spec for V0.1. PA 진실값 / CA derived 역방향 모델 (PlayerGen 과 대비). 스타 픽 메커닉 (5% PA +50). 시드 = currentDate.Ticks + userActionHash 결합 (외부 마이닝 + 직플 영상 공유 둘 다 방어). 시설 통합 등급 V0.1 + V1.0+ 분리 명세. 포지션 V0.1 균등 / V1.0+ 가중치 변동 트리거. 미영입 V0.1 모두 제거 / V1.0+ AI 영입 트리거. age 가중치 16=40/17=40/18=20. 국적 자국 78%. `design-decisions.md` #35 (V0.1 정책) / #36 (`GameState.nextIntakeId`) 와 연동. |

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
| 2026-05-19 | #2 Test Scenarios | Sub-C 본 구현 시 정규근사 (Skellam) 재계산 + 실측 검증 결과로 T3~T6 임계치/매치수 미세조정 (#113). T3 강팀 승률 70%→60% (홈) / 60%→45% (원정) — 정규근사 기대 ~64%/51% 에 표본오차 마진. 강팀 원정은 거의 50/50 (홈 보너스가 약팀 측 가산되는 게 큰 영향). T4 분포 명세 추가 (45/22/33%). T5 무득점 비율 8~10%→2~10% (이론 5%, 명세 초안 오기 수정). T6 매치 수 1000→500, 라인 분포 명세화 (GK3/DF8/MF8/AT6) + 가중치 합 계산 명시. **시드 well-distributed 정책 추가** — `(seedBase+i)^i` collision 회피 위해 `seedGen.Next()` 패턴 명시. |
| 2026-05-19 | #2 4단계 / Balancing / V1.0 Notes / T3 | **`strengthExponent` (k) 도입** (#113). 단순 선형 ratio 가 CA 1.89배 차이를 골 1.43배 차이로만 반영 → 강팀 원정 51% 라 디자인 의도 (압도적 강팀이 자주 이김) 부족. `pow(s, k)` 비선형화로 강팀 우월함 증폭 (k=1.5 기본 → 강팀 홈 72% / 원정 59%). V0.1 임시 변통 — V1.0+ 매치 엔진 재작성 시 finishing 등 개별 stats 가 결정력 직접 표현하므로 k=1 회귀 또는 폐기. T3 임계치 재조정 (홈 60→65, 원정 45→50). |
| 2026-05-20 | Priority Order + #4 | Youth Pool Generation `## 4` 섹션 신규 작성 (Task 10 Sub-A, #123). PA 진실값 / CA derived 역방향 모델. 스타 픽 메커닉 (5% PA bonus). 시드 = `currentDate.Ticks` + `userActionHash` 결합 (외부 마이닝 + 직플 영상 공유 둘 다 방어). V0.1 시설 통합 등급 + V1.0+ 분리 명세. `design-decisions.md` #35/#36 와 연동. |
| 2026-05-20 | Priority Order + #3 + #3.1 | Market Value + Transfer Flow `## 3` 섹션 신규 작성 (Stage 11 Sub-A, #130). Market Value 6 요소 곱셈 공식 (CA pow 4 + PA gap + age + contract + position + injury). 슈퍼스타 vs 평범 15.7배 차이 (사용자 의도 "비교도 안 되게"). 이적 흐름 — 이적시장 (검색·오퍼·협상) 상시 / 이적시장 활성화 기간 (체결) 6/1~8/31 + 1/1~1/31. Accepted 대기 → 활성화 기간 시 자동 체결. AI 응답 ±10% noise. V0.1 단일 라운드 / 선수 자동 통과 / AI 영입 미구현. `design-decisions.md` #37 연동. V1.0+ Migration Notes 30+ 항목. |
