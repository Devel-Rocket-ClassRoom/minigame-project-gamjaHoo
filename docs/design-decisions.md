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

**외부화:** `GameBalanceSO` 신규 13개 필드 (`youthStarPickProbability=0.05`, `youthStarPaBonus=50`, `youthPaStdDev=15`, `youthPaGapStdDev=25`, `youthIntakeMinAge=16`, `youthIntakeMaxAge=18`, `youthIntakeAgeWeights={0.40, 0.40, 0.20}`, `youthPrimaryNationalityRatio=0.78`, `youthIntakeMainMonth/Day=6/15`, `youthIntakeSecondMonth/Day=1/15`). `algorithms.md #4` 참조.

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
