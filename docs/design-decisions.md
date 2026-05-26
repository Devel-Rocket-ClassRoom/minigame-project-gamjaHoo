# Design Decisions

이 문서는 프로젝트의 모든 설계 결정과 **그 이유**를 기록한다. "왜 이렇게 했나"에 답할 수 있어야 한다.

새 결정이 생기면 이 문서에 추가하고, 기존 결정을 바꾸면 변경 이력에 기록한다.

---

## 1. ID 기반 참조 (Direct reference 금지)

**결정:** 모든 도메인 객체 간 참조는 ID(int)로만 한다. 직접 객체 참조 금지.

```csharp
// ❌ 안 함
public class Player {
    public Club currentClub;
}

// ✅ 함
public class Player {
    public int currentClubId;
}
```

**이유:**
- JSON 직렬화 시 순환 참조 문제 회피
- 세이브 파일 크기 감소
- 객체 풀에서 빠른 조회 (Dictionary 인덱스로 O(1))

**조회 방법:** `gameState.GetPlayer(id)` 같은 헬퍼 메서드 사용.

---

## 2. GameState — 단일 진실의 원천

**결정:** 모든 도메인 인스턴스는 `GameState` 하나에 모인다. 세이브 파일은 `GameState` 직렬화 결과.

```csharp
public class GameState {
    public List<Player> allPlayers;
    public List<Club> allClubs;
    public List<League> leagues;
    public List<TransferOffer> activeOffers;
    public DateTime currentDate;
    public int userClubId;
    public int rerollTokens;
    public int randomSeed;
}
```

**이유:**
- 세이브/로드 단순화 (직렬화 대상이 명확)
- 데이터 흐름 추적 용이
- 인덱스 동기화 지점이 단일

---

## 3. Stateless Systems

**결정:** Application Layer의 시스템(`MatchSimulator`, `TransferSystem` 등)은 상태를 가지지 않는다. 모든 상태는 `GameState`에 있고, 시스템은 `GameState`를 입력받아 변경한다.

**이유:**
- 시스템 자체는 직렬화 대상 아님
- 테스트 용이
- 의존성 명확 (입력 → 출력)

---

## 4. Composition vs Aggregation 명확 구분

**결정:**
- **Composition (◆):** 부모 없이 못 사는 객체 (`Club → Finance`, `Player → Stats`)
- **Aggregation (◇):** 부모와 별개로 존재 가능 (`League ↔ Club`, `YouthIntake ↔ Player`)

**이유:**
- 삭제 시 cascade 여부 명확
- 데이터 흐름 추적 용이

---

## 5. Stats 표현 — 평탄한 필드 (Option A)

**결정:** `Stats`는 Dictionary가 아닌 평탄한 int 필드로 구성한다.

```csharp
public class TechnicalStats {
    public int passing;
    public int shooting;
    public int tackling;
    // ...
}
```

**이유:**
- 타입 안전 (오타 시 컴파일 에러)
- 자동완성 동작
- 인스펙터 표시 자동
- JSON 출력 가독성

**트레이드오프:** 일괄 처리는 카테고리 클래스마다 `ApplyToAll(Func<int,int>)` 헬퍼를 한 번 만들어 해결.

---

## 6. YouthIntake — Player를 참조만 (Aggregation)

**결정:** `YouthIntake`는 `Player`를 소유하지 않는다. ID 리스트로 후보/영입/거절을 추적할 뿐.

**이유:**
- 영입 안 된 후보 선수도 게임 세계에 계속 존재해야 함 (다른 구단으로 가거나 무명으로)
- 모든 선수의 단일 소유자는 `GameState`

---

## 7. YouthIntake 영구 저장

**결정:** 인스펙션 이력은 게임 종료까지 보관한다.

**이유:**
- 회고 재미 ("그때 그 선수 지금 어디 갔지?")
- 게임의 핵심 시스템이라 가치 있음
- 데이터 크기 영향은 적음 (시즌당 2회 × 풀 사이즈)

---

## 8. Match 데이터 — 시즌 종료 시 압축

**결정:** 현재 시즌 경기는 풀 데이터 보관, 이전 시즌은 요약만.

**이유:**
- 세이브 파일 크기 관리
- 과거 경기 디테일은 회고 가치 낮음 (우승/강등팀, 시상 정도만)

**압축 시점:** 시즌 종료 직후.

---

## 9. 비활성 구단 — 동일 클래스 + 플래그

**결정:** 비활성 구단도 `Club` 클래스 사용. `isActiveSimulation` 플래그로 처리 깊이 결정.

**이유:**
- `ClubLite` 별도 클래스 분리하면 코드 중복
- 활성/비활성 전환 가능 (유저가 관심 가지면 활성화)
- 비활성이면 일부 필드를 null 또는 빈 리스트로 표현

---

## 10. 네임스페이스 — 평탄

**결정:** `FMLite.Core`, `FMLite.Domain`, `FMLite.Application`, `FMLite.UI` 정도. 도메인 내부 세분화 안 함.

**이유:**
- 단순성 우선
- using이 너무 많아지지 않음
- 나중에 필요하면 분리 가능

---

## 11. 수치 외부화 — ScriptableObject

**결정:** 게임 룰 / 밸런싱 수치는 코드에 박지 않고 `GameBalanceSO` 등 SO로 외부화한다.

**이유:**
- 밸런싱 시 코드 수정 불필요
- 인스펙터에서 직접 편집 가능
- 핫리로드 가능

**원칙:** "이 숫자 어디서 왔지?" 사태 방지. 매직 넘버 금지.

---

## 12. ScriptableObject vs 일반 클래스 구분

**결정:**
- **ScriptableObject (게임 룰북):** TraitSO, PositionSO, FacilityLevelSO, GameBalanceSO 등. 빌드에 포함, 플레이 중 안 바뀜.
- **일반 클래스 (게임 인스턴스):** Player, Club, Match 등. 게임 시작 후 생성/변경, JSON 직렬화 대상.

**판단 기준:**
- 플레이 중 바뀌나? → 일반 클래스
- 종류별 하나? 인스턴스 여러 개? → 종류별 하나면 SO
- 개발자가 만드나? 게임이 생성하나? → 개발자가 만들면 SO

**SO 참조:** 일반 클래스에서는 SO를 직접 참조하지 않고 ID로 참조. `GameDatabase.GetTrait(id)` 같은 헬퍼로 조회.

---

## 13. 초기 스쿼드 가챠 — 전체 리롤만

**결정:** 게임 시작 시 구단 스쿼드는 랜덤 생성. 라인별 리롤 없이 **전체 리롤만** 3회 제공.

**이유:**
- 라인별 리롤 허용 시 체리피킹으로 모든 라인이 상위 분포로 수렴
- 결과적으로 구단 명성과 무관하게 최강 스쿼드 조립 가능
- 전체 리롤만 허용해야 라인 간 트레이드오프가 살아남 (수비 좋은데 공격 약함 등)
- 이 트레이드오프가 의사결정의 재미

---

## 14. 정보 표시 — 5단계 티어, 정확한 수치 숨김

**결정:** 스쿼드 평가는 4라인 × 5단계 티어로만 표시. 평균 CA / 오버롤 점수는 숨김.

**이유:**
- 정확한 수치 노출 시 단일 지표 최적화 패턴 발생
- "OO 라인 X점 이상 나올 때까지 리롤" 같은 행동 유도됨
- 티어 표시는 비교가 주관적이게 함 (의사결정 흥미 유지)

**5단계:** Elite (10%) / Strong (30%) / Average (40%) / Weak (20%) / Poor (10%)

---

## 15. 티어 — 명성 대비 상대평가

**결정:** 티어 기준은 절대 수치가 아닌 구단 명성 대비 상대평가.

**이유:**
- 작은 구단으로 시작해도 "우리 공격진 훌륭함!" 같은 만족감 가능
- 빅클럽의 "평범" ≈ 중위권의 "훌륭" (절대값으로는 후자가 낮을 수도)

---

## 16. 리롤 토큰 시스템화

**결정:** 세이브 로드 리롤을 토큰이라는 게임 자원으로 흡수.

**이유:**
- 유저 행동(편법) → 게임 메커닉
- 세이브 로드 = 게임 전체 리로드 (느림, 번거로움)
- 토큰 = 클릭 한 번 (편하고 합법)
- 편한 길이 합법 경로면 유저는 자연스럽게 그쪽 선택

**지급:** 시즌 시작 3개 + 시설 업그레이드 / 보드 미션 시 +1, 이월 최대 5개

---

## 17. 경기 결과 — 시작 직전 시드 고정

**결정:** 경기 결과는 경기 시작 직전 시드를 고정하고 미리 산출한다. 이후 표시되는 이벤트들은 그 결과에 부합하도록 생성.

**이유:**
- 결과 검증 가능
- 세이브 로드 후 같은 결과 보장 (멀티 세이브 일관성)

---

## 18. 외부 의존성 최소화

**결정:** DOTween만 사용. Odin Inspector / Zenject 등 미사용.

**이유:**
- 누구나 빌드 가능한 코드베이스
- 유료 에셋 의존성 회피
- 포트폴리오 관점에서 감점 회피
- 커스텀 에디터는 필요할 때 직접 작성 (Assets/_Project/Scripts/Editor/)

---

## 19. EPL 기준 일정

**결정:** 8월 중순 개막 → 5월 중순 종료. 회계연도 6/1 시작.

**이유:**
- 박싱데이 일정 같은 특유의 매력 살리기
- 6월 메인 유스 인스펙션 / 1월 보조 인스펙션 자연스러움

---

## 20. 경기 시각화 포기

**결정:** 2D/3D 매치 엔진 없음. 텍스트 결과만.

**이유:**
- 2주 스코프에서 매치 엔진 구현 불가
- 컴팩트 게임 콘셉트와 충돌
- 동수칸/종식이 콘텐츠도 결국 텍스트 위주

---

## 21. 직렬화 — Newtonsoft.Json

**결정:** Unity의 `JsonUtility` 대신 Newtonsoft.Json 사용.

**이유:**
- Dictionary 직렬화 지원
- 다형성 직렬화 가능
- 사람이 읽을 수 있는 출력 (디버깅 용이)
- 버전 호환 옵션 풍부

---

## 22. 폰트 — NotoSansKR (OFL, Variable Font)

**결정:** 한국어 UI는 NotoSansKR (Open Font License) Variable Font 사용. TMP Font Asset은 Dynamic Mode로 운영 (atlas 1024 → 런타임 자동 확장).

**이유:**
- OFL 라이선스 → public repo에 폰트 파일 동봉 가능 (재배포 자유)
- Variable Font 하나로 모든 weight 지원
- Dynamic Mode → 사전 글리프 베이크 불필요 (사용 글자 풀이 미정인 1인 프로토타입에 최적)

**변경 이력:** 초안에서는 Malgun Gothic이었으나 (a) Windows 종속 (b) 재배포 라이선스 모호 두 이유로 NotoSansKR로 변경 (Task 1.3 진행 중).

**대체:** V1.x 에서 Pretendard 등 미려한 폰트 고려.

---

## 23. GameTime — 자체 상태 보유 (V0.1 한정)

**결정:** `GameTime` 은 static 클래스로 자체 `CurrentDate` 상태를 보유한다. `GameState.currentDate` 와는 진입/저장 시점에 양방향 동기화.

```csharp
public static class GameTime {
    public static DateTime CurrentDate { get; private set; }
    public static void Reset(DateTime d) { ... }
    public static void Advance(int days) { ... }   // 하루씩 N번 DayAdvancedEvent 발행
}
```

**이유:**
- Task 2.2(GameTime) 가 Task 3.3(GameState) 보다 먼저 → GameState 의존 없이 동작해야 함
- EventBus 와 동일 패턴 (인프라성 static) — Don'ts 의 "Singleton 남용 자제" 예외
- 테스트 격리는 `Reset()` 으로 해결 (`EventBus.Clear()` 와 짝)

**동기화 시점 (Task 3.3 이후 적용):**
- GameInitializer 시작 시: `GameTime.Reset(state.currentDate)`
- 세이브 직전: `state.currentDate = GameTime.CurrentDate`
- 로드 직후: `GameTime.Reset(state.currentDate)`

**대안 검토:** 파라미터 방식 `Advance(GameState, int)` 도 가능하나, 호출자가 항상 GameState 참조를 들고 다녀야 해 번잡 (EventScheduler, DailyProcessor 등 다수). 자체 상태 + 동기화가 V0.1 스코프엔 더 단순.

**`Advance(N)` 발행 횟수:** N번 (하루씩) 발행. FM 식 Continue 메커니즘은 한 계층 위(`GameManager.AdvanceDay()`) 에서 "정지 이벤트까지 루프" 로 구현 — 매일 EventScheduler 가 동작해야 하므로 합산 1회 발행은 부적합.

---

## 24. CA-Stats 분리 운영 (V0.1 한정)

**결정:** V0.1 에서 `Player.currentAbility` (CA) 와 `Player.stats` 는 독립 데이터. CA 는 generation 진실값, stats 는 분배 결과지만 정확히 가중합 ≠ CA.

**이유:**
- V0.1 매치 시뮬레이션은 팀 CA 합 기반 → 개별 stats 무관
- CA = derived(stats) 강제하면 generation 로직 복잡도 ↑ (가중합 일치까지 후처리 필요)
- stats 는 디스플레이/캐릭터성 (포지션별 강조 표현) 용도로 자유롭게 분포

**Sanity check:** algorithms.md #1 T5 에서 CA 와 stats 가중합의 상관계수 > 0.6 검증 — 완전 분리는 아니지만 한쪽이 폭주하지 않게.

**V1.0 변경 트리거:** 매치 시뮬레이션이 개별 stats 를 사용하기 시작하면 (algorithms.md #2 V1.0 확장 시점) `Player.DeriveCAFromStats(pos)` 도입 검토.

---

## 25. 트레잇 충돌 그룹 시스템

**결정:** `TraitSO` 에 `exclusionGroupId` 필드 추가. 같은 그룹 트레잇은 한 선수에 동시 부여 불가. 트레잇 개수 자체는 제한 없음 (확률로 자연 감소).

```csharp
public class TraitSO : ScriptableObject {
    public int id;
    public string displayName;
    public string description;
    public float weight = 1.0f;
    public int exclusionGroupId = 0;     // 0 = 충돌 없음 (기본)
}
```

**이유:**
- "늦깎이형 + 조숙형" 같은 의미적 모순 방지
- 무관한 트레잇 다중 보유는 캐릭터성으로 살림 (e.g., "빅매치형 + 만능형")
- 추가/삭제 트레잇 시 데이터로만 관리 (코드 분기 없음)

**현재 정의된 그룹:**
- Group 1 (DevelopmentSpeed): 늦깎이형, 조숙형

---

## 26. 2차 포지션 Affinity 시스템

**결정:** `PositionSO` 에 `affinities: List<PositionAffinity>` + `fallbackAffinityWeight` 필드 추가. 명시적 어피니티가 있는 포지션은 높은 확률, 없는 포지션도 작은 확률(2~3%)로 뚫림. GK 는 affinity 비워두고 2차 포지션 생성에서 명시적 제외 — "필드/GK 이중 포지션" 비현실성 회피.

```csharp
public class PositionSO : ScriptableObject {
    // 기존 필드 ...
    public List<PositionAffinity> affinities;
    public float fallbackAffinityWeight = 0.05f;
}

[Serializable]
public class PositionAffinity {
    public Position position;
    public float weight;     // 1.0 ~ 10.0 권장
}
```

**이유:**
- 자연스러운 친밀도 (ST → 윙) + 별난 캐릭터 (ST → CB) 의 다양성 둘 다 살림
- V1.x 적응도 시스템의 데이터 구조 기반
- GK 만 별도 분기 — 데이터로 처리하기엔 너무 강한 제약이라 알고리즘에서 명시 제외

---

## 27. Club 명성 분포 — 4티어 계단 + 약상관 재정/시설

**결정:** 20구단 명성을 4티어 계단으로 분배. 카운트는 **ratio** 로 외부화해 가변 `clubCount` 에 대응. 각 티어 내에서 균등 추첨. 재정과 시설은 명성과 강한 양상관이지만 노이즈로 한두 단계 출렁.

```
tier  | ratio | clubCount=20 → count | repRange
------|-------|---------------------|---------
Top4  | 0.20  |          4          | 85..95     # 빅클럽
Euro  | 0.30  |          6          | 65..80     # 유럽권 다툼
Mid   | 0.35  |          7          | 45..60     # 중위권
Rel   | 0.15  |          3          | 25..40     # 강등권
                  Σratio = 1.00
```

**이유:**
- EPL 실제 분포 모티브 (빅4 + 유럽권 + 중위권 + 강등권).
- 티어 경계가 의사결정의 재미 — "강등권 → 중위권" / "중위권 → 유럽권" 같은 단기 목표가 명확.
- 선형 분포는 단조롭고, 멱함수는 중하위권 해상도 떨어짐.
- **ratio 화 이유**: clubCount 가 20 외 (10/12/24 등) 가변 입력으로 들어와도 `AllocateTierCounts` 라운드 보정으로 항상 합 일치. 새 리그 추가 비용 최소화.

**약상관 정책:** 시설은 `rep/20` 매핑 + `NextNormal(σ=1)` 노이즈로 한두 단계 출렁. 자금은 `base + repCoeff×rep + 15% σ`. → 빅클럽인데 시설 평범 / 중위권인데 유스 강한 등 구단 캐릭터성 살아남 (스토리텔링 재미).

**외부화:** `GameBalanceSO.tierClubRatios/tierRepMin/tierRepMax`, `facilityNoiseSigma`, `financeNoiseSigma` (`algorithms.md #5` 참조).

### V1.0+ 보완 포인트

- **리그별 다른 ratio 표** — 현재는 모든 리그가 동일한 `tierClubRatios` 사용. V1.0 에서 LeagueConfigSO 로 이전해 ESP=빅2 강세, GER=빅3 + 평준화 등 리그 색깔 반영.
- **다중 리그 동시 운영** — 현재 ClubGenerator 는 단일 리그 호출 (caller loop 로 다중 리그도 가능하나 명성 통합 ranking 없음). V1.0 에서 이적 시장 연동을 위한 글로벌 명성 ranking 도입.
- **시즌 목표 동적화** — 현재 `targetLeaguePosition = i+1` (명성 순위 = 목표). V1.0 에서 보드 신뢰도·예산 조합 기반 동적 목표.

---

## 28. 초기 스쿼드 — `FormationConfig` + 필수 + 랜덤 (V0.1)

**결정:** V0.1 에서 모든 구단이 같은 분배표로 25명 스쿼드 생성하되, **포메이션(4-4-2 기준) 필수 인원 + 랜덤 자리** 정책으로 변경. 정적 13개 int 분배표 → `FormationConfig` 단위로 묶음.

```
필수 23명 (4-4-2 기준):
  GK 3
  CB ≥ 4
  LB ≥ 2
  RB ≥ 2
  DM + CM (그룹 합) ≥ 4
  LM + LW (그룹 합) ≥ 2
  RM + RW (그룹 합) ≥ 2
  ST + CF (그룹 합) ≥ 4

랜덤 2자리:
  위 카테고리 중 시드 기반으로 +1
```

**V0.1 기본값 (23명):** GK 3 / CB 4 / LB 2 / RB 2 / DM 2 / CM 2 / LM 1 / LW 1 / RM 1 / RW 1 / ST 2 / CF 2. AM / WB 는 V0.1 분배표 제외 (4-4-2 정통 포메이션 X).

**이유:**
- **더블 스쿼드 보장** — 모든 사용 포지션 최소 2명. 부상 / 컨디션 회복 중에도 라인업 가능. GK 는 서드키퍼까지 3명.
- **포메이션 단위 응집** — V0.1 4-4-2 단일 디폴트지만 `FormationConfig` 데이터 단위로 묶어 V1.0 진입 시 매끄러움 (`#32` 참조).
- **시드 기반 랜덤 2자리** — 구단 / 시드별 미세한 다양성. 결정성 보장.

**외부화:** `GameBalanceSO.formation: FormationConfig` (nested class — `[Serializable]` value object). V0.1 단일 인스턴스. V1.0 에서 `FormationSO` 로 추출.

```csharp
[Serializable]
public class FormationConfig {
    public string name = "4-4-2";
    public int gk           = 3;
    public int cbMin        = 4;
    public int lbMin        = 2;
    public int rbMin        = 2;
    public int dmCmGroupMin = 4;   // DM + CM 합
    public int lmLwGroupMin = 2;   // LM + LW 합
    public int rmRwGroupMin = 2;   // RM + RW 합
    public int stCfGroupMin = 4;   // ST + CF 합
    public int randomSlots  = 2;
}
```

**가변 `playersPerClub` 대응:** `Σ(필수) + randomSlots ≠ playersPerClub` 일 경우 V0.1 은 **분배표 합 기준으로 진행 + 경고**. V1.0 에서 ratio 화 검토.

### V1.0+ 보완 포인트

- **`FormationSO` 추출** — nested `FormationConfig` 를 별도 SO 로 분리. `List<FormationSO> availableFormations` 카탈로그 도입.
- **가챠 시 포메이션 랜덤화** (`#32`) — 초기 스쿼드 가챠 단계에 포메이션도 랜덤 추첨. 각 포메이션이 굴러갈 수 있는 최소 구성 비율 보장 (현재 4-4-2 한정 → 4-3-3 / 3-5-2 등 추가).
- **전술 프리셋 연결** — `FormationSO` 가 단순 분배표 외 전술 매개변수 (압박 강도 / 라인 높이 등) 도 포함. 경기 시뮬레이션 입력.
- **구단별 색깔** — 명성·예산·유스 시설에 따라 스쿼드 편향 (빅클럽=veteran/외국인 ↑, 강등권=youth/자국인 ↑).
- **homegrown 시설 연동** — 현재 모든 구단 20% 고정. 유스 시설 Lv5 → 35%, Lv1 → 10% 등 시설 연동.

---

## 29. GameManager 는 Core Layer (Application 아님)

**결정:** `GameManager` 는 `FMLite.Core` 에 위치한다. Application 이 아니다.

**이유:**

1. **진입점/컨테이너 성격**. AdvanceDay / SaveGame / LoadGame 같은 호출은 모두 Application 시스템(`MatchSimulator`, `DailyProcessor`, `SaveSystem` 등)에 위임한다. GameManager 자체는 도메인 로직을 수행하지 않는다.
2. **State 보유는 "포인터 보유"** 이지 상태 머신이 아니다. `design-decisions.md #3` 의 Stateless 원칙은 Application 시스템(`MatchSimulator` 등)에 적용되는 것이고, GameManager 의 `State` 는 단지 GameState 참조를 들고 있을 뿐. 변경은 시스템이 한다.
3. **다른 인프라 (`GameTime`, `EventBus`, `GameLog`) 와 같은 레이어** — Unity MonoBehaviour 라이프사이클 통합 + 싱글톤 패턴 + 정적 진입점 성격이 동일.

**의존 정리 (이 결정의 결과로 의존 방향 정통화):**

- `Domain` 가장 안쪽 (외부 의존 0) — `Domain.asmdef` references 비워짐.
- `Core → Domain` (GameManager 가 GameState/Club 알아야 하므로). Clean Arch 의 의존 역전 패턴.
- `Application → Core + Domain` (기존 그대로).
- 순환 의존 없음. Domain 이 Core 를 알지 않으므로 안전.

**참고:** 이전엔 세 문서가 충돌 상태였음 (`project-context.md` / `class-diagram.md` 는 Application, `coding-conventions.md` 는 Core 로 표기). 이 결정과 함께 세 문서 모두 Core 로 통일.

**GameManager 책임 경계 (Task 8.3 작업 시 보완 — 2026-01-12):**

- **State 보유 + 진입점 (싱글톤) 만**. 흐름 조율은 Application 시스템에 위임.
- `AdvanceDay` 같은 시간 진행 / 시스템 호출 조율 = `GameLoop` (Application). GameManager 가 직접 호출하지 않음.
- 이유: Core → Application 참조는 **순환 의존** (Application → Core 이미 존재). 호출 책임을 Application 으로 옮겨 의존 그래프 그대로 유지.
- 호출 흐름:
  ```
  UI Continue 버튼
    → GameLoop.ContinueUntilStop(state, balance)   ← Application
        → GameTime.Advance(1)                       ← Core 정적
        → DailyProcessor.Run(state, balance)        ← Application
        → EventScheduler.Run(state)                 ← Application
  ```

**V1.0+ 보완 포인트:**

- 의존 역전 강화: GameManager 가 정말로 시스템을 직접 알아야 하는 시나리오 생기면 인터페이스 도입 (`IDailyProcessor` 등) 또는 EventBus 만으로 처리.

---

## 30. Starting Squad 평가 — 4라인 + 명성 대비 + ACE

**결정:** 초기 스쿼드를 **4라인 × 5단계 티어** 로 평가. 명성 대비 정규화로 빅클럽 / 약체 모두 만족스러운 평가. ACE 마커 단일.

**4라인 분류:**

```
GK : GK
DF : CB, LB, RB, WB
MF : DM, CM, AM, LM, RM
AT : LW, RW, ST, CF
```

V0.1 알고리즘 내 하드코딩. V1.0 에서 `PositionSO.lineCategory: Line` 필드 추가 검토 (외부화).

**평가 알고리즘 (algorithms.md #6 3단계):**

1. **라인 평균 CA**: `lineCA = avg(p.currentAbility for p in line)`
2. **명성 대비 정규화**: `expectedMeanCA = caRepBase + caRepCoeff × club.reputation` (algorithms.md #1 1단계와 동일 공식)
3. **티어 컷** (`GameBalanceSO` 외부화):
   - `ratio ≥ 1.20` → Elite
   - `≥ 1.05` → Strong
   - `≥ 0.90` → Average
   - `≥ 0.75` → Weak
   - 그 외 → Poor

**ACE 마커:** 모든 선수 중 최고 CA 선수의 **라인 1개** 마커. 단일.

**이유:**
- **CA 평균** — 단순 선수 수가 많다고 강한 게 아니라 평균 능력치. "AT 라인에 8명인데 모두 약함" 이 "AT 5명인데 모두 강함" 보다 약하다.
- **명성 대비 비율** — `#15` "빅클럽 평범 ≈ 중위권 훌륭" 의 수치적 구현. 작은 구단 유저 만족감 보장.
- **ACE 마커** — 4라인 평가만으론 단조. "에이스 어디 있나" 가 의사결정 포인트.

**예시 검증:**
- 빅클럽 (rep=90, expectedMean=132) + 라인 평균 140 → ratio 1.06 → Strong
- 중위권 (rep=50, expectedMean=100) + 라인 평균 110 → ratio 1.10 → Strong
- 같은 절대 CA (120) 도 명성 따라 다른 평가. 빅클럽이면 Average, 중위권이면 Elite.

### V1.0+ 보완 포인트

- **z-score 정규화** — 단순 비율 대신 `(lineCA - expectedMean) / caStdDev`. 더 통계적으로 정확.
- **출전 시간 카테고리** (`#33` 참조) — 능력치 기반 자동 배정 + 노이즈. "주전급/서브급/비상후보급". 사기 시스템과 연동.
- **라인별 ACE** — 단일 ACE → 라인별 최고 CA 4명 마커.
- **위치 외부화** — `PositionSO.lineCategory` 필드.

---

## 31. Reroll — 재생성 + 새 ID (V0.1)

**결정:** 스타팅 가챠 Reroll 시 해당 구단 25명 전체 **GameState 에서 제거** + 새 시드 / 새 id 로 25명 재생성. 기존 id 재사용 X.

```
1. state.rerollTokens -= 1
2. foreach playerId in club.seniorSquadIds: state.RemovePlayer(playerId)
3. club.seniorSquadIds.Clear()
4. nextId = state.nextPlayerId  ← 신규 필드 (모든 player id 카운터 단일 진실의 원천)
5. ClubGen 의 스쿼드 생성 단계 호출. id = nextId++
6. state.nextPlayerId = nextId
```

**이유:**
- **결정성 + 디버그 명확성** — 같은 id 가 다른 선수 데이터를 가지면 세이브 / 디버그 혼란.
- **세이브 일관성** — 세이브 / 로드 시 player 객체 동일성 보장.
- **id 단조증가** — 새 id 만 부여하면 `nextPlayerId` 카운터 하나만 관리하면 됨.

**구현 영향:**
- **`GameState.nextPlayerId: int` 필드 신규 도입** — 모든 player id 발급의 단일 진실의 원천. `ClubGenerator` 도 이걸 사용 (기존엔 `startPlayerId` 파라미터 호출자 관리).
- `GameInitializer` 가 ClubGen 호출 후 `state.nextPlayerId` 갱신.
- `StartingSquadGacha.RerollSquad` 도 이걸 사용.

**대안 검토 — id 재사용:** 디스크 / 메모리 절약은 미미. 디버그 / 결정성 보장 손실이 큼. 거절.

### V1.0+ 보완 포인트

- **rerollsUsed 추적** — 시즌 첫 가챠 외 다른 Reroll 시스템 (유스 인스펙션 등) 과 별도 카운터 분리.
- **트랜잭션 정책** — Reroll 중 ClubGen 실패 시 롤백. V0.1 은 Assert 후 부분 실패.
- **derived seed 정책** — 현재 호출자가 `rng = new Random(state.randomSeed ^ club.id ^ rerollIdx)` 수동 부여. V1.0 에서 헬퍼 추출.

---

## 32. V0.1 단일 포메이션 + V1.0 가챠 랜덤화 확장

**결정:** V0.1 에서 4-4-2 단일 포메이션만 지원. `FormationConfig` 데이터 단위로 묶어 V1.0 진입 시 매끄럽게 확장.

**V0.1:**
- `GameBalanceSO.formation: FormationConfig` 단일 인스턴스 (4-4-2).
- ClubGen 가 이걸 사용해 분배표 생성.
- 가챠는 평가 / 리롤만. 포메이션 선택 없음.

**V1.0+ 확장 시나리오 (사용자 의도):**
- `FormationConfig` → `FormationSO` 로 추출.
- `List<FormationSO> availableFormations` 카탈로그 (4-4-2 / 4-3-3 / 3-5-2 / 4-2-3-1 / 4-4-1-1 등 5~6개).
- **가챠 시 포메이션 랜덤 추첨** — 각 구단마다 다른 포메이션. 분배표도 그에 맞춰 다름.
- 각 포메이션이 굴러갈 수 있는 **최소 구성 비율 보장** — `FormationConfig` 의 필수 인원 + 그룹 정책이 이미 그 형태로 설계됨.

**이유:**
- V0.1 단순화 (단일 포메이션) 와 V1.0 확장성 동시 달성.
- nested class → SO 추출은 직관적 마이그레이션 경로.

### V1.0+ 보완 포인트

- **FormationSO 신규** — id / name / 분배표 정책 / 전술 매개변수 (압박, 라인 높이 등) 보유.
- **가챠 추첨 메커닉** — 명성 가중치 (빅클럽 = 화려한 포메이션 ↑) 도 검토.
- **유저 변경 가능성** — 초기 가챠 후 시즌 중 포메이션 변경 가능 여부 (전술 화면 UX).

---

## 33. V0.1 Match Simulation 정책 — 단순 CA 합 + Poisson

**결정:** V0.1 매치 시뮬레이션은 **결과 우선** 모델 (`#17` 정신 계승). 양 팀 starting11 의 CA 합 → Poisson 분포로 골수 결정 → 라인 가중 + CA 비례 추첨으로 득점자 결정.

```
1. rng = new Random(match.id ^ state.randomSeed)        # 시드 고정 (#17)
2. starting11 = top-11 by CA (부상자 제외)              # V0.1 자동 선정 (라인업 시스템 V1.0+)
3. teamStrength = SUM(starting11.CA)
4. λ_home = totalLambda * (homeStrength / total) + homeAdvantageGoalBonus
   λ_away = totalLambda * (awayStrength / total)
   homeScore = Poisson(λ_home), awayScore = Poisson(λ_away)
5. 골 마다 weight = balance.scoringWeightByLine[line] * (p.CA / 100) 로 득점자 추첨
```

**이유:**

- **Poisson 분포**: 실제 축구 골 분포의 학계 표준 (Dixon-Coles 1997 등). 같은 λ 라도 매 매치 다른 결과 — 강팀이 무득점, 약팀이 이변 가능. 결정성과 자연 분포 동시 충족.
- **단순 CA 합 (`#24` 일관)**: V0.1 매치는 CA 만 사용, 개별 stats 무관. 라인별 가중치 / 포지션 적합도 / 폼·사기·피로 보정 모두 V1.0+. 매치 시뮬레이션 복잡도 ↓.
- **starting11 = top-11 by CA**: V0.1 라인업 결정 UI / 자동 라인업 알고리즘 없음 (Task 13 까지). 시뮬레이터가 자동 선정. 포지션 무시 — 명세 단순화.
- **득점자 = 라인 가중치 × CA**: 공격수가 ~60% 득점 (현실 분포). CA 보정으로 에이스 효과. `algorithms.md` #6 의 4라인 분류 재사용 → 일관성.
- **홈 어드밴티지 = home λ 가산**: 단순 + 의도 직관적 ("홈팀 이점"). EPL 통계 근사 (홈 46% / 무 26% / 원정 28%).
- **결과 우선 모델 (#17 정신)**: 시드 고정 → 결과 미리 산출 → 표시 이벤트는 결과에 부합. V0.1 에선 스코어 + 득점자만 미리 결정. 표시할 텍스트 이벤트는 V1.0+.

**외부화:** `GameBalanceSO.avgGoalsPerMatch (2.7)` / `homeAdvantageGoalBonus (0.3)` / `scoringWeightByLine ({0, 0.4, 1.5, 5.0})`. 모두 플레이테스트로 조정.

**전제 조건:**

- `match.id` 가 ScheduleGenerator 산출 시 unique. → 검증됨 (Task 7.2 T1~T9).
- `state.randomSeed` 가 GameInitializer 가 고정. → 검증됨 (Task 7.1).
- `Utils/RngExtensions.NextPoisson` 헬퍼 필요 (Sub-PR B 에서 추가, PlayerGen 의 `NextNormal` 패턴).

### `strengthExponent` 임시 변통 (V0.1 한정)

**보강 결정 (구현 검증 후, 2026-05-19):** 단순 선형 ratio (`s_h / (s_h + s_w)`) 는 CA 1.89배 차이를 골 1.43배 차이로만 반영 → 강팀 원정 51% / 홈 64% 라 디자인 의도 (압도적 강팀이 자주 이김) 부족.

**해결:** `strengthRatio = pow(s_h, k) / (pow(s_h, k) + pow(s_w, k))` 비선형화. `k=1.5` 기본값 시 강팀 홈 ~72% / 원정 ~59% — EPL 1위 팀 시즌 승률 (73~79%) 근사.

**이유:**

- **CA 차이를 골수 차이로 증폭** — 단순 CA 합 모델의 한계 보강.
- **대칭성 보존** — 양 팀 동일 변환, 약팀 차별 X.
- **k=1 폴백** — 외부화로 선형 ↔ 비선형 토글 가능. 플레이테스트 조정 여지.
- **동급 팀에서 k 무관** — `s_h == s_w` 면 k 어떤 값이든 ratio = 0.5. 무승부 / 홈 어드밴티지 검증 (T4/T5/T6) 영향 없음.

**V0.1 한정 명시:** 이 변통은 V0.1 단순 CA 합 모델의 결정력 부족을 임시 보강하는 것. V1.0+ 매치 엔진 재작성 시 (`#34` 이벤트 시퀀스) 폐기 예정 — finishing / composure / decisions 등 개별 stats 가 슈팅 변환률을 직접 결정하므로 비선형 보정 불필요.

### V1.0+ 보완 포인트

- **개별 stats 사용** — `#24` V1.0 트리거. 매치가 finishing / passing / tackling 등 직접 참조 시 stats 합과 CA 가 자연스럽게 일치하도록 derived CA 모델 검토.
- **`strengthExponent` 폐기** — 위 V0.1 임시 변통. 매치 엔진 재작성 시 k=1 회귀 또는 알고리즘 자체 제거.
- **라인업 결정 시스템** — 자동 라인업 (포지션 필수 + top-by-CA) → 유저 수동 라인업 UI. `Simulate(match, state, homeXI, awayXI)` 오버로드 도입 시점.
- **컵 연장전 + 승부차기** — `Match.type == FACup/CarabaoCup` 분기. 동점 시 `extraTimeLambda` Poisson 한 번 더 → 그래도 동점이면 승부차기 (별도 5+ 라운드).
- **비활성 구단 경량 시뮬** — V0.1 에선 단일 `Simulate` 사용. 이벤트 시퀀스 시스템 도입 후 비활성 구단 전용 경량 경로 (`SimulateLite`) 분리 검토. `data-flows.md` #3 갱신과 짝.
- **외부 영향 반영** — strength 계산 시 폼·사기·피로 곱셈 보정 (`design-decisions.md` #30 출전 시간 / 사기 시스템과 연동).

---

## 34. V1.0+ Match Simulation 진화 경로 — 이벤트 시퀀스

**결정:** V0.1 의 "결과 우선" 모델은 V1.0+ 에서 **분 단위 이벤트 시뮬레이션** 으로 전환. 인터페이스 `MatchSimulator.Simulate(match, state) → MatchResult` 는 유지 — 호출자 (`GameLoop`, `BackgroundSimulator`, `MatchPostProcessor`) 영향 없음. 내부만 교체.

**V0.1 (결과 우선)** vs **V1.0+ (이벤트 시퀀스)**:

```
V0.1: rng 고정 → 양 팀 strength → λ → Poisson(home/away goals) → 득점자 추첨 → MatchResult
V1.0+: rng 고정 → 분 단위 step (1~90) →
         step 마다 이벤트 발생 (슈팅 시도, 카드, 부상, 교체 …) →
         누적 상태 (점수, 카드 수, 부상자, 11→10명 등) 가 다음 step 분기에 영향 →
         최종 누적 = MatchResult
```

**왜 진화가 필요한가:**

1. **앞 이벤트가 뒤 이벤트에 영향** — 옐로 2장 → 퇴장 → 10명 → strength ↓ → 골 확률 ↓ 같은 누적 효과를 결과 우선 모델로는 표현 불가.
2. **부상 → 교체** — 부상자 발생 시 벤치 strength 가 들어옴. 교체 타이밍이 결과에 영향.
3. **교체 / 외침 등 유저·AI 의사결정 반영** — V1.0+ 텍스트 이벤트 시스템 도입 후 유저 응답 (전반 종료 코칭 코멘트 등) 이 후반에 영향.
4. **카드 / 부상 시스템 자연 발생** — 분 단위 이벤트가 곧 카드/부상 발생 지점.

**왜 V0.1 에선 안 하는가:**

- 분 단위 시뮬레이션은 복잡도 ↑↑ (이벤트 종류 정의 / 분기 / 확률 곱 / 누적 상태 / AI 교체 로직).
- V0.1 스코프 (2~3주) 에선 결과 우선 모델로 충분 — 스코어 + 득점자만 표시.
- **사용자 의도**: "교체는 AI 가 자동" / "외침 등 V1.0+ 에 추가될 때 재변동 가능" — 인터페이스만 유지하면 V1.0 에서 내부 자유롭게 교체 가능.

**인터페이스 호환성 보장:**

- `MatchSimulator.Simulate(match, state) → MatchResult` 시그니처 동일 (`class-diagram.md` 합의).
- 시드 결정성 (`#17`) 정신 보존 — 매 step rng 상태 누적이지만 같은 시드 → 같은 시퀀스 → 같은 결과.
- `MatchResult` 스키마 호환 — V1.0+ 에선 `assists` / `rating` / `yellowCards` / `redCards` 가 0 이 아니게 채워지지만 필드 추가/제거는 없음.

**V0.1 코드의 운명:**

- 4단계 Poisson + 5단계 라인 가중 추첨 알고리즘은 V1.0+ 진입 시 **제거**. 대신 이벤트 시퀀스 엔진이 자체적으로 슈팅 시도 / 골 / 어시스트 / 카드 / 부상 등을 분 단위로 발생.
- 다만 V0.1 의 외부화 파라미터 (`avgGoalsPerMatch`, `homeAdvantageGoalBonus`, `scoringWeightByLine`) 일부는 V1.0+ 에서도 재활용 가능 (특히 라인 가중치).
- V0.1 EditMode 테스트 (T1~T7) 는 V1.0+ 진입 시 인터페이스 차원 테스트만 유지 (결정성 / 강팀 승률 / playerStats 정확성) — Poisson 분포 통계 테스트는 폐기 후 이벤트 시퀀스 테스트로 교체.

### V1.0+ 보완 포인트 (이벤트 시퀀스 도입 시)

- **이벤트 종류 정의** — Shot / Save / Goal / YellowCard / RedCard / Injury / Substitution / OffsideCalled / Foul …. 각 이벤트의 발생 확률 공식 / 결과 분기.
- **분 단위 vs 이벤트 단위** — 매 분 RNG 굴리기 (90 step) vs Poisson 으로 시간 간격 샘플링. 후자가 단순.
- **AI 교체 시스템 (`SubstitutionAI`)** — 피로 / 부상 / 전술 / 스코어 상황 기반 자동 교체. V0.1 starting11 자동 선정과 같은 자리.
- **유저 코칭 인터럽트** — 전반 종료 / 중요 이벤트 시 유저에게 외침·교체·전술 변경 옵션. UI 의존성.
- **퇴장 후 strength 보정** — 11명 → 10명 시 strength × 0.9 같은 보정 또는 자연 발생 이벤트 (10명은 슈팅 시도 횟수 자체가 줄어 자연 반영).
- **`MatchEvent` 도메인 필드 활용** — `class-diagram.md` 의 `Match.events: List<MatchEvent>` placeholder 가 본격 사용. 분 단위 이벤트 기록.

---

## 35. V0.1 Youth Pool Generation 정책

**결정:** 유스 인스펙션 풀 생성은 **PA 진실값 + CA derived 역방향** 모델 (PlayerGen 의 CA 진실값 모델과 대비). V0.1 시설 통합 등급 + 스타 픽 메커닉 + 강화된 시드 공식 (외부 마이닝 + 직플 영상 공유 둘 다 방어).

```
1. 시드 = state.randomSeed ^ currentDate.Ticks ^ userActionHash ^ club.id ^ intake.id ^ rerollsUsed
2. 풀 사이즈 = FacilityLevelSO(Youth).youthPoolSize (시드 자산)
3. 각 선수: PA 추첨 (스타 픽 5% + 일반 95%) → CA derived (PA 역방향 + σ=25 약화)
4. 나이/국적/포지션/트레잇/계약 = PlayerGen 부분 재활용
```

**이유 — 사용자 의도별 정리:**

1. **PA 진실값 / CA derived (사용자 #3)**: V0.1 유스는 PA 가 핵심 (잠재력). CA 는 어차피 어릴 때라 낮음. `youthPaGapStdDev=25` (PlayerGen σ=15 의 1.67배) 로 CA 분산 ↑ → 같은 PA 라도 CA 변동 커서 PA 추정 어려움. **CA 만 보고 PA 유추 불가** = 디자인 의도.

2. **스타 픽 메커닉 (사용자 #2)**: 5% 확률 PA 평균 +50 보너스. **시설 구려도 가끔 천재 발굴**. 시설 Lv1 (avgPA 100) 도 가끔 PA 150 슈퍼유망주. 시설 좋아도 평범한 풀 가능 — 게임 진행 재미 ↑.

3. **시드 공식 — 옵션 2+3 결합 (사용자 #6)**:
   - `currentDate.Ticks` 포함 → **외부 시드 마이닝 차단** (newgame seed 단독으로 미래 시점 예측 어려움, DateTime.Ticks 가 100-nanosecond 단위)
   - `userActionHash` (`finance.money ^ squad.Count ^ tokens` 등) → **직플 영상 공유 차단** (자금 1원 / 영입 1명 / 토큰 1개 차이로도 hash 변동)
   - **결정성 (`#17`) / 멀티세이브 일관성 보존** — 같은 행동 = 같은 hash = 같은 결과
   - **본질적 한계 인정**: 완벽히 동일 플레이는 같은 결과 (결정론의 본질, 막으면 세이브 깨짐)

4. **V0.1 시설 통합 등급 (사용자 #1)**: `FacilityLevelSO(Youth)` 가 시설 + 코치 + 모집 통합 책임. 실제 FM 메커닉 (시설 ≠ 코치 ≠ 모집) 은 V1.0+ 분리 트리거로 명세. V0.1 단순화.

5. **미영입 V0.1 단순화 (사용자 #9)**: 영입 결정 후 미영입 후보 모두 GameState 제거. `intake.rejectedPlayerIds` 에 ID 만 보관 (`#7` 영구 저장). V1.0+ AI 다른 구단 영입 시스템 트리거.

6. **나이 가중치 + birthDate 저장 (사용자 #4)**: 16=40%, 17=40%, 18=20%. `PersonalInfo.birthDate` 저장 (age 필드 별도 X) — PlayerGen 패턴 그대로. 미래 홈그로운 / 출전 가능 나이 / 적응 기간 등 계산 시 birthDate 필수.

7. **국적 자국 78% (사용자 #8)**: ClubGen 의 `primaryNationalityRatio=0.70` 보다 ↑. 유스는 자국 출신 비중이 더 큰 게 현실적 + 게임 만족감.

**외부화:** `GameBalanceSO` 신규 12개 필드 (`youthStarPickProbability=0.05`, `youthStarPaBonus=50`, `youthPaStdDev=15`, `youthPaGapStdDev=25`, `youthIntakeMinAge=16`, `youthIntakeMaxAge=18`, `youthIntakeAgeWeights={0.40, 0.40, 0.20}`, `youthPrimaryNationalityRatio=0.78`, `youthIntakeMainMonth/Day=6/15`, `youthIntakeSecondMonth/Day=1/15`). `algorithms.md #4` 참조.

### V1.0+ 보완 포인트

- **유스 시설 분리** — `FacilityLevelSO(Youth)` 통합 등급 → `Club.youthCoachLevel` (PA 평균) / `Club.youthRecruitmentLevel` (풀 크기) 분리. 시설 등급은 다른 효과 (스타플레이어 인지도 / 외국 유스 영입 가능 / 보드 신뢰도 +) 로 재정의.
- **포지션 가중치 변동** — V0.1 균등 → V1.0+ 라운드별 가중치 가챠 (어떤 인스펙션은 GK 0명, ST 다수 / 다른 인스펙션은 반대). `youthPositionWeightVolatility` 같은 외부화 도입.
- **AI 다른 구단 영입** — 미영입 후보 일정 확률 (`youthRejectedToOtherClubRatio`) 로 다른 구단 영입. 알림 이벤트 + 추후 조우 시 디스플레이.
- **CA-Stats 정합성 (algorithms.md #1 V1.0 트리거와 짝)** — PA → CA 단순 derived 대신 stats 가중합 기반 derived 검토. 같은 PA 라도 stats 분포에 따라 CA 다양화.
- **트레잇 가중치 차등** — 유스 시설 등급별 "고급 트레잇 (빅매치형 등)" 가중치 ↑. PlayerGen 트레잇 부여 알고리즘에 분기 추가.
- **시드 강화** — userActionHash 정교화 (`intakeHistory.Sum(...)` 과거 영입 패턴 / `state.activeOffers.Count` 등 추가).
- **추가 스카우트 (`data-flows.md #4 [3-c]`)** — 비용 차감 + UI 정보 정확도 ↑ (PA 추정치 범위 좁힘 / 트레잇 노출 정도).
- **계약 기간 차등** — V0.1 균등 2~4년 → 시설 / 나이 / PA 에 따라 차등 (천재는 짧게 — 빅클럽 위협, 잠재력 낮으면 길게 — 위험 헤지).
- **다른 클럽 인스펙션** — V0.1 유저 클럽만 → 시즌 사이클에 AI 클럽도 인스펙션 실행 + 영입 결정.

---

## 36. GameState.nextIntakeId 단조증가 카운터

**결정:** `GameState` 에 `int nextIntakeId` 신규 필드 (디폴트 1). 모든 YouthIntake 의 id 발급 단일 진실의 원천 (PlayerGen 의 `nextPlayerId` 패턴).

```csharp
public class GameState {
    // ... 기존 필드 ...
    public int nextPlayerId = 1;     // 기존 (#31)
    public int nextIntakeId = 1;     // 신규
}
```

**이유:**

- **결정성 + 디버그 명확성** (`#17`, `#31` 패턴 일관) — 같은 id 가 다른 intake 데이터를 가지면 세이브 / 디버그 혼란.
- **세이브 일관성** — 세이브 / 로드 시 intake 객체 동일성 보장.
- **단조증가** — 시즌별 메인/보조 인스펙션 누적. 시즌 1 메인 = id=1, 보조 = id=2, 시즌 2 메인 = id=3, ...
- **시드 공식 의존** (`#35` 1단계) — `intake.id` 가 시드에 들어가므로 ID 결정성이 풀 결정성으로 이어짐.

**구현 영향:**

- `YouthSystem.GenerateIntake` 가 `state.nextIntakeId++` 로 발급
- `GameInitializer` 가 `state.nextIntakeId = 1` 초기화 (이미 디폴트라 사실상 no-op)
- 세이브 / 로드 라운드트립 검증 필요 (PlayerGen 의 `nextPlayerId` 패턴 그대로)

### V1.0+ 보완 포인트

- **id 재사용 검토 X** — `#31` 과 동일 정책. 디스크 / 메모리 절약 미미. 디버그 / 결정성 손실 큼.
- **derived seed 헬퍼 추출** — 현재 `state.randomSeed ^ currentDate.Ticks ^ userActionHash ^ club.id ^ intake.id ^ rerollsUsed` 가 호출자 수동 조합. V1.0 에서 `IntakeSeed.Compute(state, club, intake)` 헬퍼 추출.

---

## 37. V0.1 Transfer Market 정책

**결정:** V0.1 이적 시스템 = **이적시장 (검색·오퍼·협상) 상시** + **이적시장 활성화 기간 (체결) 6/1~8/31 + 1/1~1/31** 분리 + 단일 라운드 AI 응답 + 사용자 클럽 능동 영입 + 슈퍼스타 압도적 가격.

```
[검색] TransferMarket.SearchPlayers — 시점 제약 X, V0.1 정확도 100%
[제출] SubmitOffer — 시점 제약 X (활성화 기간 외에도 미리 협상 가능)
[응답] ProcessOffers (매일, DailyProcessor) — AI 가 ratio >= 1.20 면 Accepted 아니면 Rejected
[대기] Accepted 오퍼는 활성화 기간 외엔 status 유지
[체결] 활성화 기간 진입 시 ProcessOffers 가 자동 CompleteTransfer
```

**이유 — 사용자 의도별 정리:**

1. **이적시장 vs 활성화 기간 분리 (사용자 #2)**: 실제 축구 메커닉 반영. **검색·오퍼·협상은 상시** — 시즌 중간에도 미리 다른 클럽 선수 영입 협상 가능. **체결만 활성화 기간** — 협상 합의돼도 시장 열린 후 발효.
   - 시나리오: 11/15 (시즌 중) 협상 합의 → Accepted 대기 → 1/1 (윈터 활성화) 시 자동 체결.

2. **Market Value 슈퍼스타 압도 (사용자 "비교도 안 되게")**: `caFactor = pow(CA / 100, 4)` 비선형 + `marketValueBase = 500k` + `marketValuePaCoeff = 50k`. CA 100 (평범 600k) vs CA 180 슈퍼스타 (9.5M) = **15.7배 차이**. 빅클럽 자금 (~9M) 도 한두 시즌 모아야 슈퍼스타 영입 가능.

3. **V0.1 단일 라운드 / 자동 통과**: AI 판매 구단 응답 (Accept/Reject) 만. 역제안 / 다중 라운드 / 선수 협상 V1.0+. V0.1 단순화 우선.

4. **AI 구단 영입 미구현 (V0.1)**: 다른 AI 구단은 능동 영입 행동 X. 사용자 클럽만 오퍼 제출. V1.0+ AI 영입 시스템 (CpuTransferAi).

5. **PA 노출 V0.1 정확도 100%**: 스카우트 시스템 V1.0+. V0.1 검색 결과 모든 선수 정확한 CA/PA 표시.

6. **시드 결정성 (`#17`)**: `rng = new Random(state.randomSeed ^ offer.id ^ currentDate.Ticks)` AI 응답 결정성. ±10% noise (aiValueNoiseSigma) 로 평가 부정확성 표현.

7. **DailyProcessor 통합**: `ProcessOffers` 가 매일 호출. Pending → AI 응답, Accepted → 활성화 기간 시 자동 체결.

8. **활성화 기간 외부화**: `transferWindowSummerStart/End` + `transferWindowWinterStart/End` (4쌍 month/day). V1.0+ LeagueConfigSO 로 이전.

9. **용어 정정**: ❌ "이적창" (모호) → ✅ **"이적시장 활성화 기간"** (한국어 docs / UI 라벨). 영어 변수명 `transferWindow*` / `IsTransferWindowOpen` 그대로 (도메인 표준).

**외부화:** `GameBalanceSO` 신규 ~13개 필드 (`marketValueBase`, `marketValueCaExponent`, `marketValuePaCoeff`, `marketValueAgeCurve[4]`, `marketValueContractCurve[4]`, `marketValuePositionFactor[4]`, `marketValueInjuryFactor`, `aiValueNoiseSigma`, `aiAcceptRatio`, transferWindow* 4쌍). `algorithms.md #3` 참조.

### V1.0+ 보완 포인트

`algorithms.md #3` V1.0 Migration Notes 30+ 항목 종합 — 가장 중요한 큰 트리거만 여기 정리:

- **선수/구단 reputation 도입** → Market Value 곱셈 보정. 빅네임 / 빅클럽 프리미엄.
- **AI 협상 시스템** — 역제안 (CounterOffer status) + 다중 라운드 + 선수 개인 협상.
- **AI 구단 영입 시스템 (CpuTransferAi)** — 약점 포지션 / 자금 여유 / 명성 기준 자동 의사결정.
- **스카우트 시스템** — `Club.facilities.scoutLevel` 기반 검색 정확도. PA 추정치 / 트레잇 노출 정도.
- **에이전트 / 보너스 / 임대 시스템** — Contract 확장.
- **계약 갱신 + FA (자유계약)** — 만료 6개월 전 갱신 협상 / 자유이적.
- **트랜스퍼 리스트 / 다른 클럽 인지 (Interest)** — 다중 오퍼 경쟁.

---

## 38. V0.1 시즌 사이클 정책

**결정:** V0.1 시즌 사이클 = **5/15 종료 + 6/1 회계연도 + 8/15 매치 개막** 3 시점 분리 + FA 전환 / 은퇴 처리 도입 + 시상·보드 평가·재정 결산·사기 정산·Match 압축 V1.0+ 미루기.

```
5/15 — SeasonEndProcessor (FA 전환 / 은퇴 / SeasonEndedEvent)
        ↓ 오프시즌 (5/16 ~ 5/31)
6/1  — NewSeasonProcessor (회계연도 / 토큰 +3 / fatigue·form 리셋 /
        새 일정 / Standings 초기화 / SeasonStartedEvent)
        ↓ 6/15 메인 인스펙션 (Stage 10 통합) / 이적시장 활성화 기간 (Stage 11)
8/15 — 매치 개막 (ScheduleGenerator 가 6/1 에 생성한 새 일정의 첫 매치)
```

**프리시즌 컨셉 (GameInitializer 신규 흐름 + 2026-05-20 보강)**:
- `GameInitializer.NewGame` 의 `seasonStart` 인자 = **프리시즌 시작일** (`state.currentDate` 초기값). 첫 매치일과 분리.
- 첫 매치일 = `seasonStart` 이후 가장 가까운 `newSeasonOpening` (예: seasonStart=7/1 → 첫 매치 8/15).
- 프리시즌 기간 (예: 7/1 ~ 8/14, 약 6주) 동안 사용자가 구단 선택 / 스쿼드·전술 점검 가능 — 실제 FM 표준 흐름.
- 동기: 첫 매치를 seasonStart 당일에 배치하면 `GameLoop.AdvanceDay` 가 시간 진행 후 매치 처리 → 첫 날 매치 영원히 미처리. 프리시즌 분리로 자연스럽게 해결.

**이유 — 사용자 합의 사항:**

1. **3 시점 변수명 분리 (혼동 회피)**:
   - `seasonEndMonth/Day = 5/15` — 시즌 종료 (마지막 매치 시점)
   - `fiscalYearStartMonth/Day = 6/1` — 회계연도 / 신규 시즌 행정 처리 시작
   - `newSeasonOpeningMonth/Day = 8/15` — 매치 개막 (ScheduleGenerator 가 새 시즌 첫 매치 배치)
   - 사용자 지적: 매치 개막 8/15 와 행정 처리 6/1 이 헷갈리기 쉬워 변수명 분리 + docs 명확화

2. **고정 날짜 V0.1 (사용자 명시)**:
   - 5/15 / 6/1 / 8/15 고정. 실제 EPL 은 매년 가변 (2025-26 = 8/15 / 5/24, 2026-27 = 8/22 / 5/30)
   - V0.1 단순화 — 매년 동일 일정
   - V1.0+ 트리거: **캘린더 / 요일 정보 도입 시** "5월 마지막 토요일" / "8월 셋째 토요일" 같은 dynamic 계산. `LeagueConfigSO.seasonEndRule` 같은 enum + DayOfWeek 처리.

3. **계약 만료 → FA 전환 V0.1 도입 (필수)**:
   - 만료 선수 `currentClubId = -1` + `club.seniorSquadIds` 제거
   - 한 시즌 완주 후 자유계약 시장 형성. V1.0+ 갱신 협상 추가.

4. **은퇴 처리 V0.1 단순 도입**:
   - `age >= balance.retirementMinAge (33)` + `rng.NextDouble() < balance.retirementProbabilityPerYear (0.15)`
   - 은퇴 시 `GameState.RemovePlayer` (단순). V1.0+ `Player.isRetired` 플래그 + 능력치 하락 곡선 + 사후 통계
   - GameBalanceSO 필드 이미 존재 (재활용)

5. **NewSeasonProcessor 책임 (V0.1)**:
   - `currentDate = 6/1` 동기화
   - `state.rerollTokens` += `seasonRerollTokenGrant (3)`, max `maxRerollStockpile (5)`
   - 모든 선수 `state.fatigue = 0` / `state.form = 50` 리셋 (시즌 중 누적 리셋)
   - 모든 League.standings 초기화 (entries 0 리셋)
   - `ScheduleGenerator.Generate(...)` 호출 → 새 시즌 매치 일정 (`newSeasonOpening*` 부터 ~ `seasonEnd*` 까지)
   - 클럽별 `season.targetLeaguePosition` 갱신 (명성 순위 기반 단순 — `#27` 유지)
   - `season.boardConfidence` 초기화 (50)

6. **시상 / 보드 평가 / 재정 결산 / 사기 정산 / Match 압축 V0.1 미구현**:
   - 모두 V1.0+ 별도 시스템과 짝 (사기 / 평점 / 재정 시스템 등)
   - V0.1 한 시즌 완주 기능에 비필수
   - `data-flows.md #6` 의 해당 단계 = V1.0+ 트리거로 명세 갱신

7. **EventScheduler 통합 — 2 신규 트리거**:
   - 5/15 → `SeasonEndProcessor.Run` + `SeasonEndedEvent` 발행 + 정지 신호
   - 6/1 → `NewSeasonProcessor.Run` + `SeasonStartedEvent` 발행 + 정지 신호

**외부화:** `GameBalanceSO` 신규 6 필드 (`seasonEndMonth/Day`, `fiscalYearStartMonth/Day`, `newSeasonOpeningMonth/Day`). 기존 4 필드 (`seasonRerollTokenGrant`, `maxRerollStockpile`, `retirementMinAge`, `retirementProbabilityPerYear`) 재활용.

### V1.0+ 보완 포인트

- **캘린더 / 요일 dynamic 계산** — `DayOfWeek` 활용 ("5월 마지막 토요일") + LeagueConfigSO 의 enum 기반 규칙. 매년 가변 일정 자연 발생.
- **시상 시스템** — `SeasonAward` 도메인 (MVP / 득점왕 / 영플레이어 / 베스트XI). `SeasonEndProcessor` 에 단계 추가.
- **보드 시즌 평가 / 경질** — `Club.season.boardConfidence` 변동 (목표 - 실제 순위 차이 × multiplier). 임계점 이하 시 경질 알림.
- **재정 결산** — 입장료 (홈 매치 수 × 명성 multiplier) + 중계권 (순위 기반) + 상금 (1~4위 차등). `Club.finance.money` 갱신.
- **사기 / 모랄 정산** (#30) — 우승팀 +, 강등팀 -, 약속 출전시간 미달자 등.
- **Match 데이터 압축** — `Match.events` / `playerStats` 디테일 제거. 우승 / 강등 / 시상 정보만 보존 (#8 패턴).
- **계약 갱신 협상** — 만료 6개월 전부터 갱신 협상 시작. V0.1 FA 전환과 짝.
- **은퇴 정교화** — 능력치 하락 곡선 + `Player.isRetired` 플래그 + 사후 통계 / 명예의 전당.
- **승강** (`data-flows.md #6` 명시) — V0.1 단일 리그라 미구현. V1.0+ 다중 리그 + 승강.
- **레전 (Regen)** — 은퇴 / 자유계약 선수 일부를 차세대 유스로 환생. `PlayerOrigin.Regen` enum 이미 존재.

---

## 39. Stats 스케일 + 카테고리 — FM 49 stats 1-100 + CA/PA 1-200 (V1.0)

**결정:** V1.0 Stats 49 필드 (FM26 표준 1:1 매핑) + 스케일 1-100 (FM 1-20 → 5배 세분화). CA / PA 는 FM 표준 1-200 그대로.

**카테고리 (49)**:
- **Technical 14**: Corners, Crossing, Dribbling, Finishing, First Touch, Free Kick Taking, Heading, Long Shots, Long Throws, Marking, Passing, Penalty Taking, Tackling, Technique
- **Mental 14**: Aggression, Anticipation, Bravery, Composure, Concentration, Decisions, Determination, Flair, Leadership, Off the Ball, Positioning, Teamwork, Vision, Work Rate
- **Physical 8**: Acceleration, Agility, Balance, Jumping Reach, Natural Fitness, Pace, Stamina, Strength
- **Goalkeeping 13**: Aerial Reach, Command of Area, Communication, Eccentricity, First Touch (GK), Handling, Kicking, One on Ones, Passing (GK), Punching Tendency, Reflexes, Rushing Out (Tendency), Throwing

**이유:**
- **FM 1:1 매핑 (Q1)**: 사용자 피드백 "실제 FM 스탯 가져와서 갱신" 직역. FM 유저 친숙. V0.1 42 필드 → 49 (보강 7).
- **1-100 (Q12)**: 사용자 명시 "0~20 → 1~100 으로". FM 1-20 보다 5배 세분화 → 매치 시뮬의 미세한 stat 차이 자연 노출. CA / PA 는 도메인 표준 보존 (CA = 4 카테고리 종합 / Stat = 개별 — 단위 차이가 가독성 ↑).
- **재밸런싱 필수**: V0.1 1-20 외부화 수치 (statMeanAtCAFloor 등) 전부 1-100 기준 재산정. `algorithms.md` #1 갱신.

**영향 범위:** `Stats.cs` 4 카테고리 신규 7 필드 / `PlayerGenerator` 3단계 / `GameBalanceSO` ~25 stat 필드 / 시드 자산 / UI 표시 / Save Migration X (V0.1 → V1.0 무효, #52).

**V0.1 → V1.0 매핑** (명세상 추적, Save Migration 미적용):
| V0.1 카테고리 | V0.1 필드 | V1.0 변경 |
| --- | --- | --- |
| Technical (12) | passing, shooting, tackling, dribbling, heading, crossing, firstTouch, finishing, longShots, freeKickAccuracy, penaltyTaking, corners | + marking, technique, longThrows / shooting 제거 (finishing 대체) / freeKickAccuracy → freeKickTaking 명칭 |
| Mental (12) | vision, anticipation, composure, concentration, decisions, determination, leadership, offTheBall, positioning, teamwork, workRate, aggression | + bravery, flair |
| Physical (8) | acceleration, agility, balance, jumping, naturalFitness, pace, stamina, strength | jumping → jumpingReach 명칭 |
| GK (10) | aerialReach, commandOfArea, communication, eccentricity, handling, kicking, oneOnOnes, reflexes, rushingOut, throwing | + firstTouchGk, passingGk, punchingTendency |

### V1.0+ 보완 포인트 (V1.x 검토)

- **stat 별 매치 영향 명세** — Shot 결과 분기에 `finishing × composure`, Save에 GK `reflexes × handling` 등 명시적 매핑. V1.0 매치 엔진 (#44) 작성 시 확정.
- **stat 카테고리 가중치 위치별 영향** — PositionSO 의 emphasis flags 가 stat 가중치로 진화. ST = finishing emphasis × 1.5, CB = tackling emphasis × 1.5 등.

---

## 40. Hidden Attributes + Absolute/Relative 분리 (V1.0)

**결정:** `Player` 에 신규 도메인 객체 `hiddenAttrs: HiddenAttributes` 도입 (1-100, 9 필드). 사용자 피드백 "trait 의리·부상빈도 수치형 0-20" 흡수. Trait 자체는 명시적 플레이스타일 마커로 재정의 (#41).

**HiddenAttributes 9 필드:**
- `loyalty` (충성도) — 재계약 시 주급 요구 ↓, 이적 요청 ↓
- `ambition` (야망) — 출전시간 부족 / 빅클럽 오퍼 시 이적 요청 ↑
- `professionalism` (프로페셔널) — 훈련 효율 / 사기 안정 (변동폭 ×0.7)
- `pressureHandling` (압박 내성) — 빅매치 평점 가산
- `temperament` (기질) — 카드 / 라커룸 분위기
- `controversy` (논란성) — 미디어 사고 확률 (V1.x 미디어 시스템 도입 시)
- `injuryProneness` (부상 빈도) — 부상 발생률 곱셈
- `consistency` (일관성) — 폼 변동폭
- `versatility` (다재다능) — 2차 포지션 적응 속도

**Absolute vs Relative 분리** (FM 표준):
- **Absolute** (10): Determination, Work Rate, Leadership, Flair, Bravery, Aggression, Concentration, Natural Fitness, Composure, Decisions. 훈련으로 거의 안 자람 (인성 기반).
- **Relative** (나머지 39): 훈련 / Mentoring 으로 성장 가능.
- 메타 위치: `Utils/StatMetadata.cs` 또는 `StatCategorySO`. 하드코딩 가능 (stat name 기준).

**이유:**
- **사용자 피드백 흡수**: "의리·부상빈도 같은 trait 수치형으로" → Hidden 으로 분리가 자연스러움 (FM 표준이 이 형태). Trait 은 "빅매치형" 같은 명시적 마커로 별개 카테고리 (#41).
- **노출 정책 (Q4)**: 스카우트 명단 ∈ 정확 / 명단 ∉ 비공개 — 정보 비대칭의 핵심 자산.
- **Absolute/Relative**: 훈련 / 성장 시스템 (#50 Mentoring 등) 의 변화율 분기에 필수. FM 도 동일 구분.

**영향 범위:** `Player.hiddenAttrs` 신규 / `PlayerGenerator` 단계 추가 (Hidden 추첨) / `MoraleSystem` 의 변동 보정에서 hidden 참조 / `TransferSystem` 협상에서 loyalty/ambition 참조 / Save Migration X (V0.1 → V1.0 무효).

### V1.0+ 보완 포인트 (V1.x)

- **Personality 도입** — FM 표준 (Driven / Model Citizen / Professional / Resolute 등 ~30종 마커). Hidden Attributes 조합으로 계산. UI 표시.
- **Media Handling Style** — FM 표준. Hidden + Personality 조합. V1.x 미디어 시스템과 짝.

---

## 41. Trait 효과 본격화 + 카테고리화 (V1.0)

**결정:** TraitSO 에 `effects: List<TraitEffect>` 필드 추가 — 효과 본격 도입. 4 카테고리로 재정의.

**Trait 카테고리:**
| 카테고리 | 예시 | 효과 종류 |
| --- | --- | --- |
| **플레이스타일** | 빅매치형 / 클러치 / 무리한 패스 / 와이드 플레이 / 자국인 우대 | `MatchModifier` — 매치 시뮬 분기 가중 |
| **부상/체력** | 유리몸 (강한 `injuryProneness` 가산) / 철인 | `InjuryRateModifier` |
| **성장곡선** | 조숙형 (V0.1 존재) / 늦깎이형 (V0.1 존재) / 슈퍼유망주 | `GrowthRateModifier` |
| **포지션 특화** | 멀티포지션 / 골 결정력 / 수비형 윙백 | `MatchModifier` (포지션 적응 / 매치 행동) |

**TraitEffect.cs** 신규:
```csharp
public class TraitEffect {
    public TraitEffectType type;        // MatchModifier / GrowthRateModifier / InjuryRateModifier / MoralePropensity / MarketValueModifier
    public string targetKey;            // "shotChance" / "stat:finishing" / "morale" 등
    public float multiplier;            // 곱셈 보정
    public float additive;              // 가산 보정 (선택)
    public Dictionary<string, string> conditions;  // 조건부 ("matchType=BigMatch" 등)
}
```

**충돌 그룹 확장**:
- Group 1 (DevelopmentSpeed) — 조숙형 / 늦깎이형 / 표준 (기존)
- Group 2 (Durability) — 유리몸 / 철인 (신규)
- Group 3 (PressureMentality) — 빅매치형 / 멘탈약자 (신규)

**V1.0 카탈로그 (~20 trait)**:
- V0.1 6개 (조숙형/늦깎이형/부상취약/멘탈강자/빅매치형/만능형) + V1.0 신규 ~14 (클러치 / 무리한패스 / 와이드플레이 / 자국인우대 / 유리몸 / 철인 / 멘탈약자 / 슈퍼유망주 / 멀티포지션 / 골결정력 / 수비형윙백 / 정신적리더 / 페널티스페셜리스트 / 프리킥마이스터).

**이유:**
- **효과 본격 도입**: V0.1 = 라벨만. V1.0 = 매치 엔진 (#44) 분기 + 성장 시스템 + 부상 시스템 본격 활용.
- **Hidden Attributes 와 분리**: Hidden = 수치형 인성. Trait = 명시적 플레이스타일 마커. 둘 다 같은 효과 (Morale / 매치) 에 입력되지만 Hidden 은 미세한 수치 곱셈, Trait 은 분기 / 큰 가중치.
- **데이터로 관리**: TraitSO 추가 / 삭제 = SO asset 변경. 코드 분기 없음.

**영향 범위:** `TraitSO` 필드 추가 / `TraitEffect` 클래스 신규 / 매치 엔진 (#44) trait 효과 통합 / 성장 시스템 / `algorithms.md` #1 Player Generation 의 트레잇 부여 단계.

### V1.0+ 보완 포인트 (V1.x)

- **트레잇 카탈로그 50+ 확장** — FM 트레잇 (Plays One-Twos / Likes to Beat Offside Trap 등) 본격 도입.
- **트레잇 변화 (Learn / Lose)** — Mentoring (#50) 으로 베테랑이 mentee 에게 트레잇 전수.
- **트레잇 노출 정확도** — 스카우트 시설 등급별 트레잇 노출 정도 (현 V1.0 = displayName 일부, V1.x = 효과 정확 / 일부 / 비공개).

---

## 42. Morale + Happiness 분리 (V1.0)

**결정:** V0.1 의 `PlayerState.morale` (변동 없음) → V1.0 `morale` (단기) + `happiness` (장기) 분리. 변동 트리거 본격 도입.

**구조:**
- `Morale` (0-100, 단기) — 매치 / 라커룸 / 코칭 코멘트 기반. 매주 회복 경향.
- `Happiness` (0-100, 장기 추세) — 약속 / 출전시간 / 구단 성적 / 재계약 기반. 변화 느림.
- `PlayerState.happiness: int` 신규 필드 (디폴트 50).

**변동 트리거 (전체 매트릭스는 `v1.0-plan.md` §3.4.2 참조):**
- Morale: 매치 결과 (±5 ~ ±15) / 코칭 코멘트 / 라커룸 분위기 / 면담
- Happiness: 약속 이행 / 출전시간 / 강등·우승 / 재계약 / 보드 약속 / 면담 (지속 영향)

**Happiness 임계점 → 행동 분기:**
- ≥ 80: 만족 (보너스 +)
- 60-79: 양호 (정상)
- 40-59: 불만 표시 (인터뷰 사고 V1.x)
- 20-39: 이적 요청 (`TransferRequestEvent` — Q9 자동 트리거 + 유저 승인 패턴)
- < 20: 반항 (훈련 거부 / 평점 -)

**Hidden Attributes 연동 (#40):**
- `loyalty` 높을수록 Happiness 하락폭 ↓
- `ambition` 높을수록 강등 / 출전시간 미달 시 하락폭 ↑
- `professionalism` 높을수록 변동폭 전체 ×0.7

**라커룸 분위기 (V1.0 단순):**
- `Club.season.dressingRoomMood: int` — 1군 Happiness 평균 + 캡틴 leadership 가산점.
- < 30 → 시즌 폼 전체 -5 보정.

**이유:**
- **사용자 피드백 2.6**: "강등 시 불만 / 약속된 출전시간 미달 / 충성도·의리로 누그러뜨림" 직역.
- **Morale vs Happiness 분리**: 단기 변동 (매치 직후 사기) vs 장기 추세 (시즌 만족도) 가 자연스럽게 다른 트리거.
- **Q7 핵심만 V1.0**: 핵심 트리거 + 임계점 분기 + Hidden 연동 + 라커룸 분위기. 멘토링 / 멘트 세분화 등 정교화는 V1.x.

**영향 범위:** `PlayerState` 신규 필드 / `MatchPostProcessor` 사기 갱신 단계 (V0.1 미구현 → V1.0 본격) / `DailyProcessor` Morale 회복 단계 / 신규 `MoraleSystem.cs` (Application) / `algorithms.md` #8 신규 작성.

### V1.0+ 보완 포인트 (V1.x)

- **인터뷰 사고** — Happiness 40-59 + Hidden controversy 높음 → 미디어 인터뷰에서 부정 발언 자동 생성. V1.x 미디어 시스템과 짝.
- **선수 면담 멘트 세분화** — V1.0 = 4-6 옵션. V1.x = ~20 옵션 + 효과 분기 정교화.
- **그룹 사기 (Cliques)** — 라커룸 파벌 / 같은 국적 / 같은 연령대 그룹 영향. V1.x.

---

## 43. Promise 시스템 + 면담 (V1.0)

**결정:** FM 표준 약속 시스템 4종 V1.0 도입. 면담 시스템 단순 도입 (4-6 옵션).

**Promise 4종:**
| 종류 | 설명 | 측정 |
| --- | --- | --- |
| **PlaytimeAgreement** | 출전시간 보장 — 5단계 (Star / Important / Squad / Backup / Hot Prospect) | 시즌 매치 출전 비율 |
| **TransferIn** | 영입 약속 — 시즌 시작 시 특정 포지션 영입 | 활성화 기간 종료 시 영입 완료 |
| **Renewal** | 새 계약 약속 — 다음 시즌 시작 시 재계약 | 시즌 시작 시 RenewContract |
| **TransferOut** | 이적 약속 — 다음 활성화 기간에 이적 허용 | 활성화 기간 종료 시 이적 완료 |

**데이터 구조:**
```csharp
public class Promise {
    public int id;
    public int playerId;
    public PromiseType type;
    public DateTime madeAt;
    public DateTime deadline;
    public PromiseStatus status;       // Active / Fulfilled / Broken
    public Dictionary<string, int> targets;
}
```
- `GameState.activePromises: List<Promise>` 신규.
- `state.nextPromiseId: int` 단조증가 카운터 (#31 패턴).

**처리:**
- `DailyProcessor.Run` 가 `PromiseSystem.CheckProgress(state)` 호출.
- deadline 도달 시 status 확정 (Fulfilled / Broken). Broken → Happiness -20 (#42 변동 트리거).
- 마감 임박 (deadline - 30일) 알림 이벤트.

**면담 시스템 (V1.0 단순):**
- 유저 → PlayerProfile → [면담] 버튼 → 4-6 사전 정의 멘트:
  - "출전시간 보장하겠다" (PlaytimeAgreement Promise 생성)
  - "다음 시즌 새 계약 협상하자" (Renewal Promise)
  - "현재 성과 칭찬" (즉시 Morale +5)
  - "더 노력해야 한다" (Morale -3, professionalism 높으면 -1)
- 효과는 hidden (loyalty / ambition / professionalism) 에 따라 다름.

**이유:**
- **사용자 피드백 2.6**: "약속된 출전시간에 비해 적게 출전하면 불만" 직역. Promise 의 PlaytimeAgreement 가 핵심.
- **FM 표준**: Promise 시스템이 FM 메인 시스템 중 하나. 게임플레이 핵심 의사결정 추가.
- **Q7 핵심만**: 4종 Promise + 면담 4-6 멘트로 단순화. V1.x 멘트 세분화 / 더 많은 Promise 종류.

**영향 범위:** `Promise.cs` 신규 / `PromiseSystem.cs` (Application) 신규 / `GameState.activePromises / nextPromiseId` / `DailyProcessor` 통합 / `event-bus-catalog.md` `PromiseCreatedEvent / PromiseFulfilledEvent / PromiseBrokenEvent` 신규 / UI `PromiseInboxScene` 또는 Dashboard 인박스.

### V1.0+ 보완 포인트 (V1.x)

- **Promise 종류 확장** — FM 표준 약속들 (구장 확장 / 컵 우승 / 유럽 진출 / 슈퍼스타 영입 등). 보드 약속과 묶음.
- **면담 멘트 ~20 + 사전 시뮬레이션** — 멘트 선택 전 효과 예상 표시.
- **Promise 진행도 표시** — Dashboard 에 진행률 % (출전시간 약속 = 현재 35%, 목표 50%).

---

## 44. 매치 엔진 V1.0 — 분 단위 이벤트 시퀀스 (#34 실현)

**결정:** `design-decisions.md` #34 V1.0+ 진화 경로 본격 실현. 인터페이스 (`Simulate(match, state) → MatchResult`) 유지 / 내부 전면 재작성.

**구조 (`v1.0-plan.md` §3.5):**
```
1. 시드 고정 (V0.1 동일)
2. starting11 결정 — Tactic (#45) 산출물 사용 (Role 호환 + 자동 라인업)
3. 분 단위 step (1~90, 컵은 ~120 + 승부차기 — Q2 V2.0+ 라 미적용):
   a. 이벤트 종류 추첨 (Shot / Pass / Tackle / Foul / Card / Injury / Sub)
   b. 이벤트 주체 선수 추첨 (포지션 + 스탯 + Role 가중치 + 폼 + 사기)
   c. 이벤트 결과 분기 (Shot → Goal/Save/Miss/Block)
   d. 누적 상태 갱신 + SubstitutionAI 판단
4. 최종 누적 = MatchResult
```

**핵심 결정:**
1. **이벤트 종류 정의** — Shot / Save / Foul / Card (Y/R) / Injury / Sub / Pass / Cross / OffsidesCalled / KeyPass / Goal / Assist. ~12 종.
2. **stat 직접 참조 (#39 V1.0)** — Shot 결과 = `finishing × composure / 100`, Save = `reflexes × handling / 100`, Foul = `aggression`, KeyPass = `vision × passing`, Cross = `crossing × technique`.
3. **외부 영향 (form / morale / fatigue)** — strength 계산 시 곱셈 보정. `effectiveCA = CA × (1+form-50/200) × (1+morale-50/200) × max(0.5, 1-fatigue/200)`.
4. **부상 / 카드 / 정지 시스템 자연 발생** — Foul → Yellow / Red, Yellow 2장 = Red, 옐로 5장 = 1경기 정지, Injury 자연 발생 + 교체 트리거.
5. **AI 자동 교체 (`SubstitutionAI`)** — fatigue 70+ / Injury / 스코어 상황 기반.
6. **텍스트 이벤트 (Q5)** — 유저 매치 한정 핵심 ~15-20 이벤트 (Shot/Goal/Card/Injury/Sub + KeyPass). 비활성 매치 = `Match.events` 비움.
7. **SimulateLite 경량 경로** — 비활성 구단 매치는 V0.1 단순 Poisson + 라인 가중. 텍스트 / 분 단위 step X.
8. **strengthExponent (k=1.5) 폐기** — `design-decisions.md` #33 V0.1 임시 변통. 개별 stats 가 결정력 직접 표현 → 비선형 보정 불필요.
9. **시드 결정성 (#17)** — 같은 시드 = 같은 시퀀스 = 같은 결과 유지.
10. **컵 연장전 / 승부차기** — Q2 V2.0+ 미도입. 인터페이스만 대비 (`match.type` 분기).

**이유:**
- **#34 진화 경로 실현**: 옐로 2장 → 퇴장 → 10명 / 부상 → 교체 같은 누적 효과 자연 표현.
- **사용자 피드백 2.9**: 텍스트 이벤트 유저 구단 한정 직역. SimulateLite 분리로 비활성 비용 ↓.
- **V0.1 → V1.0 사이즈 폭증**: 매치 엔진 재작성이 V1.0 단일 최대 작업. Stage I 9 Sub-task.

**영향 범위:** `MatchSimulator` 전면 재작성 / `MatchEvent` 도메인 본격 활용 (`textKey / textArgs`) / `InjuryTypeSO` 카탈로그 본격 채움 (~15 종) / `PlayerMatchStat` 필드 확장 (shots / passes / tackles 등) / 평점 시스템 / `algorithms.md` #2 V1.0 재작성 / UI `MatchTextScene` / `SubstitutionAI.cs` 신규.

### V1.0+ 보완 포인트 (V1.x)

- **Team Instructions 효과** (#45 V1.x) — Tempo / Passing / Pressing / Line / Width 가 이벤트 확률 가중 입력으로 추가.
- **유저 코칭 인터럽트** — 전반 종료 / 중요 이벤트 시 외침 / 교체 옵션. UI 의존.
- **xG / heatmap / 슈팅 위치** — 매치 통계 풍부화. V1.x.
- **컵 연장전 / 승부차기** — Q2 V2.0+ 도입 시점.
- **날씨 / 잔디 상태** — strength 보정 추가. V1.x+.

---

## 45. Tactic 시스템 V1.0 — 중간 스코프 (Q10)

**결정:** Formation + Mentality + 간단 Role (3-4/포지션) + Duty(A/S/D) + Set Pieces 담당자. Team Instructions 는 V1.x.

**Formation:**
- `FormationConfig` nested → `FormationSO` 추출 (`design-decisions.md` #32 실현).
- 카탈로그 5-6개: 4-4-2 / 4-3-3 / 3-5-2 / 4-2-3-1 / 4-4-1-1 / 5-3-2.
- `Club.tactic: Tactic` 신규 필드.

**Player Role + Duty:**
- 포지션별 3-4 Role 카탈로그 (총 ~40 Role) — 명세는 `v1.0-plan.md` §3.6.2.
- Duty: Attack / Support / Defend.
- `PlayerRoleSO` 신규 — id / name / 호환 포지션 / 디폴트 duty / 매치 이벤트 가중치 modifier.

**Mentality 7단계:**
- `enum Mentality { VeryDefensive, Defensive, Cautious, Balanced, Positive, Attacking, VeryAttacking }`.
- 매치 시뮬 곱셈 보정 — VeryDefensive = 슈팅 빈도 ×0.6 / VeryAttacking = ×1.5.
- 외부화: `GameBalanceSO.mentalityModifiers[7]` 또는 `MentalitySO`.

**Set Pieces 담당자:**
- `Tactic.setPieceTakers: List<int>` — 페널티 / 자유킥 / 코너 / 스로인.
- 미지정 시 자동 (`finishing` / `freeKickAccuracy` / `corners` 최상위).

**캡틴 / 부캡틴:**
- `Club.season.captainPlayerId / viceCaptainPlayerId` — 자동 (leadership + age + 계약 잔여) / 수동 변경.
- 효과: 라커룸 분위기 +5 / 매치 평점 가산점.

**가챠 시 포메이션 랜덤화 (`#32` 실현):**
- 초기 스쿼드 가챠 단계에 포메이션도 랜덤 추첨.
- 빅클럽 = 화려 포메이션 (4-3-3 / 4-2-3-1) ↑ / 약체 = 견고 (4-4-2 / 5-3-2) ↑.

**이유:**
- **Q10 중간 스코프**: 풀 FM (Role + Duty + Mentality + Team Instructions 모두) 은 매치 엔진 (#44) 작성 부담 ↑↑. Team Instructions 빼고 V1.0 → V1.x 정교화.
- **Role + Duty 핵심**: 같은 포지션 = 같은 행동이 단조. Poacher vs Target Forward 차이가 의사결정 재미.
- **Set Pieces 담당자**: 골 결정자 / 어시 통계에 영향. FM 표준.

**영향 범위:** `Tactic / TacticSlot / PlayerRoleSO / FormationSO / MentalitySO` 신규 / `Club.tactic` / 매치 엔진 (#44) Role 가중치 입력 / UI `LineupScene / TacticScene` 신규 / 가챠 (`StartingSquadGacha`) 포메이션 추첨 / `algorithms.md` #6 갱신.

### V1.0+ 보완 포인트 (V1.x)

- **Team Instructions** — Tempo / Passing / Pressing / Defensive Line / Width 5 옵션. 매치 엔진 가중치 입력.
- **다중 전술 슬롯** — 클럽별 3 전술 (기본 / 강팀 상대 / 약팀 상대) 슬롯. 매치 직전 자동 선택.
- **유저 수동 라인업 정교화** — 드래그앤드롭 UI.
- **Role 카탈로그 확장** — FM 표준 ~80 Role (현 V1.0 ~40).

---

## 46. 스카우트 시스템 V1.0 — 이분법 + 정성적 라벨 (Q4)

**결정:** 스카우트 명단 ∈ / ∉ 이분법. 명단 밖 선수도 **조회는 가능** — 정확 수치만 가림.

**ScoutReport 도메인:**
```csharp
public class ScoutReport {
    public int playerId;
    public int scoutLevel;             // 1-100 (시설 등급 + 시간 누적)
    public DateTime lastUpdated;
    public CaPaEstimate caEstimate;
    public CaPaEstimate paEstimate;
    public List<int> revealedTraitIds;
    public HiddenAttributesPartial revealedHidden;
}
```
- `Club.scoutingKnowledge: Dictionary<int, ScoutReport>` 신규.

**시설 등급 → 명단 크기 / 정확도:**
- 스카우트 시설 Lv1 → ~50명, ±30 CA 정확도
- Lv3 → ~400명, ±15
- Lv5 → ~3000명 (사실상 전체), ±5
- 외부화: `FacilityLevelSO(Scout).scoutPoolSize / scoutAccuracyRange`.

**명단 진입 기준:**
- 자기 구단 → 자동 (scoutLevel 100 고정)
- 자기 리그 → 시설 Lv2 이상 자동 추가
- 타 리그 → 시설 + 시간 (시즌 시작 후 N일 경과)
- 유저 수동 [스카우트 추가] — V1.x

**검색 결과 가시성 (Q4 이분법):**
- 명단 ∈: 정확 CA/PA + 정확 stats + 모든 트레잇 + Hidden Attributes 노출
- 명단 ∉: 이름·구단·포지션·나이·국적 노출. **CA/Stats 정성적 라벨** (매우높음 / 높음 / 중간 / 낮음 / 매우낮음). Hidden 완전 비공개. Trait `displayName` 일부.
- `TransferSearchFilter.requireScouted` 폐기 — 모든 선수 검색 가능.
- 디버그 모드 (`isDebugMode`) — 명단 무관 모두 정확 노출.

**이유:**
- **사용자 피드백 2.7**: "스카우팅 명단 + 다른 구단도 자체 명단 + 명단 밖도 조회 가능 + 정성적 표현" 직역.
- **정보 비대칭**: `design-decisions.md` #14 정신 (티어 표시) 의 스카우트 버전. 시설 투자 보상 명확.
- **AI 영입 연동 (#47)**: AI 구단도 자체 명단 → 명단 안 선수만 영입 (현실적).

**영향 범위:** `ScoutReport / CaPaEstimate / HiddenAttributesPartial` 신규 / `Club.scoutingKnowledge` / `TransferSystem.SearchPlayers` 가시성 분기 / 신규 `ScoutingSystem.cs` (명단 자동 추가 / 정확도 누적) / `FacilityLevelSO(Scout)` 필드 추가 / UI Transfer 검색 결과 표시 (자물쇠 아이콘 / 회색조).

### V1.0+ 보완 포인트 (V1.x)

- **개별 스카우트 인사 (Staff)** — `Staff.cs` 도메인 + 개별 스카우트 (국가 / 영역 전문성). 현 V1.0 = 시설 추상화.
- **스카우트 임무 (Assignment)** — 특정 국가 / 리그 / 포지션 스카우트 발주. V1.x.
- **유저 수동 스카우트 추가** — 검색 화면에서 명단에 직접 추가.
- **트레잇 노출 정확도** — 시설 등급별 트레잇 효과 정확 / 부정확.

---

## 47. CpuTransferAi V1.0 — 필요 기반 트리거 (Q3)

**결정:** 횟수 / 빈도 X. **각 구단의 영입 필요 상황 발생 시점에 오퍼**. 트리거 5종.

**필요 트리거 (매주 호출):**
1. **약점 포지션** — 4라인 평균 CA ratio < `aiWeaknessRatioThreshold (0.95)` (명성 대비) → 그 라인 영입. (최우선)
2. **부상자 발생** — 핵심 (CA 상위 ≥ 70%) 가 `aiCoreInjuryWeeksThreshold (4)` + 부상 → 같은 포지션 영입.
3. **계약 잔여 6개월** — 핵심 선수 FA 임박 → 대체 영입 (`amount = current player 시장가 × 1.0`).
4. **약속 미이행 위험** — 보드 약속 (시즌 시작 영입 약속) 임박 → 약속 포지션 영입.
5. **명성 대비 자금 여유** — 자금 > `clubReputation × aiSavingsThreshold` → 명성 합의 강화 영입.

**트리거 우선순위**: 약점 > 부상 > FA > 약속 > 자금 여유. 같은 클럽 같은 주에 여러 트리거면 1개만.

**의사결정:**
- 자기 명단 ∩ 그 포지션 ∩ 자금 안에 있는 선수 추첨.
- `SubmitOffer(amount = marketValue × random(1.20 ~ 1.40))` 호출.
- 시드: `state.randomSeed ^ club.id ^ currentDate.Ticks ^ trigger.type` (결정성 유지).
- 활성화 기간 외에도 트리거 — 미리 협상 (V0.1 #37 정신 일관).

**외부화** (`GameBalanceSO`):
- `aiWeaknessRatioThreshold = 0.95`
- `aiCoreInjuryWeeksThreshold = 4`
- `aiSavingsThreshold = 1000`
- `aiOfferAmountRandomMin = 1.20` / `aiOfferAmountRandomMax = 1.40`

**이유:**
- **Q3 결정**: 횟수 외부화는 단조. 필요 기반이 FM 표준 + 자연스러운 시장 움직임. 클럽 명성·자금·약점에 따른 자연 빈도 차이.
- **#46 스카우트 연동**: 명단 ∩ 영입 — 명단 작은 클럽은 영입 시도 자체 ↓.
- **결정성 보존 (#17)**: 시드 derived → 같은 시드 같은 시장 움직임.

**영향 범위:** `CpuTransferAi.cs` (Application) 신규 / `EventScheduler` 매주 호출 / `algorithms.md` #7 신규 작성 (Stage F).

### V1.0+ 보완 포인트 (V1.x)

- **AI 협상 응답 의지** — 역제안 / 다중 라운드 (#48 V1.0 도입과 짝).
- **AI 매각 의향** — 약점 포지션 외 잉여 선수 매각. transferListed 자동 등록.
- **AI 임대 활용** — 영입 가능한 자금 X 시 임대로 대안 (#48 임대 시스템과 짝).
- **AI 클럽별 성향** — 명성 / 자금 / 보드 야망 따라 보수적 / 공격적 영입 차이. 신규 도메인 (`Club.aiPersonality`).
- **구단별 비동기 영입 타이밍** — 현재 V1.0 = "매주 월요일 모든 AI 구단 동시 호출" → V1.x = 각 구단 독립 cooldown (`club.lastTransferAttemptDate` + `aiAttemptCooldownDays`). 클럽 성향·자금·약점 강도에 따라 다음 시도 시점 자체가 다름. `aiPersonality` 와 짝 (공격적 클럽 = 짧은 cooldown). 결정성 유지: 시드 = `randomSeed ^ club.id ^ lastAttemptDate.Ticks` (currentDate 대신 lastAttempt → 비동기에도 재현성). `DailyProcessor` 매일 호출하되 클럽별 cooldown 체크로 실제 처리 클럽 선별.
- **다중 오퍼 동시 지정** — 현재 V1.0 `DetectTrigger` = 우선순위 1개만 리턴 → 클럽당 주 1오퍼. **여름 윈도우 대규모 리빌딩 (신임 감독 / 강등 후 재건) 시나리오 X**. V1.x = `DetectTriggers` (복수) — 우선순위 정렬된 트리거 리스트 + 자금 안에서 가능한 만큼 동시 오퍼. 자금 분산 정책: `affordableMax = money × aiBudgetRatio` 를 트리거별 분배 (예: 균등 / 우선순위 가중 / 시장가 비례). 위 비동기 타이밍과 함께 도입 — 두 기능 결합 시 FM 식 자연스러운 시장 움직임 (동시 다발 협상 + 클럽별 페이스).

---

## 48. 협상 V1.0 — CounterOffer + 선수 협상 + 임대 (Q7 핵심)

**결정:** V0.1 단일 라운드 → V1.0 다중 라운드 + 선수 개인 협상 + 임대 시스템 + release clause 활성화.

**CounterOffer (역제안):**
- `OfferStatus.CounterOffer` 신규 enum 값.
- AI 응답 분기 (V0.1 2 → V1.0 4):
  - ratio ≥ 1.30 → Accepted
  - 1.10 ≤ ratio < 1.30 → CounterOffer (시장가 ×1.30 역제안)
  - 0.85 ≤ ratio < 1.10 → Rejected
  - < 0.85 → Rejected + 사기 가산점 (-3 morale 보너스)
- 유저 응답: 수락 / 거절 / 재역제안 (최대 3 라운드 — `maxNegotiationRounds`).

**선수 개인 협상 (Negotiating):**
- AI 판매 구단 Accepted → `OfferStatus.Negotiating` (V0.1 자동 통과 → V1.0 단계).
- 선수 측 평가:
  - 주급 ≥ 시장가 기반 추정 주급 × 1.10 → 수락
  - + `loyalty` (현 구단 충성도) 가산 (loyalty 80+ = 거의 거절)
  - + `ambition` (빅클럽 이적 욕구) 가산 (ambition 80+ = 거의 수락)
  - + 출전시간 약속 (Promise 자동 생성 옵션)
- 결과: Accepted (체결 단계로) / Rejected (협상 결렬).

**임대 시스템 (Loan):**
- `TransferOffer.cs` 신규 필드: `isLoan / loanFee / loanWageShare / loanEndDate`.
- `LoanOption.cs`: `mandatoryPurchaseAtEnd / purchaseClause / recallClause`.
- `Player.parentClubId: int` 신규 필드 — 임대 시 원 소속.
- 임대 종료 (loanEndDate) → 자동 원 구단 복귀 (DailyProcessor).

**Release Clause 활성화:**
- 오퍼 amount ≥ `player.contract.releaseClause` → 판매 구단 응답 스킵 (강제 Accepted).
- 단 선수 개인 협상은 그대로 진행.

**상시 재계약 (사용자 피드백 2.5):**
- `TransferSystem.RenewContract(playerId, newContract, state, balance)` 신규.
- 시점 제약 X — 언제든. 단 잔여 6개월 이내 가산점.
- 주급 ↑ 비례 사기 회복 (`balance.contractRenewalMoraleBoost`).

**자유계약 시장 (FA):**
- `SeasonEndProcessor` FA 전환 유지.
- 잔여 6개월 이내 → 타 구단 `SubmitFreeAgentContract` 가능 (보스만 룰).

**이유:**
- **사용자 피드백 2.5 + Q7**: 상시 재계약 + 사기 연동. 협상 V1.0 정교화는 사용자 피드백에 명시는 없으나 FM 표준 / 시장 메커닉 필수.
- **Hidden 연동 (#40)**: loyalty / ambition 가 선수 협상 핵심 입력.
- **Promise 자동 생성**: 출전시간 약속 옵션 → 사기 안정. PromiseSystem (#43) 연동.

**영향 범위:** `OfferStatus.CounterOffer / Negotiating` enum / `TransferOffer` 신규 필드 (isLoan / loan* / parentClubId) / `TransferSystem` 메서드 확장 (RenewContract / SubmitFreeAgentContract) / `algorithms.md` #3 V1.0 갱신 / UI `NegotiationScene` 신규.

### V1.0+ 보완 포인트 (V1.x)

- **에이전트 / 사이닝 보너스 / 충성 보너스 / 출전 보너스 / 골 보너스** — Contract 확장. V1.x.
- **다중 오퍼 경쟁 (Interest System)** — 같은 선수에 여러 클럽 관심. V1.x.
- **트랜스퍼 리스트 자동 거래** — 시장가 ×0.7 자동 할인. V1.0 활성화, V1.x 정교화.

---

## 49. 시설 시스템 V1.0 — 8종 × 10단계 + 병렬 + 비용 인상 (사용자 피드백 2.1)

**결정:** 시설 8종 확장 + 등급 1-10 세분화 + 병렬 업그레이드 + 비용 인상 + 효과 본격 도입.

**8종 (V0.1 3종 → V1.0 8종):**
| FacilityType | 효과 |
| --- | --- |
| **Scout** | 스카우트 명단 크기 / 정확도 (#46) |
| **Training** | 1군 훈련 효율 (성장률) |
| **YouthCoach** | 유스 평균 PA + 고급 트레잇 부여 확률 |
| **YouthRecruitment** | 유스 풀 크기 + 인스펙션 빈도 |
| **YouthFacility** | 유스 선수 성장률 + 1군 콜업 적응 |
| **Medical** | 부상 회복 속도 + 부상 발생률 ↓ |
| **Stadium** | 입장료 수입 + 명성 가산 |
| **Gym** | 피지컬 스탯 성장률 + 부상 회복 일부 |

**등급 1-10 세분화 (V0.1 1-5 → V1.0 1-10):**
- 각 등급 효과 + 비용 비선형 (Lv1→2 저비용, Lv9→10 고비용).
- `FacilityLevelSO` 카탈로그 = 8 type × 10 level = 80 asset.

**병렬 업그레이드 (사용자 피드백):**
- 현재: 한 시설 진행 중 차단.
- 변경: 자금만 있으면 동시 N개 가능. 같은 시설은 한 번에 1단계.
- UI: 진행 중 업그레이드 목록 + 새 발주.

**비용 인상 (사용자 피드백 "너무 쌈"):**
- 비용 = `baseCost × pow(level, 2.5)` + 노이즈.
- Lv1→2 = ~50k, Lv9→10 = ~5M. 빅클럽 (자금 9M) 도 한 시즌 모아 Lv3→Lv4 부담스럽게.

**효과 본격 도입 (V0.1 미구현):**
- Training Lv N → 매주 stat 성장 `growthRate × (1 + N × 0.1)`.
- Medical Lv N → 부상 회복 일수 `÷ (1 + N × 0.05)` + 부상 발생률 `× (1 - N × 0.05)`.
- Stadium Lv N → 시즌 입장료 `baseStadiumIncome × N × clubReputation`.
- 등 (`v1.0-plan.md` §3.10.5).

**이유:**
- **사용자 피드백 2.1**: "시설 세분화 / 병렬 / 비용 인상 / 핵심 게임플레이 포인트" 직역.
- **유스 시설 분리 (`#35` V1.0+ + #50)**: 사용자 피드백 "청소년 코치 / 모집 시스템 분리" 직역 → 3분리 (YouthCoach / YouthRecruitment / YouthFacility).
- **시설 = 핵심 자원 의사결정**: 자금 → 어느 시설에 투자할지 = 시즌 운영 핵심 결정. FM 표준.

**영향 범위:** `FacilityType` enum 5 추가 / `Facilities` 도메인 8 필드 / `FacilitySystem` 병렬 업그레이드 / `FacilityLevelSO` 80 asset 신규 / 각 시설 효과 적용 (Training → 성장 시스템 / Medical → MatchPostProcessor 부상 회복 / 등) / UI `FacilityScene` 갱신.

### V1.0+ 보완 포인트 (V1.x)

- **시설 → Staff 도입** — Coach / Doctor / Scout 개별 인사. V1.x.
- **시설 등급 효과 곡선 다양화** — 일부는 선형, 일부는 임계점 (Lv5 = 1.5배, Lv10 = 2배).
- **시설 부작용** — 큰 업그레이드 = 시즌 중 매치 X (Stadium 공사 시 홈 어드밴티지 손실). V1.x+.

---

## 50. 유스 시스템 V1.0 — CA 캡 + 시설 분리 + 풀 전체 영입 + Mentoring (사용자 피드백 2.2)

**결정:** 사용자 피드백 3개 + Mentoring 신규.

**유스 CA 캡 ~100 (사용자 피드백):**
- V0.1: PlayerGenerator 호출 → CA 50-200 (명성 기반).
- V1.0: 유스 전용 분포 — `youthMinCa = 30 / youthMaxCa = 95`.
- PA 는 그대로 (PA 진실값 모델 #35) — 100-180.
- 16-18세 = CA 낮은 게 현실적. V0.1 너무 높은 CA 발생은 σ 과대 + 시드 충돌.

**풀 전체 영입 가능 (사용자 피드백):**
- 현재: subset 선택.
- 변경: UI 디폴트 "전체 영입" + 개별 선택 옵션.
- 영입 인원 제한 = `YouthRecruitment` 시설 등급. Lv1 → 풀 사이즈 ÷ 3, Lv10 → 풀 전체.

**유스 시설 분리 (사용자 피드백 + `#35` V1.0+):**
- V0.1 `Facilities.youthLevel: int` 단일 → V1.0 3분리 (#49).
- `youthCoachLevel` — 평균 PA + 트레잇 가중치.
- `youthRecruitmentLevel` — 풀 사이즈 + 인스펙션 빈도 (Lv7+ = 보조 인스펙션 추가).
- `youthFacilityLevel` — 유스 선수 성장률 + 1군 콜업 적응.

**Mentoring 시스템 (FM 표준 — V1.0 신규):**
- `Club.season.mentoringGroups: List<MentoringGroup>`.
- `MentoringGroup.cs`:
  ```csharp
  public class MentoringGroup {
      public int id;
      public int mentorPlayerId;        // 베테랑 (보통 30+ + leadership ↑)
      public List<int> menteePlayerIds; // 1-3명 (유스 / 어린 1군)
      public DateTime startedAt;
  }
  ```
- 효과 (월 1회 체크):
  - Mentee Hidden Attributes (`professionalism / determination`) 가 Mentor 쪽으로 수렴 (시즌당 ±5).
  - `ambition / loyalty` 도 영향.
- 외부화: `mentoringRateModifier`.
- UI: Squad → [Mentoring] 탭.

**라운드별 포지션 가중치 (`algorithms.md` #4 V1.0+):**
- 균등 → 가중치 변동. 어떤 인스펙션은 GK 0, AT 다수.
- 외부화: `youthPositionWeightVolatility = 0.5`.

**미영입 후보 → AI 다른 구단 영입 (`algorithms.md` #4 V1.0+):**
- 일정 확률 (`youthRejectedToOtherClubRatio = 0.3`) 로 다른 구단 영입.
- `YouthSignedByOtherEvent` 발행.

**1군 콜업 자동 트리거 + 유저 승인 (Q9):**
- 자동: 18세 + CA ≥ 클럽 평균 70% → `YouthPromotionSuggestedEvent` 발행.
- 유저: Dashboard 인박스 → 클릭 → PlayerProfile [1군 승격] / [거절].

**이유:**
- **사용자 피드백 2.2 + 2.1 직역**: CA 캡 + 풀 전체 + 시설 분리.
- **Mentoring**: FM 표준 + 사용자 피드백 "충성도·의리 같은 수치로 누그러뜨림" 의 long-term 변화 메커닉. Hidden Attributes 의 동적 변화.
- **자동 + 승인 (Q9)**: 유저 관리 부담 ↓ + 통제 보존.

**영향 범위:** `Facilities` 8 필드 / `MentoringGroup.cs` 신규 / `Club.season.mentoringGroups` / `YouthSystem.cs` 갱신 (CA 캡 / 풀 전체) / `MentoringSystem.cs` 신규 / `algorithms.md` #4 V1.0 갱신 / UI `MentoringScene` 신규 / `YouthPromotionSuggestedEvent / YouthSignedByOtherEvent` 신규.

### V1.0+ 보완 포인트 (V1.x)

- **추가 스카우트 (data-flows #4 [3-c])** — 비용 차감 + 정보 정확도 ↑. V1.x.
- **계약 기간 차등** — 시설 / 나이 / PA 기반. V1.0 균등 → V1.x 차등.
- **AI 클럽 인스펙션** — V1.0 = 유저 클럽만. V1.x 다른 클럽도 인스펙션 + 영입 결정.

---

## 51. 시즌 시스템 V1.0 — 시상 + 보드 평가 + 재정 결산 + 매니저 평판 (`#38` V1.0+ 실현)

**결정:** V0.1 미구현 5종 본격 도입.

**시상 (V1.0 신규):**
- `SeasonAward.cs` + `AwardType` enum 7종: LeagueMVP / TopScorer / TopAssist / YoungPlayer / BestEleven / GoldenGlove / ManagerOfSeason.
- `SeasonEndProcessor` 계산 단계 추가.
- 수상 선수 morale +10 / happiness +10.

**월간 어워드:**
- 매월 1일 `DailyProcessor` 가 직전 월 통계 계산.
- Manager of the Month — boardConfidence +5.
- Player of the Month — 평점 + 골/어시 기반 / 사기 +10.

**보드 평가 / 경질:**
- `Club.season.boardConfidence` 변동 (V0.1 50 고정 → V1.0 본격):
  - 매월: (실제 순위 vs 목표 순위) × multiplier
  - 매치: 패배 -2, 빅매치 패배 -5, 승리 +1
  - 보드 약속 미이행 -20
- < 30 → 경질 경고. < 10 → 경질 (V1.0 = Game Over).

**보드 약속 (시즌 시작):**
- 시즌 목표 순위 / 영입 예산 / 매각 예산.
- `Club.season.boardPromises: List<BoardPromise>`.
- 매니저 수락 / 거절. 거절 = boardConfidence -10.

**재정 결산:**
- `SeasonEndProcessor` 신규 단계:
  - 입장료 = 홈 매치 × stadium level × club reputation × baseFee
  - TV 중계권 = 시즌 평균 명성 × baseFee
  - 상금 = 리그 순위별 (1위 ~ 강등권 차등)
- `Club.finance.money` 갱신 + transferBudget / wageBudget 재계산.

**매니저 평판 (단순):**
- `GameState.managerReputation: int` 신규 (0-100).
- 변동: 우승 +20 / 승격 +15 / 보드 약속 이행 +5 / 경질 -30 / 월간 매니저 +5.
- V1.0: 효과 = boardConfidence 가산. V1.x 다른 구단 부임 / 미디어 / 국대.

**시즌 통계 저장 (사용자 피드백 2.8):**
- `Player.career: List<SeasonStat>` (V0.1 정의됨, 미사용) 채움.
- 시즌 종료 시 각 선수 그 시즌 통계 → `career` 에 추가.
- `League.history: List<SeasonHistory>` 신규 — 시즌별 순위 / 시상 보존.

**Match 데이터 압축 (`#8` 실현):**
- 시즌 종료 시 직전 시즌 외 Match `events / playerStats` 비움. 우승 / 강등 / 시상만 보존.

**이유:**
- **`#38` V1.0+ 실현**: 시즌 시스템의 의사결정 깊이 = 시상 / 보드 / 재정 정산.
- **사용자 피드백 2.8**: 리그 시즌 통계 저장 직역.
- **보드 평가**: FM 매니저 게임 핵심 — 시즌 운영의 동기 (경질 회피 / 보드 신뢰 ↑).

**영향 범위:** `SeasonAward / BoardPromise / SeasonHistory` 신규 / `Club.season.boardConfidence / boardPromises / captainPlayerId` / `GameState.managerReputation / activeAwards` / `SeasonEndProcessor` 단계 추가 (5종) / `DailyProcessor` 월간 어워드 단계 / UI `SeasonSummaryScene` 신규 / `algorithms.md` #11 시상 알고리즘 / event-bus `AwardWonEvent / BoardConfidenceChangedEvent / ManagerSackedEvent` 신규.

### V1.0+ 보완 포인트 (V1.x)

- **다른 구단 부임 (경질 후)** — Game Over 대신 다른 구단 오퍼. V1.x.
- **재정 정교화** — 스폰서십 / 광고 보드 / 부채 / 대출. V1.x.
- **사기 / 모랄 정산** — 우승팀 +, 강등팀 -, 약속 출전시간 미달자. V1.x.
- **보드 본격 인터랙션** — 예산 요청 / 비전 / 야망. V1.x.

---

## 52. 인프라 V1.0 — String Table + Localization + Save Migration (사용자 피드백 2.11)

**결정:** 3종 인프라 V1.0 도입.

**String Table (사용자 피드백):**
- 현재: UI 한글 직박 ("리롤" / "확정" / "다음 경기").
- 변경: `LocalizationSystem` 키 기반 조회.
- 데이터: `LocalizationSO` (CSV / JSON 임포트 가능).
- API: `Localization.Get(key, args)` static.
- 매치 텍스트 이벤트 (#44) 의 `textKey / textArgs` 도 같은 시스템.
- 마이그레이션: 기존 UI 코드 한글 → key 추출 (Stage A.3).

**Localization (사용자 피드백):**
- 영어 + 한국어 (V1.0 2 언어).
- `LocalizationSystem.CurrentLanguage: Language` enum.
- 게임 시작 시 시스템 언어 감지 / 옵션 변경.
- 폰트 — NotoSansKR 유지 (한·영 둘 다).
- V1.x: 일본어 / 중국어 / 스페인어.

**Save Migration (사용자 피드백 + Q8):**
- `GameState.saveVersion: int` 신규 (디폴트 2 = V1.0, V0.1 = 1).
- `SaveSystem.Load` deserialize 후 `saveVersion < currentVersion` → 마이그레이션.
- `SaveMigration.cs`:
  ```csharp
  public static class SaveMigration {
      public static GameState Migrate(GameState state, int targetVersion) {
          while (state.saveVersion < targetVersion) {
              state = Migrators[state.saveVersion + 1].Apply(state);
              state.saveVersion++;
          }
          return state;
      }
  }
  ```
- **V0.1 → V1.0 마이그레이션 = Q8 결정: 미지원** (V1.0 신규게임만). 단 인프라는 도입 (V1.0 → V1.1 등 후속 대비).

**자동 저장 (`data-flows.md` TBD):**
- 시즌 종료 시 자동 — `SeasonEndProcessor` 가 `SaveSystem.Save(state, "autosave_season_{year}")` 호출.
- 옵션: 매일 자동 (Dashboard 설정).
- 슬롯명: `autosave_001 ~ autosave_005` 순환 (5슬롯).

**이유:**
- **사용자 피드백 2.11 직역**: String Table + 영/한 + Save Migration.
- **Q8 결정**: V1.0 = 큰 재구조화. 마이그레이션 가치 < 비용. 단 SaveMigration 인프라는 V1.x+ 대비 필수.

**영향 범위:** `LocalizationSystem.cs / LocalizationSO` 신규 / 기존 UI 코드 ~11 씬 전수 한글 → key 추출 / `SaveSystem.Save / Load` 에 saveVersion 처리 / `SaveMigration.cs` 골격 / `GameState.saveVersion` 필드 / `coding-conventions.md` Localization 패턴 추가.

### V1.0+ 보완 포인트 (V1.x)

- **추가 언어** — 일본어 / 중국어 / 스페인어. V1.x.
- **Save 파일 압축 (gzip)** — 크기 ↓ ~50%. V1.x.
- **자동 저장 정교화** — 매일 / 시간별 / 매치 후 옵션. V1.x.
- **클라우드 동기화** — Steam Cloud 등. V1.x+.

---

## 53. 시설 효과 본격 적용 — Training + Medical + Gym (V1.0 D.4)

**결정:** V1.0 D.4 에서 3 시설 효과 본격 도입 (`algorithms.md` V1.0-10 + V1.0-11). Stadium / Scout / Youth* 은 후속 Stage (M.6 / E.2 / L.1-3) 의존.

**Training — Player Growth System (V1.0-10):**
- 매월 1일 `GrowthSystem.Tick(state, balance)` 호출 (V0.1 ProcessSchedule 패턴 일관).
- 1군 선수 대상 — Relative stats 만 변동 (Absolute = ×0.10 페널티).
- **2단계 모델**: (a) 발생 확률 = `growthBaseChance (0.01) × ageFactor × absoluteFactor × trainingBonus × gymBonus(피지컬) × paFactor`. (b) 발생 시 size 추첨 = `[+1, +2, +3]` 분포 `[75, 20, 5]` (peak youth = `[60, 30, 10]`).
- Training Lv N → `1 + N × 0.10` (Lv1 ×1.1, Lv10 ×2.0).
- decline (ageFactor < 0) 대칭 — `-1 / -2 / -3` 같은 분포.
- 결정성 — 시드 = `state.randomSeed ^ player.id ^ (year×12 + month)`.

**Medical — Injury Recovery + Rate (V1.0-11):**
- 회복 일수 = `InjuryTypeSO.recoveryDays / (1 + medicalLevel × 0.05 + gymLevel × 0.02)`.
- 부상 발생률 = `injuryBaseRate × max(0.5, 1 - medicalLevel × 0.05)` — floor 0.5 (게임플레이 유지).
- `DailyProcessor.ProcessRecovery` 매일 호출 — `expectedReturn` 도래 시 부상 해제.
- 매치 엔진 (Stage I.3) 호출 인터페이스만 D.4 도입. 실제 부상 발생 / 매치 분 단위 이벤트는 Stage I 에서.

**Gym — 보조 시설:**
- 피지컬 stat 8개 한정 성장 보정 (Acceleration / Agility / Balance / Jumping Reach / Natural Fitness / Pace / Stamina / Strength).
- 부상 회복 일부 보정 (×(1 + N × 0.02)).
- 발생률 보정 X (Medical 만).

**나이 곡선 (`GrowthSystem.ComputeAgeFactor`):**
- 16-22세: +1.5 ~ +0.9 (peak growth)
- 23-26세: +1.0 ~ +0.5 (prime)
- 27-30세: +0.0 (정체)
- 31세+: -0.2 ~ -1.0 (decline)

**CA-PA 캡:**
- V0.1 #35 PA 진실값 모델 정신 — PA = 캡. CA = PA 도달 시 성장 정지 (단 decline 은 가능, ageFactor < 0 일 때).
- CA = static field (generation 시점 고정). V1.x = derived from stats 검토 (`#24` V1.0+ 보완 포인트).

**임대 선수 (Stage K.3 Loan):**
- 현재 소속 (`currentClubId`) 클럽의 시설 영향. 원 소속 (`parentClubId`) X.
- 부상 회복 도중 임대 이동 — `expectedReturn` 고정 (V1.0). V1.x 재계산.

**Stadium / Scout / Youth* — D.4 책임 X:**
- **Stadium** → Stage M.6 (SeasonEndProcessor 재정 결산 시 `baseStadiumIncome × stadiumLevel × clubReputation × homeMatches`)
- **Scout** → Stage E.2 (ScoutingSystem 명단 크기 / 정확도 — `FacilityLevelSO(Scout).scoutPoolSize / scoutAccuracyRange` 활용)
- **YouthCoach / YouthRecruitment / YouthFacility** → Stage L.1-3 (유스 PA / 풀 크기 / 성장률)

**이유:**
- **D.4 스코프 한정 — 직접 효과 3 시설**: Stadium / Scout / Youth* 은 시즌 / 검색 / 유스 시스템의 일부라 해당 Stage 가 책임. D.4 에서 다 처리하면 후속 Stage 와 중복 + PR 사이즈 폭증.
- **성장 시스템 = 신규 시스템**: V0.1 = 선수 stat 시즌 내내 고정. V1.0 = 매월 변동 도입 (V1.0 의 핵심 시뮬레이션 깊이 추가).
- **결정성 보존**: V0.1 #17 시드 모델 일관 — 같은 시드 = 같은 성장 시퀀스.

**영향 범위:**
- `Application/GrowthSystem.cs` 신규 (Stateless)
- `Application/InjurySystem.cs` 신규 (Stateless — `ComputeRecoveryDays / ComputeInjuryRate / ProcessRecovery`)
- `DailyProcessor` 통합 — 매월 1일 `GrowthSystem.Tick` + 매일 `InjurySystem.ProcessRecovery`
- `GameBalanceSO` 신규 ~10 필드 (`growthBaseChance / growthAbsoluteFactor / growthTrainingCoeff / growthGymCoeff / growthPaGapNormalizer / growthYouthFactor / growthYouthPeakAge / growthPrimePeakAge / growthDeclineStartAge / injuryMedicalRecoveryCoeff / injuryGymRecoveryCoeff / injuryMedicalRateCoeff`)
- `Utils/StatMetadata.cs` — `IsPhysical(stat)` 메서드 추가 (피지컬 8 stat 판별, B.4 와 짝)
- `event-bus-catalog.md` — `PlayerStatChangedEvent` (V1.x UI 알림 용도, V1.0 = 도메인 이벤트만) / `PlayerInjuryRecoveredEvent` 신규 등록

### V1.0+ 보완 포인트 (V1.x)

- **개인 훈련 (Individual Training)** — 유저가 특정 선수 / stat 집중 훈련 (FM 표준).
- **시즌 외 프리시즌 캠프** — 6/1~8/15 추가 성장 (현 V1.0 = 매월 동일).
- **부상 중 성장** — 영향 X → ×0.5 검토.
- **Mentoring stat 영향** — Stage L.4 = Hidden 만. V1.x = stat 도 일부.
- **부상 multi-phase** — 회복 / 재활 / 컨디션 회복 단계.
- **CA derived from stats** — V0.1 #24 V1.0+ 보완 포인트 일관.
- **시설 → Staff 도입** (`#49` V1.x) 시 코치 quality 추가 입력.

---

## Change Log

| Date | Decision | Note |
| --- | --- | --- |
| 2025-05-15 | Initial decisions 1-22 | Pre-coding design phase |
| 2026-05-18 | #22 Malgun Gothic → NotoSansKR | Task 1.3 진행 중 라이선스/플랫폼 사유로 변경 |
| 2026-05-18 | #23 GameTime 자체 상태 보유 추가 | Task 2.2 작업 시 결정 |
| 2026-05-18 | #24~#26 추가 | algorithms.md #1 Player Generation 명세 작성 시 결정 (CA-Stats 분리 / 트레잇 충돌 그룹 / 2차 포지션 affinity) |
| 2026-05-19 | #27, #28 추가 | algorithms.md #5 Club Generation 명세 작성 시 결정. ratio 화로 가변 clubCount/playersPerClub 대응. V1.0+ 보완 포인트 각 결정에 별도 명시. |
| 2026-05-19 | #29 추가 | Task 2.3 마무리 (#76) 작업 중 GameManager 레이어 결정. `Core → Domain` 정통 의존 방향 복원 (`Domain.asmdef` 미사용 Core 참조 제거 + `Core.asmdef` 에 Domain 추가). 세 문서 (project-context / class-diagram / coding-conventions) Core 로 통일. |
| 2026-05-19 | #28 갱신 + #30~32 추가 | algorithms.md #6 Starting Squad Gacha 명세 작성 시 결정. 분배표 정책 `FormationConfig` 단위로 갱신 (필수 23 + 랜덤 2). Gacha 평가 정책 (4라인 + 명성 대비 + ACE). Reroll 재생성 + 새 id (`GameState.nextPlayerId` 신규). V0.1 단일 포메이션 → V1.0 가챠 랜덤화 확장 경로 명시. 출전 시간 시스템은 V1.0+ 보완 포인트로만 기록. |
| 2026-05-19 | #33, #34 추가 | algorithms.md #2 Match Simulation 명세 작성 (Task 9.1 Sub-A, #109) 시 결정. #33 V0.1 정책 (단순 CA 합 + Poisson + 홈 어드밴티지 + 포지션 라인 가중 득점자). #34 V1.0+ 이벤트 시퀀스 진화 경로 — 옐로 2장/부상→교체/외침 등 누적 처리 가능 구조. 인터페이스 유지로 V0.1 호출자 영향 없이 내부 교체 가능. |
| 2026-05-19 | #33 보강 | Sub-C 본 구현 검증 시 (#113) 단순 선형 ratio 의 결정력 부족 발견 — 강팀 원정 승률 51% 로 디자인 의도 부족. `strengthExponent` (k=1.5 기본) 비선형화 도입. V0.1 임시 변통으로 명시 — V1.0+ 매치 엔진 재작성 (#34) 시 폐기 예정. |
| 2026-05-20 | #35, #36 추가 | algorithms.md #4 Youth Pool Generation 명세 작성 (Task 10 Sub-A, #123). #35 V0.1 정책 (PA 진실값 + CA derived 역방향 / 스타 픽 5% PA bonus / 시드=`currentDate.Ticks`+`userActionHash` 결합으로 외부 마이닝+직플 영상 공유 둘 다 방어 / 시설 통합 등급 / 미영입 단순 제거 / 나이 가중치 / 자국 78%). #36 `GameState.nextIntakeId` 단조증가 카운터 (PlayerGen `nextPlayerId` 패턴). V1.0+ 보완 포인트 9개 정리 (시설 분리 / 포지션 가중치 / AI 영입 / CA-Stats 정합성 / 시드 강화 / 추가 스카우트 / 계약 차등 등). |
| 2026-05-20 | #37 추가 | algorithms.md #3 Market Value + Transfer Flow 명세 작성 (Stage 11 Sub-A, #130). 이적시장 (상시) / 이적시장 활성화 기간 (체결만, 6/1~8/31 + 1/1~1/31) 분리 — 미리 협상 가능 + 체결만 시기 제약. Market Value 6 요소 곱셈 공식 (CA pow 4 + PA gap + age + contract + position + injury) — 슈퍼스타 vs 평범 15.7배 차이 (사용자 의도). V0.1 단일 라운드 / 선수 자동 통과 / AI 영입 미구현. AI 응답 ±10% noise. 용어 정정 ("이적창" → "이적시장 활성화 기간"). V1.0+ 보완 포인트 7+ 항목. |
| 2026-05-20 | #38 추가 | Stage 12 시즌 사이클 명세 작성 (Sub-A, #135). 5/15 종료 / 6/1 회계연도 / 8/15 매치 개막 3 시점 변수명 분리 (혼동 회피). V0.1 도입 — FA 전환 + 33+ 확률적 은퇴 + NewSeasonProcessor (토큰/일정/리셋). V0.1 미구현 — 시상 / 보드 평가 / 재정 결산 / 사기 정산 / Match 압축 (모두 V1.0+ 별도 시스템과 짝). 캘린더/요일 dynamic 계산은 V1.0+ ("5월 마지막 토요일" 같은 — 매년 가변 일정). V1.0+ 보완 포인트 10 항목. |
| 2026-05-20 | #38 보강 | Stage 15 통합 테스트 (#59) 작성 시 GameInitializer 가 첫 매치를 seasonStart 당일에 배치 → GameLoop.AdvanceDay 가 시간 진행 후 처리하므로 영원히 미처리 발견. **프리시즌 컨셉 도입**: `seasonStart` = 프리시즌 시작일 (state.currentDate 초기값). 첫 매치 = `newSeasonOpening` (8/15) 부터. GameInitializer.NewGame 이 `firstMatchDate = seasonStart 이후 가장 가까운 newSeasonOpening` 계산 후 ScheduleGenerator 호출. 사용자 합의: "원래 FM 도 프리시즌부터 시작해서 팀 뽑고 전술 / 스탭 만지고 첫 경기 시작할 시간을 줘야 한다". NewSeasonProcessor 는 이미 동일 패턴 (`ComputeNewSeasonOpeningDate` 사용) — 일관성 확보. |
| 2026-05-22 | #39~#52 추가 | V0.1 빌드 마무리 후 V1.0 계획 수립 (`docs/v1.0-plan.md` 작성). 사용자 플레이테스트 피드백 11 카테고리 + 기존 V1.0+ 보완 포인트 + FM 표준 통합. 12 Open Questions 모두 결정 후 본 결정사항 #39~#52 추가. **§ 매핑**: #39 Stats 1-100 + FM 49 (Q1, Q12) / #40 Hidden Attributes (Q4) / #41 Trait 효과 본격화 / #42 Morale + Happiness 분리 (Q7) / #43 Promise + 면담 (Q7) / #44 매치 엔진 V1.0 분 단위 (#34 실현, Q5) / #45 Tactic 중간 스코프 (Q10) / #46 스카우트 이분법 (Q4) / #47 CpuTransferAi 필요 기반 (Q3) / #48 협상 V1.0 + 임대 / #49 시설 8종 × 10단계 + 병렬 / #50 유스 V1.0 (CA 캡 + 시설 분리 + Mentoring) / #51 시즌 V1.0 (시상 + 보드 + 재정) / #52 인프라 (String Table + Localization + Save Migration, Q8). 일정 정책 (Q11) = 마감 없음. |
| 2026-05-26 | #53 추가 | Stage D.4 Sub-A 명세 (`algorithms.md` V1.0-10 + V1.0-11 와 짝). 시설 효과 본격 적용 — Training (Player Growth System 신규) + Medical (Injury Recovery + Rate 보정) + Gym (피지컬 성장 보조 + 회복 일부). Stadium / Scout / Youth* 은 D.4 책임 X — 후속 Stage M.6 / E.2 / L.1-3 의존. 성장 시스템 = 매월 1일 / 2단계 모델 (발생 확률 + size 분포 +1/+2/+3) / Relative only (Absolute ×0.10) / 나이 곡선 4단계 (16-22 peak / 23-26 prime / 27-30 정체 / 31+ decline) / PA 캡. 결정성 시드 = `state.randomSeed ^ player.id ^ (year×12 + month)`. CA = static (V1.x derived 검토). 부상 회복 결정성 = 발생 시점 `expectedReturn` 고정. 발생률 floor 0.5 (Medical Lv10 도 부상 완전 차단 불가). |
| 2026-05-26 | #53 보강 | 성장 size 분포 도입. V1.0-10 의 초안 (`+1` 단위만) → 사용자 지적 ("특정 스탯 +2 가능") 반영. **2단계 모델**: (1) 발생 확률 `growthBaseChance = 0.01` (월 1% — 초안 0.05 너무 빈번해서 1/5 로 낮춤. 49 stat × 1% = 평범 선수 1년 ~6 stat 변동). (2) 발생 시 size 추첨 `[+1, +2, +3]` 분포 `[75, 20, 5]`. peak youth (ageFactor ≥ 1.3, 16-18세) 는 큰 점프 분포 `[60, 30, 10]`. decline 대칭. 18세 wonderkid 1년 stat 합산 ~12 (peak 추정), 평범 25세 ~5. FM 표준 (15-20 wonderkid / 5-10 평범) 와 일치. |
| 2026-05-26 | #47 V1.0+ 보완 포인트 2 항목 추가 | F.1+F.2 머지 (#295) 직후 사용자 지적. 현재 V1.0 한계 2가지 명세화 — (1) "매주 월요일 모든 AI 구단 동시" = 비자연스러운 동기화. (2) "구단당 주 1 오퍼" = 여름 윈도우 대규모 리빌딩 시나리오 X. V1.x 진화 = 구단별 cooldown (`Club.lastTransferAttemptDate`) + `DetectTrigger` → `DetectTriggers` (복수) + 자금 트리거별 분배. `aiPersonality` 와 결합 시 FM 식 비동기 + 다발 협상. 결정성 시드는 `lastAttemptDate.Ticks` 로 클럽별 독립 재현성 확보. V1.0 본문 정책 (매주 호출) 은 그대로 유지 — V1.x 스코프. |
