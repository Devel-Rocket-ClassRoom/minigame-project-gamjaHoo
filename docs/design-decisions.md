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

## 17. 경기 결과 — 시작 직전 시드 고정 (V0.1 한정 — "결과 미리 산출")

**결정 (V0.1):** 경기 결과는 경기 시작 직전 시드를 고정하고 미리 산출한다. 이후 표시되는 이벤트들은 그 결과에 부합하도록 생성.

**이유:**
- 결과 검증 가능
- 세이브 로드 후 같은 결과 보장 (멀티 세이브 일관성)

**V0.5 변경 (2026-05-27) — "결과 미리 산출" 완전 폐기:**
- V0.5 매치 엔진 (#44 5-zone Markov) 은 **forward simulation** — 매 분 emergent. "결과 먼저 정하고 이벤트 끼워맞추기" 폐기.
- **결정성의 진짜 출처는 시드 고정** (`match.id ^ randomSeed`) — "결과 미리 산출" 이 아님. 같은 시드 + 같은 입력 state → 같은 이벤트 시퀀스 → 같은 결과 (재현성 / 세이브 일관성 그대로 달성).
- 즉 이 결정의 _목적_ (검증 가능 + 세이브 일관성) 은 V0.5 에서도 유지되나, _수단_ (결과 미리 산출) 은 폐기. Markov forward 와 "결과 미리 정하기" 는 양립 불가 (결과 강제 시 이벤트 조작 필요 → 흐름 왜곡).
- SimulateLite (비활성 매치) 도 V0.5 은 Markov → "결과 미리 산출" 은 V0.5 전체 어디에도 없음 (V0.1 역사적 정책으로만 존재).

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

**대체:** V1.0 에서 Pretendard 등 미려한 폰트 고려.

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

**V0.5 변경 트리거:** 매치 시뮬레이션이 개별 stats 를 사용하기 시작하면 (algorithms.md #2 V0.5 확장 시점) `Player.DeriveCAFromStats(pos)` 도입 검토.

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
- V1.0 적응도 시스템의 데이터 구조 기반
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

### V0.5+ 보완 포인트

- **리그별 다른 ratio 표** — 현재는 모든 리그가 동일한 `tierClubRatios` 사용. V0.5 에서 LeagueConfigSO 로 이전해 ESP=빅2 강세, GER=빅3 + 평준화 등 리그 색깔 반영.
- **다중 리그 동시 운영** — 현재 ClubGenerator 는 단일 리그 호출 (caller loop 로 다중 리그도 가능하나 명성 통합 ranking 없음). V0.5 에서 이적 시장 연동을 위한 글로벌 명성 ranking 도입.
- **시즌 목표 동적화** — 현재 `targetLeaguePosition = i+1` (명성 순위 = 목표). V0.5 에서 보드 신뢰도·예산 조합 기반 동적 목표.

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
- **포메이션 단위 응집** — V0.1 4-4-2 단일 디폴트지만 `FormationConfig` 데이터 단위로 묶어 V0.5 진입 시 매끄러움 (`#32` 참조).
- **시드 기반 랜덤 2자리** — 구단 / 시드별 미세한 다양성. 결정성 보장.

**외부화:** `GameBalanceSO.formation: FormationConfig` (nested class — `[Serializable]` value object). V0.1 단일 인스턴스. V0.5 에서 `FormationSO` 로 추출.

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

**가변 `playersPerClub` 대응:** `Σ(필수) + randomSlots ≠ playersPerClub` 일 경우 V0.1 은 **분배표 합 기준으로 진행 + 경고**. V0.5 에서 ratio 화 검토.

### V0.5+ 보완 포인트

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

**V0.5+ 보완 포인트:**

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

V0.1 알고리즘 내 하드코딩. V0.5 에서 `PositionSO.lineCategory: Line` 필드 추가 검토 (외부화).

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

### V0.5+ 보완 포인트

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

### V0.5+ 보완 포인트

- **rerollsUsed 추적** — 시즌 첫 가챠 외 다른 Reroll 시스템 (유스 인스펙션 등) 과 별도 카운터 분리.
- **트랜잭션 정책** — Reroll 중 ClubGen 실패 시 롤백. V0.1 은 Assert 후 부분 실패.
- **derived seed 정책** — 현재 호출자가 `rng = new Random(state.randomSeed ^ club.id ^ rerollIdx)` 수동 부여. V0.5 에서 헬퍼 추출.

---

## 32. V0.1 단일 포메이션 + V0.5 가챠 랜덤화 확장

**결정:** V0.1 에서 4-4-2 단일 포메이션만 지원. `FormationConfig` 데이터 단위로 묶어 V0.5 진입 시 매끄럽게 확장.

**V0.1:**
- `GameBalanceSO.formation: FormationConfig` 단일 인스턴스 (4-4-2).
- ClubGen 가 이걸 사용해 분배표 생성.
- 가챠는 평가 / 리롤만. 포메이션 선택 없음.

**V0.5+ 확장 시나리오 (사용자 의도):**
- `FormationConfig` → `FormationSO` 로 추출.
- `List<FormationSO> availableFormations` 카탈로그 (4-4-2 / 4-3-3 / 3-5-2 / 4-2-3-1 / 4-4-1-1 등 5~6개).
- **가챠 시 포메이션 랜덤 추첨** — 각 구단마다 다른 포메이션. 분배표도 그에 맞춰 다름.
- 각 포메이션이 굴러갈 수 있는 **최소 구성 비율 보장** — `FormationConfig` 의 필수 인원 + 그룹 정책이 이미 그 형태로 설계됨.

**이유:**
- V0.1 단순화 (단일 포메이션) 와 V0.5 확장성 동시 달성.
- nested class → SO 추출은 직관적 마이그레이션 경로.

### V0.5+ 보완 포인트

- **FormationSO 신규** — id / name / 분배표 정책 / 전술 매개변수 (압박, 라인 높이 등) 보유.
- **가챠 추첨 메커닉** — 명성 가중치 (빅클럽 = 화려한 포메이션 ↑) 도 검토.
- **유저 변경 가능성** — 초기 가챠 후 시즌 중 포메이션 변경 가능 여부 (전술 화면 UX).

---

## 33. V0.1 Match Simulation 정책 — 단순 CA 합 + Poisson

**결정:** V0.1 매치 시뮬레이션은 **결과 우선** 모델 (`#17` 정신 계승). 양 팀 starting11 의 CA 합 → Poisson 분포로 골수 결정 → 라인 가중 + CA 비례 추첨으로 득점자 결정.

```
1. rng = new Random(match.id ^ state.randomSeed)        # 시드 고정 (#17)
2. starting11 = top-11 by CA (부상자 제외)              # V0.1 자동 선정 (라인업 시스템 V0.5+)
3. teamStrength = SUM(starting11.CA)
4. λ_home = totalLambda * (homeStrength / total) + homeAdvantageGoalBonus
   λ_away = totalLambda * (awayStrength / total)
   homeScore = Poisson(λ_home), awayScore = Poisson(λ_away)
5. 골 마다 weight = balance.scoringWeightByLine[line] * (p.CA / 100) 로 득점자 추첨
```

**이유:**

- **Poisson 분포**: 실제 축구 골 분포의 학계 표준 (Dixon-Coles 1997 등). 같은 λ 라도 매 매치 다른 결과 — 강팀이 무득점, 약팀이 이변 가능. 결정성과 자연 분포 동시 충족.
- **단순 CA 합 (`#24` 일관)**: V0.1 매치는 CA 만 사용, 개별 stats 무관. 라인별 가중치 / 포지션 적합도 / 폼·사기·피로 보정 모두 V0.5+. 매치 시뮬레이션 복잡도 ↓.
- **starting11 = top-11 by CA**: V0.1 라인업 결정 UI / 자동 라인업 알고리즘 없음 (Task 13 까지). 시뮬레이터가 자동 선정. 포지션 무시 — 명세 단순화.
- **득점자 = 라인 가중치 × CA**: 공격수가 ~60% 득점 (현실 분포). CA 보정으로 에이스 효과. `algorithms.md` #6 의 4라인 분류 재사용 → 일관성.
- **홈 어드밴티지 = home λ 가산**: 단순 + 의도 직관적 ("홈팀 이점"). EPL 통계 근사 (홈 46% / 무 26% / 원정 28%).
- **결과 우선 모델 (#17 정신)**: 시드 고정 → 결과 미리 산출 → 표시 이벤트는 결과에 부합. V0.1 에선 스코어 + 득점자만 미리 결정. 표시할 텍스트 이벤트는 V0.5+.

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

**V0.1 한정 명시:** 이 변통은 V0.1 단순 CA 합 모델의 결정력 부족을 임시 보강하는 것. V0.5+ 매치 엔진 재작성 시 (`#34` 이벤트 시퀀스) 폐기 예정 — finishing / composure / decisions 등 개별 stats 가 슈팅 변환률을 직접 결정하므로 비선형 보정 불필요.

### V0.5+ 보완 포인트

- **개별 stats 사용** — `#24` V0.5 트리거. 매치가 finishing / passing / tackling 등 직접 참조 시 stats 합과 CA 가 자연스럽게 일치하도록 derived CA 모델 검토.
- **`strengthExponent` 폐기** — 위 V0.1 임시 변통. 매치 엔진 재작성 시 k=1 회귀 또는 알고리즘 자체 제거.
- **라인업 결정 시스템** — 자동 라인업 (포지션 필수 + top-by-CA) → 유저 수동 라인업 UI. `Simulate(match, state, homeXI, awayXI)` 오버로드 도입 시점.
- **컵 연장전 + 승부차기** — `Match.type == FACup/CarabaoCup` 분기. 동점 시 `extraTimeLambda` Poisson 한 번 더 → 그래도 동점이면 승부차기 (별도 5+ 라운드).
- **비활성 구단 경량 시뮬** — V0.1 에선 단일 `Simulate` 사용. 이벤트 시퀀스 시스템 도입 후 비활성 구단 전용 경량 경로 (`SimulateLite`) 분리 검토. `data-flows.md` #3 갱신과 짝.
- **외부 영향 반영** — strength 계산 시 폼·사기·피로 곱셈 보정 (`design-decisions.md` #30 출전 시간 / 사기 시스템과 연동).

---

## 34. V0.5+ Match Simulation 진화 경로 — 이벤트 시퀀스

**결정:** V0.1 의 "결과 우선" 모델은 V0.5+ 에서 **분 단위 이벤트 시뮬레이션** 으로 전환. 인터페이스 `MatchSimulator.Simulate(match, state) → MatchResult` 는 유지 — 호출자 (`GameLoop`, `BackgroundSimulator`, `MatchPostProcessor`) 영향 없음. 내부만 교체.

**V0.1 (결과 우선)** vs **V0.5+ (이벤트 시퀀스)**:

```
V0.1: rng 고정 → 양 팀 strength → λ → Poisson(home/away goals) → 득점자 추첨 → MatchResult
V0.5+: rng 고정 → 분 단위 step (1~90) →
         step 마다 이벤트 발생 (슈팅 시도, 카드, 부상, 교체 …) →
         누적 상태 (점수, 카드 수, 부상자, 11→10명 등) 가 다음 step 분기에 영향 →
         최종 누적 = MatchResult
```

**왜 진화가 필요한가:**

1. **앞 이벤트가 뒤 이벤트에 영향** — 옐로 2장 → 퇴장 → 10명 → strength ↓ → 골 확률 ↓ 같은 누적 효과를 결과 우선 모델로는 표현 불가.
2. **부상 → 교체** — 부상자 발생 시 벤치 strength 가 들어옴. 교체 타이밍이 결과에 영향.
3. **교체 / 외침 등 유저·AI 의사결정 반영** — V0.5+ 텍스트 이벤트 시스템 도입 후 유저 응답 (전반 종료 코칭 코멘트 등) 이 후반에 영향.
4. **카드 / 부상 시스템 자연 발생** — 분 단위 이벤트가 곧 카드/부상 발생 지점.

**왜 V0.1 에선 안 하는가:**

- 분 단위 시뮬레이션은 복잡도 ↑↑ (이벤트 종류 정의 / 분기 / 확률 곱 / 누적 상태 / AI 교체 로직).
- V0.1 스코프 (2~3주) 에선 결과 우선 모델로 충분 — 스코어 + 득점자만 표시.
- **사용자 의도**: "교체는 AI 가 자동" / "외침 등 V0.5+ 에 추가될 때 재변동 가능" — 인터페이스만 유지하면 V0.5 에서 내부 자유롭게 교체 가능.

**인터페이스 호환성 보장:**

- `MatchSimulator.Simulate(match, state) → MatchResult` 시그니처 동일 (`class-diagram.md` 합의).
- 시드 결정성 (`#17`) 정신 보존 — 매 step rng 상태 누적이지만 같은 시드 → 같은 시퀀스 → 같은 결과.
- `MatchResult` 스키마 호환 — V0.5+ 에선 `assists` / `rating` / `yellowCards` / `redCards` 가 0 이 아니게 채워지지만 필드 추가/제거는 없음.

**V0.1 코드의 운명:**

- 4단계 Poisson + 5단계 라인 가중 추첨 알고리즘은 V0.5+ 진입 시 **제거**. 대신 이벤트 시퀀스 엔진이 자체적으로 슈팅 시도 / 골 / 어시스트 / 카드 / 부상 등을 분 단위로 발생.
- 다만 V0.1 의 외부화 파라미터 (`avgGoalsPerMatch`, `homeAdvantageGoalBonus`, `scoringWeightByLine`) 일부는 V0.5+ 에서도 재활용 가능 (특히 라인 가중치).
- V0.1 EditMode 테스트 (T1~T7) 는 V0.5+ 진입 시 인터페이스 차원 테스트만 유지 (결정성 / 강팀 승률 / playerStats 정확성) — Poisson 분포 통계 테스트는 폐기 후 이벤트 시퀀스 테스트로 교체.

### V0.5+ 보완 포인트 (이벤트 시퀀스 도입 시)

- **이벤트 종류 정의** — Shot / Save / Goal / YellowCard / RedCard / Injury / Substitution / OffsideCalled / Foul …. 각 이벤트의 발생 확률 공식 / 결과 분기.
- **분 단위 vs 이벤트 단위** — 매 분 RNG 굴리기 (90 step) vs Poisson 으로 시간 간격 샘플링. 후자가 단순.
- **AI 교체 시스템 (`SubstitutionAI`)** — 피로 / 부상 / 전술 / 스코어 상황 기반 자동 교체. V0.1 starting11 자동 선정과 같은 자리.
- **유저 코칭 인터럽트** — 전반 종료 / 중요 이벤트 시 유저에게 외침·교체·전술 변경 옵션. UI 의존성.
- **퇴장 후 strength 보정** — 11명 → 10명 시 strength × 0.9 같은 보정 또는 자연 발생 이벤트 (10명은 슈팅 시도 횟수 자체가 줄어 자연 반영).
- **`MatchEvent` 도메인 필드 활용** — `class-diagram.md` 의 `Match.events: List<MatchEvent>` placeholder 가 본격 사용. 분 단위 이벤트 기록.

**V0.5 실현 (2026-05-27) — 5-Zone Markov 채택:**
- 이 진화 경로가 #44 에서 본격 실현. 단, "분 단위 vs 이벤트 단위" 중 **OFM 5-zone Markov** (ballZone + possession 상태 전이) 채택 — 위 보완 포인트의 "매 분 RNG" 보다 상태 전이가 "앞 이벤트 영향" 을 더 자연스럽게 표현.
- 초안 (I.1/I.2 "양 팀 독립 추첨") 은 상태 전이가 없어 폐기 → 5-zone 재설계. 상세 #44 / `algorithms.md` V0.5-2.

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

4. **V0.1 시설 통합 등급 (사용자 #1)**: `FacilityLevelSO(Youth)` 가 시설 + 코치 + 모집 통합 책임. 실제 FM 메커닉 (시설 ≠ 코치 ≠ 모집) 은 V0.5+ 분리 트리거로 명세. V0.1 단순화.

5. **미영입 V0.1 단순화 (사용자 #9)**: 영입 결정 후 미영입 후보 모두 GameState 제거. `intake.rejectedPlayerIds` 에 ID 만 보관 (`#7` 영구 저장). V0.5+ AI 다른 구단 영입 시스템 트리거.

6. **나이 가중치 + birthDate 저장 (사용자 #4)**: 16=40%, 17=40%, 18=20%. `PersonalInfo.birthDate` 저장 (age 필드 별도 X) — PlayerGen 패턴 그대로. 미래 홈그로운 / 출전 가능 나이 / 적응 기간 등 계산 시 birthDate 필수.

7. **국적 자국 78% (사용자 #8)**: ClubGen 의 `primaryNationalityRatio=0.70` 보다 ↑. 유스는 자국 출신 비중이 더 큰 게 현실적 + 게임 만족감.

**외부화:** `GameBalanceSO` 신규 12개 필드 (`youthStarPickProbability=0.05`, `youthStarPaBonus=50`, `youthPaStdDev=15`, `youthPaGapStdDev=25`, `youthIntakeMinAge=16`, `youthIntakeMaxAge=18`, `youthIntakeAgeWeights={0.40, 0.40, 0.20}`, `youthPrimaryNationalityRatio=0.78`, `youthIntakeMainMonth/Day=6/15`, `youthIntakeSecondMonth/Day=1/15`). `algorithms.md #4` 참조.

### V0.5+ 보완 포인트

- **유스 시설 분리** — `FacilityLevelSO(Youth)` 통합 등급 → `Club.youthCoachLevel` (PA 평균) / `Club.youthRecruitmentLevel` (풀 크기) 분리. 시설 등급은 다른 효과 (스타플레이어 인지도 / 외국 유스 영입 가능 / 보드 신뢰도 +) 로 재정의.
- **포지션 가중치 변동** — V0.1 균등 → V0.5+ 라운드별 가중치 가챠 (어떤 인스펙션은 GK 0명, ST 다수 / 다른 인스펙션은 반대). `youthPositionWeightVolatility` 같은 외부화 도입.
- **AI 다른 구단 영입** — 미영입 후보 일정 확률 (`youthRejectedToOtherClubRatio`) 로 다른 구단 영입. 알림 이벤트 + 추후 조우 시 디스플레이.
- **CA-Stats 정합성 (algorithms.md #1 V0.5 트리거와 짝)** — PA → CA 단순 derived 대신 stats 가중합 기반 derived 검토. 같은 PA 라도 stats 분포에 따라 CA 다양화.
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

### V0.5+ 보완 포인트

- **id 재사용 검토 X** — `#31` 과 동일 정책. 디스크 / 메모리 절약 미미. 디버그 / 결정성 손실 큼.
- **derived seed 헬퍼 추출** — 현재 `state.randomSeed ^ currentDate.Ticks ^ userActionHash ^ club.id ^ intake.id ^ rerollsUsed` 가 호출자 수동 조합. V0.5 에서 `IntakeSeed.Compute(state, club, intake)` 헬퍼 추출.

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

3. **V0.1 단일 라운드 / 자동 통과**: AI 판매 구단 응답 (Accept/Reject) 만. 역제안 / 다중 라운드 / 선수 협상 V0.5+. V0.1 단순화 우선.

4. **AI 구단 영입 미구현 (V0.1)**: 다른 AI 구단은 능동 영입 행동 X. 사용자 클럽만 오퍼 제출. V0.5+ AI 영입 시스템 (CpuTransferAi).

5. **PA 노출 V0.1 정확도 100%**: 스카우트 시스템 V0.5+. V0.1 검색 결과 모든 선수 정확한 CA/PA 표시.

6. **시드 결정성 (`#17`)**: `rng = new Random(state.randomSeed ^ offer.id ^ currentDate.Ticks)` AI 응답 결정성. ±10% noise (aiValueNoiseSigma) 로 평가 부정확성 표현.

7. **DailyProcessor 통합**: `ProcessOffers` 가 매일 호출. Pending → AI 응답, Accepted → 활성화 기간 시 자동 체결.

8. **활성화 기간 외부화**: `transferWindowSummerStart/End` + `transferWindowWinterStart/End` (4쌍 month/day). V0.5+ LeagueConfigSO 로 이전.

9. **용어 정정**: ❌ "이적창" (모호) → ✅ **"이적시장 활성화 기간"** (한국어 docs / UI 라벨). 영어 변수명 `transferWindow*` / `IsTransferWindowOpen` 그대로 (도메인 표준).

**외부화:** `GameBalanceSO` 신규 ~13개 필드 (`marketValueBase`, `marketValueCaExponent`, `marketValuePaCoeff`, `marketValueAgeCurve[4]`, `marketValueContractCurve[4]`, `marketValuePositionFactor[4]`, `marketValueInjuryFactor`, `aiValueNoiseSigma`, `aiAcceptRatio`, transferWindow* 4쌍). `algorithms.md #3` 참조.

### V0.5+ 보완 포인트

`algorithms.md #3` V0.5 Migration Notes 30+ 항목 종합 — 가장 중요한 큰 트리거만 여기 정리:

- **선수/구단 reputation 도입** → Market Value 곱셈 보정. 빅네임 / 빅클럽 프리미엄.
- **AI 협상 시스템** — 역제안 (CounterOffer status) + 다중 라운드 + 선수 개인 협상.
- **AI 구단 영입 시스템 (CpuTransferAi)** — 약점 포지션 / 자금 여유 / 명성 기준 자동 의사결정.
- **스카우트 시스템** — `Club.facilities.scoutLevel` 기반 검색 정확도. PA 추정치 / 트레잇 노출 정도.
- **에이전트 / 보너스 / 임대 시스템** — Contract 확장.
- **계약 갱신 + FA (자유계약)** — 만료 6개월 전 갱신 협상 / 자유이적.
- **트랜스퍼 리스트 / 다른 클럽 인지 (Interest)** — 다중 오퍼 경쟁.

---

## 38. V0.1 시즌 사이클 정책

**결정:** V0.1 시즌 사이클 = **5/15 종료 + 6/1 회계연도 + 8/15 매치 개막** 3 시점 분리 + FA 전환 / 은퇴 처리 도입 + 시상·보드 평가·재정 결산·사기 정산·Match 압축 V0.5+ 미루기.

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
   - V0.5+ 트리거: **캘린더 / 요일 정보 도입 시** "5월 마지막 토요일" / "8월 셋째 토요일" 같은 dynamic 계산. `LeagueConfigSO.seasonEndRule` 같은 enum + DayOfWeek 처리.

3. **계약 만료 → FA 전환 V0.1 도입 (필수)**:
   - 만료 선수 `currentClubId = -1` + `club.seniorSquadIds` 제거
   - 한 시즌 완주 후 자유계약 시장 형성. V0.5+ 갱신 협상 추가.

4. **은퇴 처리 V0.1 단순 도입**:
   - `age >= balance.retirementMinAge (33)` + `rng.NextDouble() < balance.retirementProbabilityPerYear (0.15)`
   - 은퇴 시 `GameState.RemovePlayer` (단순). V0.5+ `Player.isRetired` 플래그 + 능력치 하락 곡선 + 사후 통계
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
   - 모두 V0.5+ 별도 시스템과 짝 (사기 / 평점 / 재정 시스템 등)
   - V0.1 한 시즌 완주 기능에 비필수
   - `data-flows.md #6` 의 해당 단계 = V0.5+ 트리거로 명세 갱신

7. **EventScheduler 통합 — 2 신규 트리거**:
   - 5/15 → `SeasonEndProcessor.Run` + `SeasonEndedEvent` 발행 + 정지 신호
   - 6/1 → `NewSeasonProcessor.Run` + `SeasonStartedEvent` 발행 + 정지 신호

**외부화:** `GameBalanceSO` 신규 6 필드 (`seasonEndMonth/Day`, `fiscalYearStartMonth/Day`, `newSeasonOpeningMonth/Day`). 기존 4 필드 (`seasonRerollTokenGrant`, `maxRerollStockpile`, `retirementMinAge`, `retirementProbabilityPerYear`) 재활용.

### V0.5+ 보완 포인트

- **캘린더 / 요일 dynamic 계산** — `DayOfWeek` 활용 ("5월 마지막 토요일") + LeagueConfigSO 의 enum 기반 규칙. 매년 가변 일정 자연 발생.
- **시상 시스템** — `SeasonAward` 도메인 (MVP / 득점왕 / 영플레이어 / 베스트XI). `SeasonEndProcessor` 에 단계 추가.
- **보드 시즌 평가 / 경질** — `Club.season.boardConfidence` 변동 (목표 - 실제 순위 차이 × multiplier). 임계점 이하 시 경질 알림.
- **재정 결산** — 입장료 (홈 매치 수 × 명성 multiplier) + 중계권 (순위 기반) + 상금 (1~4위 차등). `Club.finance.money` 갱신.
- **사기 / 모랄 정산** (#30) — 우승팀 +, 강등팀 -, 약속 출전시간 미달자 등.
- **Match 데이터 압축** — `Match.events` / `playerStats` 디테일 제거. 우승 / 강등 / 시상 정보만 보존 (#8 패턴).
- **계약 갱신 협상** — 만료 6개월 전부터 갱신 협상 시작. V0.1 FA 전환과 짝.
- **은퇴 정교화** — 능력치 하락 곡선 + `Player.isRetired` 플래그 + 사후 통계 / 명예의 전당.
- **승강** (`data-flows.md #6` 명시) — V0.1 단일 리그라 미구현. V0.5+ 다중 리그 + 승강.
- **레전 (Regen)** — 은퇴 / 자유계약 선수 일부를 차세대 유스로 환생. `PlayerOrigin.Regen` enum 이미 존재.

---

## 39. Stats 스케일 + 카테고리 — FM 49 stats 1-100 + CA/PA 1-200 (V0.5)

**결정:** V0.5 Stats 49 필드 (FM26 표준 1:1 매핑) + 스케일 1-100 (FM 1-20 → 5배 세분화). CA / PA 는 FM 표준 1-200 그대로.

**카테고리 (49)**:
- **Technical 14**: Corners, Crossing, Dribbling, Finishing, First Touch, Free Kick Taking, Heading, Long Shots, Long Throws, Marking, Passing, Penalty Taking, Tackling, Technique
- **Mental 14**: Aggression, Anticipation, Bravery, Composure, Concentration, Decisions, Determination, Flair, Leadership, Off the Ball, Positioning, Teamwork, Vision, Work Rate
- **Physical 8**: Acceleration, Agility, Balance, Jumping Reach, Natural Fitness, Pace, Stamina, Strength
- **Goalkeeping 13**: Aerial Reach, Command of Area, Communication, Eccentricity, First Touch (GK), Handling, Kicking, One on Ones, Passing (GK), Punching Tendency, Reflexes, Rushing Out (Tendency), Throwing

**이유:**
- **FM 1:1 매핑 (Q1)**: 사용자 피드백 "실제 FM 스탯 가져와서 갱신" 직역. FM 유저 친숙. V0.1 42 필드 → 49 (보강 7).
- **1-100 (Q12)**: 사용자 명시 "0~20 → 1~100 으로". FM 1-20 보다 5배 세분화 → 매치 시뮬의 미세한 stat 차이 자연 노출. CA / PA 는 도메인 표준 보존 (CA = 4 카테고리 종합 / Stat = 개별 — 단위 차이가 가독성 ↑).
- **재밸런싱 필수**: V0.1 1-20 외부화 수치 (statMeanAtCAFloor 등) 전부 1-100 기준 재산정. `algorithms.md` #1 갱신.

**영향 범위:** `Stats.cs` 4 카테고리 신규 7 필드 / `PlayerGenerator` 3단계 / `GameBalanceSO` ~25 stat 필드 / 시드 자산 / UI 표시 / Save Migration X (V0.1 → V0.5 무효, #52).

**V0.1 → V0.5 매핑** (명세상 추적, Save Migration 미적용):
| V0.1 카테고리 | V0.1 필드 | V0.5 변경 |
| --- | --- | --- |
| Technical (12) | passing, shooting, tackling, dribbling, heading, crossing, firstTouch, finishing, longShots, freeKickAccuracy, penaltyTaking, corners | + marking, technique, longThrows / shooting 제거 (finishing 대체) / freeKickAccuracy → freeKickTaking 명칭 |
| Mental (12) | vision, anticipation, composure, concentration, decisions, determination, leadership, offTheBall, positioning, teamwork, workRate, aggression | + bravery, flair |
| Physical (8) | acceleration, agility, balance, jumping, naturalFitness, pace, stamina, strength | jumping → jumpingReach 명칭 |
| GK (10) | aerialReach, commandOfArea, communication, eccentricity, handling, kicking, oneOnOnes, reflexes, rushingOut, throwing | + firstTouchGk, passingGk, punchingTendency |

### V0.5+ 보완 포인트 (V1.0 검토)

- **stat 별 매치 영향 명세** — Shot 결과 분기에 `finishing × composure`, Save에 GK `reflexes × handling` 등 명시적 매핑. V0.5 매치 엔진 (#44) 작성 시 확정.
- **stat 카테고리 가중치 위치별 영향** — PositionSO 의 emphasis flags 가 stat 가중치로 진화. ST = finishing emphasis × 1.5, CB = tackling emphasis × 1.5 등.

---

## 40. Hidden Attributes + Absolute/Relative 분리 (V0.5)

**결정:** `Player` 에 신규 도메인 객체 `hiddenAttrs: HiddenAttributes` 도입 (1-100, 9 필드). 사용자 피드백 "trait 의리·부상빈도 수치형 0-20" 흡수. Trait 자체는 명시적 플레이스타일 마커로 재정의 (#41).

**HiddenAttributes 9 필드:**
- `loyalty` (충성도) — 재계약 시 주급 요구 ↓, 이적 요청 ↓
- `ambition` (야망) — 출전시간 부족 / 빅클럽 오퍼 시 이적 요청 ↑
- `professionalism` (프로페셔널) — 훈련 효율 / 사기 안정 (변동폭 ×0.7)
- `pressureHandling` (압박 내성) — 빅매치 평점 가산
- `temperament` (기질) — 카드 / 라커룸 분위기
- `controversy` (논란성) — 미디어 사고 확률 (V1.0 미디어 시스템 도입 시)
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

**영향 범위:** `Player.hiddenAttrs` 신규 / `PlayerGenerator` 단계 추가 (Hidden 추첨) / `MoraleSystem` 의 변동 보정에서 hidden 참조 / `TransferSystem` 협상에서 loyalty/ambition 참조 / Save Migration X (V0.1 → V0.5 무효).

### V0.5+ 보완 포인트 (V1.0)

- **Personality 도입** — FM 표준 (Driven / Model Citizen / Professional / Resolute 등 ~30종 마커). Hidden Attributes 조합으로 계산. UI 표시.
- **Media Handling Style** — FM 표준. Hidden + Personality 조합. V1.0 미디어 시스템과 짝.

---

## 41. Trait 효과 본격화 + 카테고리화 (V0.5)

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

**V0.5 카탈로그 (~20 trait)**:
- V0.1 6개 (조숙형/늦깎이형/부상취약/멘탈강자/빅매치형/만능형) + V0.5 신규 ~14 (클러치 / 무리한패스 / 와이드플레이 / 자국인우대 / 유리몸 / 철인 / 멘탈약자 / 슈퍼유망주 / 멀티포지션 / 골결정력 / 수비형윙백 / 정신적리더 / 페널티스페셜리스트 / 프리킥마이스터).

**이유:**
- **효과 본격 도입**: V0.1 = 라벨만. V0.5 = 매치 엔진 (#44) 분기 + 성장 시스템 + 부상 시스템 본격 활용.
- **Hidden Attributes 와 분리**: Hidden = 수치형 인성. Trait = 명시적 플레이스타일 마커. 둘 다 같은 효과 (Morale / 매치) 에 입력되지만 Hidden 은 미세한 수치 곱셈, Trait 은 분기 / 큰 가중치.
- **데이터로 관리**: TraitSO 추가 / 삭제 = SO asset 변경. 코드 분기 없음.

**영향 범위:** `TraitSO` 필드 추가 / `TraitEffect` 클래스 신규 / 매치 엔진 (#44) trait 효과 통합 / 성장 시스템 / `algorithms.md` #1 Player Generation 의 트레잇 부여 단계.

### V0.5+ 보완 포인트 (V1.0)

- **트레잇 카탈로그 50+ 확장** — FM 트레잇 (Plays One-Twos / Likes to Beat Offside Trap 등) 본격 도입.
- **트레잇 변화 (Learn / Lose)** — Mentoring (#50) 으로 베테랑이 mentee 에게 트레잇 전수.
- **트레잇 노출 정확도** — 스카우트 시설 등급별 트레잇 노출 정도 (현 V0.5 = displayName 일부, V1.0 = 효과 정확 / 일부 / 비공개).

---

## 42. Morale + Happiness 분리 (V0.5)

**결정:** V0.1 의 `PlayerState.morale` (변동 없음) → V0.5 `morale` (단기) + `happiness` (장기) 분리. 변동 트리거 본격 도입.

**구조:**
- `Morale` (0-100, 단기) — 매치 / 라커룸 / 코칭 코멘트 기반. 매주 회복 경향.
- `Happiness` (0-100, 장기 추세) — 약속 / 출전시간 / 구단 성적 / 재계약 기반. 변화 느림.
- `PlayerState.happiness: int` 신규 필드 (디폴트 50).

**변동 트리거 (전체 매트릭스는 `v0.5-plan.md` §3.4.2 참조):**
- Morale: 매치 결과 (±5 ~ ±15) / 코칭 코멘트 / 라커룸 분위기 / 면담
- Happiness: 약속 이행 / 출전시간 / 강등·우승 / 재계약 / 보드 약속 / 면담 (지속 영향)

**Happiness 임계점 → 행동 분기:**
- ≥ 80: 만족 (보너스 +)
- 60-79: 양호 (정상)
- 40-59: 불만 표시 (인터뷰 사고 V1.0)
- 20-39: 이적 요청 (`TransferRequestEvent` — Q9 자동 트리거 + 유저 승인 패턴)
- < 20: 반항 (훈련 거부 / 평점 -)

**Hidden Attributes 연동 (#40):**
- `loyalty` 높을수록 Happiness 하락폭 ↓
- `ambition` 높을수록 강등 / 출전시간 미달 시 하락폭 ↑
- `professionalism` 높을수록 변동폭 전체 ×0.7

**라커룸 분위기 (V0.5 단순):**
- `Club.season.dressingRoomMood: int` — 1군 Happiness 평균 + 캡틴 leadership 가산점.
- < 30 → 시즌 폼 전체 -5 보정.

**이유:**
- **사용자 피드백 2.6**: "강등 시 불만 / 약속된 출전시간 미달 / 충성도·의리로 누그러뜨림" 직역.
- **Morale vs Happiness 분리**: 단기 변동 (매치 직후 사기) vs 장기 추세 (시즌 만족도) 가 자연스럽게 다른 트리거.
- **Q7 핵심만 V0.5**: 핵심 트리거 + 임계점 분기 + Hidden 연동 + 라커룸 분위기. 멘토링 / 멘트 세분화 등 정교화는 V1.0.

**영향 범위:** `PlayerState` 신규 필드 / `MatchPostProcessor` 사기 갱신 단계 (V0.1 미구현 → V0.5 본격) / `DailyProcessor` Morale 회복 단계 / 신규 `MoraleSystem.cs` (Application) / `algorithms.md` #8 신규 작성.

### V0.5+ 보완 포인트 (V1.0)

- **인터뷰 사고** — Happiness 40-59 + Hidden controversy 높음 → 미디어 인터뷰에서 부정 발언 자동 생성. V1.0 미디어 시스템과 짝.
- **선수 면담 멘트 세분화** — V0.5 = 4-6 옵션. V1.0 = ~20 옵션 + 효과 분기 정교화.
- **그룹 사기 (Cliques)** — 라커룸 파벌 / 같은 국적 / 같은 연령대 그룹 영향. V1.0.

---

## 43. Promise 시스템 + 면담 (V0.5)

**결정:** FM 표준 약속 시스템 4종 V0.5 도입. 면담 시스템 단순 도입 (4-6 옵션).

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

**면담 시스템 (V0.5 단순):**
- 유저 → PlayerProfile → [면담] 버튼 → 4-6 사전 정의 멘트:
  - "출전시간 보장하겠다" (PlaytimeAgreement Promise 생성)
  - "다음 시즌 새 계약 협상하자" (Renewal Promise)
  - "현재 성과 칭찬" (즉시 Morale +5)
  - "더 노력해야 한다" (Morale -3, professionalism 높으면 -1)
- 효과는 hidden (loyalty / ambition / professionalism) 에 따라 다름.

**이유:**
- **사용자 피드백 2.6**: "약속된 출전시간에 비해 적게 출전하면 불만" 직역. Promise 의 PlaytimeAgreement 가 핵심.
- **FM 표준**: Promise 시스템이 FM 메인 시스템 중 하나. 게임플레이 핵심 의사결정 추가.
- **Q7 핵심만**: 4종 Promise + 면담 4-6 멘트로 단순화. V1.0 멘트 세분화 / 더 많은 Promise 종류.

**영향 범위:** `Promise.cs` 신규 / `PromiseSystem.cs` (Application) 신규 / `GameState.activePromises / nextPromiseId` / `DailyProcessor` 통합 / `event-bus-catalog.md` `PromiseCreatedEvent / PromiseFulfilledEvent / PromiseBrokenEvent` 신규 / UI `PromiseInboxScene` 또는 Dashboard 인박스.

### V0.5+ 보완 포인트 (V1.0)

- **Promise 종류 확장** — FM 표준 약속들 (구장 확장 / 컵 우승 / 유럽 진출 / 슈퍼스타 영입 등). 보드 약속과 묶음.
- **면담 멘트 ~20 + 사전 시뮬레이션** — 멘트 선택 전 효과 예상 표시.
- **Promise 진행도 표시** — Dashboard 에 진행률 % (출전시간 약속 = 현재 35%, 목표 50%).

---

## 44. 매치 엔진 V0.5 — 5-Zone Markov 이벤트 시퀀스 (#34 실현)

**결정:** `#34` V0.5+ 진화 경로 실현. **초안 "분 단위 양 팀 독립 추첨" → openfootmanager(OFM) 5-zone Markov 상태 전이 모델로 전면 재설계** (2026-05-27). 인터페이스 (`Simulate(match, state, balance) → MatchResult`) 유지 / 내부 상태 머신 교체.

**왜 재설계했나 (초안의 한계):**
- 초안 (I.1/I.2 머지본 — PR #316/#318) 은 "매 분 양 팀이 동시에 독립적으로 이벤트 추첨" — 축구 흐름(앞 상황이 뒤에 영향)이 없음. 양 팀이 매 분 동시에 계속 슛 시도하는 비현실적 구조.
- #34 가 V0.5 진화 이유로 "앞 이벤트가 뒤에 영향" 을 들었으나, 초안 구현은 누적 score/card 가 다음 분 _확률_ 에 영향 X (assist 추적만).
- OFM 코드 분석 → ball 위치(zone) + 점유(possession) 상태 전이가 자연 흐름 생성. 점유 우세 → 공격 기회 ↑ / 슛 후 점유 전환 / 수적 우위 등.

**5-zone Markov 구조 (`algorithms.md` V0.5-2 상세):**
```
상태: ballZone {HomeBox/HomeDefense/Midfield/AwayDefense/AwayBox} + possession {Home/Away}
매 분: possessionTicks++ → fatigue 증가 → 1~3 ResolveAction(zone 분기) → possession contest
ResolveAction: Buildup → Midfield → AttackingThird → Shot (ball 한 zone씩 전진 / 실패 시 턴오버)
success = attEff / (attEff + defEff)
```

**핵심 결정:**
1. **Forward simulation** — 결과 미리 산출(#17 V0.1) **완전 폐기**. 결정성은 시드 고정(`match.id ^ randomSeed`)에서만. 같은 시드 + 같은 입력 state → 같은 시퀀스.
2. **49 stat zone별 매핑** — Buildup(`passing+vision+composure+teamwork`) / Midfield(`dribbling+passing+vision+teamwork` vs `tackling+positioning+decisions+teamwork`) / AttackingThird(`dribbling+pace+agility+composure` vs `marking+tackling+positioning+heading`) / Shot(`finishing+composure+decisions` vs GK `handling+reflexes+positioning`). OFM 18 stat → FM 49 매핑으로 stat 활용도 대폭 ↑.
3. **fatigue 임계 (#54)** — OFM 선형 `condition/100` 대신 임계. fatigue > 50 경기력 ↓ / > 40 부상률 ↑ (과도 로테이션 방지).
4. **활성 / 비활성 동일 엔진 (#55)** — background 도 동일 Markov. `collectEvents` 플래그로 텍스트(`Match.events`)만 분기. 통계(점유율/슛/패스/카드)는 양쪽 수집.
5. **부상 / 카드 / 페널티** — Tackle → maybeFoul → box면 penalty(`penaltyProbability`) / 2옐로 퇴장 / Injury + `PlayerInjuredEvent`.
6. **세트피스 별도 (I.10)** — Corner/FreeKick/Penalty 는 트리거만, `SetPieceResolver` 가 `corners`/`freeKickTaking`/`penaltyTaking`/`longThrows` 반영.
7. **연장 / 승부차기 (I.11, #56)** — MatchPhase 확장. 컵 매치 동점 → ExtraTime → PenaltyShootout. 컵 대회 자체는 Stage Q 신규.
8. **strengthExponent 폐기 (I.9)** — SimulateLite 도 Markov 라 V0.5 어디에도 미사용.
9. **Mentality (J.3) + Trait (C.1) 합류** — OFM `play_style_modifier` / `trait_bonus` 자리에 우리 Mentality 7단계 + TraitSO.effects.

**이유:**
- 사용자 통찰 "앞 상황 영향 = Markov 우월" + "기왕 하는 거 컵/연장까지" (2026-05-27). OFM 코드 분석으로 검증 (`engine/live_match/`).
- I.3~I.9 진입 _전_ 이 구조 교체 적기 — 후속 task (부상누적/평점/텍스트/교체/외부영향) 가 모두 이 매 분 구조 위에 쌓임.

**영향 범위:** `MatchSimulator` 5-zone 재작성 (I.1/I.2 코드 교체 — 이벤트종류/stat공식/외부화 분모는 재활용) / `MatchEvent` 종류 확장 (Corner/FreeKick/Penalty*/Dribble/Clearance 등) / `SetPieceResolver` (I.10) 신규 / 연장·승부차기 (I.11) / `SubstitutionAI` (I.6) / Stage Q 컵 대회 신규 / `algorithms.md` V0.5-2 재작성 / `v0.5-tasks.md` Stage I 재구성.

### V0.5+ 보완 포인트 (V1.0)

- **15-zone 정밀화** — OFM legacy 처럼 zone 세분화 + transition matrix (현 5-zone → 15-zone).
- **Team Instructions** (#45 V1.0) — Tempo / Pressing / Line / Width 이벤트 가중.
- **유저 코칭 인터럽트** — 전반 종료 / 중요 이벤트 시 외침 / 교체 (OFM `MatchCommand` 패턴).
- **xG / heatmap / 슈팅 위치** — 매치 통계 풍부화.
- **날씨 / 잔디 상태** — strength 보정 추가.

---

## 45. Tactic 시스템 V0.5 — 중간 스코프 (Q10)

**결정:** Formation + Mentality + 간단 Role (3-4/포지션) + Duty(A/S/D) + Set Pieces 담당자. Team Instructions 는 V1.0.

**Formation:**
- `FormationConfig` nested → `FormationSO` 추출 (`design-decisions.md` #32 실현).
- 카탈로그 5-6개: 4-4-2 / 4-3-3 / 3-5-2 / 4-2-3-1 / 4-4-1-1 / 5-3-2.
- `Club.tactic: Tactic` 신규 필드.

**Player Role + Duty:**
- 포지션별 3-4 Role 카탈로그 (총 ~40 Role) — 명세는 `v0.5-plan.md` §3.6.2.
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
- **Q10 중간 스코프**: 풀 FM (Role + Duty + Mentality + Team Instructions 모두) 은 매치 엔진 (#44) 작성 부담 ↑↑. Team Instructions 빼고 V0.5 → V1.0 정교화.
- **Role + Duty 핵심**: 같은 포지션 = 같은 행동이 단조. Poacher vs Target Forward 차이가 의사결정 재미.
- **Set Pieces 담당자**: 골 결정자 / 어시 통계에 영향. FM 표준.

**영향 범위:** `Tactic / TacticSlot / PlayerRoleSO / FormationSO / MentalitySO` 신규 / `Club.tactic` / 매치 엔진 (#44) Role 가중치 입력 / UI `LineupScene / TacticScene` 신규 / 가챠 (`StartingSquadGacha`) 포메이션 추첨 / `algorithms.md` #6 갱신.

### V0.5+ 보완 포인트 (V1.0)

- **Team Instructions** — Tempo / Passing / Pressing / Defensive Line / Width 5 옵션. 매치 엔진 가중치 입력.
- **다중 전술 슬롯** — 클럽별 3 전술 (기본 / 강팀 상대 / 약팀 상대) 슬롯. 매치 직전 자동 선택.
- **유저 수동 라인업 정교화** — 드래그앤드롭 UI.
- **Role 카탈로그 확장** — FM 표준 ~80 Role (현 V0.5 ~40).

---

## 46. 스카우트 시스템 V0.5 — 이분법 + 정성적 라벨 (Q4)

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
- 유저 수동 [스카우트 추가] — V1.0

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

### V0.5+ 보완 포인트 (V1.0)

- **개별 스카우트 인사 (Staff)** — `Staff.cs` 도메인 + 개별 스카우트 (국가 / 영역 전문성). 현 V0.5 = 시설 추상화.
- **스카우트 임무 (Assignment)** — 특정 국가 / 리그 / 포지션 스카우트 발주. V1.0.
- **유저 수동 스카우트 추가** — 검색 화면에서 명단에 직접 추가.
- **트레잇 노출 정확도** — 시설 등급별 트레잇 효과 정확 / 부정확.

---

## 47. CpuTransferAi V0.5 — 필요 기반 트리거 (Q3)

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

### V0.5+ 보완 포인트 (V1.0)

- **AI 협상 응답 의지** — 역제안 / 다중 라운드 (#48 V0.5 도입과 짝).
- **AI 매각 의향** — 약점 포지션 외 잉여 선수 매각. transferListed 자동 등록.
- **AI 임대 활용** — 영입 가능한 자금 X 시 임대로 대안 (#48 임대 시스템과 짝).
- **AI 클럽별 성향** — 명성 / 자금 / 보드 야망 따라 보수적 / 공격적 영입 차이. 신규 도메인 (`Club.aiPersonality`).
- **구단별 비동기 영입 타이밍** — 현재 V0.5 = "매주 월요일 모든 AI 구단 동시 호출" → V1.0 = 각 구단 독립 cooldown (`club.lastTransferAttemptDate` + `aiAttemptCooldownDays`). 클럽 성향·자금·약점 강도에 따라 다음 시도 시점 자체가 다름. `aiPersonality` 와 짝 (공격적 클럽 = 짧은 cooldown). 결정성 유지: 시드 = `randomSeed ^ club.id ^ lastAttemptDate.Ticks` (currentDate 대신 lastAttempt → 비동기에도 재현성). `DailyProcessor` 매일 호출하되 클럽별 cooldown 체크로 실제 처리 클럽 선별.
- **다중 오퍼 동시 지정** — 현재 V0.5 `DetectTrigger` = 우선순위 1개만 리턴 → 클럽당 주 1오퍼. **여름 윈도우 대규모 리빌딩 (신임 감독 / 강등 후 재건) 시나리오 X**. V1.0 = `DetectTriggers` (복수) — 우선순위 정렬된 트리거 리스트 + 자금 안에서 가능한 만큼 동시 오퍼. 자금 분산 정책: `affordableMax = money × aiBudgetRatio` 를 트리거별 분배 (예: 균등 / 우선순위 가중 / 시장가 비례). 위 비동기 타이밍과 함께 도입 — 두 기능 결합 시 FM 식 자연스러운 시장 움직임 (동시 다발 협상 + 클럽별 페이스).

---

## 48. 협상 V0.5 — CounterOffer + 선수 협상 + 임대 (Q7 핵심)

**결정:** V0.1 단일 라운드 → V0.5 다중 라운드 + 선수 개인 협상 + 임대 시스템 + release clause 활성화.

**CounterOffer (역제안):**
- `OfferStatus.CounterOffer` 신규 enum 값.
- AI 응답 분기 (V0.1 2 → V0.5 4):
  - ratio ≥ 1.30 → Accepted
  - 1.10 ≤ ratio < 1.30 → CounterOffer (시장가 ×1.30 역제안)
  - 0.85 ≤ ratio < 1.10 → Rejected
  - < 0.85 → Rejected + 사기 가산점 (-3 morale 보너스)
- 유저 응답: 수락 / 거절 / 재역제안 (최대 3 라운드 — `maxNegotiationRounds`).

**선수 개인 협상 (Negotiating):**
- AI 판매 구단 Accepted → `OfferStatus.Negotiating` (V0.1 자동 통과 → V0.5 단계).
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
- **사용자 피드백 2.5 + Q7**: 상시 재계약 + 사기 연동. 협상 V0.5 정교화는 사용자 피드백에 명시는 없으나 FM 표준 / 시장 메커닉 필수.
- **Hidden 연동 (#40)**: loyalty / ambition 가 선수 협상 핵심 입력.
- **Promise 자동 생성**: 출전시간 약속 옵션 → 사기 안정. PromiseSystem (#43) 연동.

**영향 범위:** `OfferStatus.CounterOffer / Negotiating` enum / `TransferOffer` 신규 필드 (isLoan / loan* / parentClubId) / `TransferSystem` 메서드 확장 (RenewContract / SubmitFreeAgentContract) / `algorithms.md` #3 V0.5 갱신 / UI `NegotiationScene` 신규.

### V0.5+ 보완 포인트 (V1.0)

- **에이전트 / 사이닝 보너스 / 충성 보너스 / 출전 보너스 / 골 보너스** — Contract 확장. V1.0.
- **다중 오퍼 경쟁 (Interest System)** — 같은 선수에 여러 클럽 관심. V1.0.
- **트랜스퍼 리스트 자동 거래** — 시장가 ×0.7 자동 할인. V0.5 활성화, V1.0 정교화.

---

## 49. 시설 시스템 V0.5 — 8종 × 10단계 + 병렬 + 비용 인상 (사용자 피드백 2.1)

**결정:** 시설 8종 확장 + 등급 1-10 세분화 + 병렬 업그레이드 + 비용 인상 + 효과 본격 도입.

**8종 (V0.1 3종 → V0.5 8종):**
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

**등급 1-10 세분화 (V0.1 1-5 → V0.5 1-10):**
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
- 등 (`v0.5-plan.md` §3.10.5).

**이유:**
- **사용자 피드백 2.1**: "시설 세분화 / 병렬 / 비용 인상 / 핵심 게임플레이 포인트" 직역.
- **유스 시설 분리 (`#35` V0.5+ + #50)**: 사용자 피드백 "청소년 코치 / 모집 시스템 분리" 직역 → 3분리 (YouthCoach / YouthRecruitment / YouthFacility).
- **시설 = 핵심 자원 의사결정**: 자금 → 어느 시설에 투자할지 = 시즌 운영 핵심 결정. FM 표준.

**영향 범위:** `FacilityType` enum 5 추가 / `Facilities` 도메인 8 필드 / `FacilitySystem` 병렬 업그레이드 / `FacilityLevelSO` 80 asset 신규 / 각 시설 효과 적용 (Training → 성장 시스템 / Medical → MatchPostProcessor 부상 회복 / 등) / UI `FacilityScene` 갱신.

### V0.5+ 보완 포인트 (V1.0)

- **시설 → Staff 도입** — Coach / Doctor / Scout 개별 인사. V1.0.
- **시설 등급 효과 곡선 다양화** — 일부는 선형, 일부는 임계점 (Lv5 = 1.5배, Lv10 = 2배).
- **시설 부작용** — 큰 업그레이드 = 시즌 중 매치 X (Stadium 공사 시 홈 어드밴티지 손실). V1.0+.

---

## 50. 유스 시스템 V0.5 — CA 캡 + 시설 분리 + 풀 전체 영입 + Mentoring (사용자 피드백 2.2)

**결정:** 사용자 피드백 3개 + Mentoring 신규.

**유스 CA 캡 ~100 (사용자 피드백):**
- V0.1: PlayerGenerator 호출 → CA 50-200 (명성 기반).
- V0.5: 유스 전용 분포 — `youthMinCa = 30 / youthMaxCa = 95`.
- PA 는 그대로 (PA 진실값 모델 #35) — 100-180.
- 16-18세 = CA 낮은 게 현실적. V0.1 너무 높은 CA 발생은 σ 과대 + 시드 충돌.

**풀 전체 영입 가능 (사용자 피드백):**
- 현재: subset 선택.
- 변경: UI 디폴트 "전체 영입" + 개별 선택 옵션.
- 영입 인원 제한 = `YouthRecruitment` 시설 등급. Lv1 → 풀 사이즈 ÷ 3, Lv10 → 풀 전체.

**유스 시설 분리 (사용자 피드백 + `#35` V0.5+):**
- V0.1 `Facilities.youthLevel: int` 단일 → V0.5 3분리 (#49).
- `youthCoachLevel` — 평균 PA + 트레잇 가중치.
- `youthRecruitmentLevel` — 풀 사이즈 + 인스펙션 빈도 (Lv7+ = 보조 인스펙션 추가).
- `youthFacilityLevel` — 유스 선수 성장률 + 1군 콜업 적응.

**Mentoring 시스템 (FM 표준 — V0.5 신규):**
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

**라운드별 포지션 가중치 (`algorithms.md` #4 V0.5+):**
- 균등 → 가중치 변동. 어떤 인스펙션은 GK 0, AT 다수.
- 외부화: `youthPositionWeightVolatility = 0.5`.

**미영입 후보 → AI 다른 구단 영입 (`algorithms.md` #4 V0.5+):**
- 일정 확률 (`youthRejectedToOtherClubRatio = 0.3`) 로 다른 구단 영입.
- `YouthSignedByOtherEvent` 발행.

**1군 콜업 자동 트리거 + 유저 승인 (Q9):**
- 자동: 18세 + CA ≥ 클럽 평균 70% → `YouthPromotionSuggestedEvent` 발행.
- 유저: Dashboard 인박스 → 클릭 → PlayerProfile [1군 승격] / [거절].

**이유:**
- **사용자 피드백 2.2 + 2.1 직역**: CA 캡 + 풀 전체 + 시설 분리.
- **Mentoring**: FM 표준 + 사용자 피드백 "충성도·의리 같은 수치로 누그러뜨림" 의 long-term 변화 메커닉. Hidden Attributes 의 동적 변화.
- **자동 + 승인 (Q9)**: 유저 관리 부담 ↓ + 통제 보존.

**영향 범위:** `Facilities` 8 필드 / `MentoringGroup.cs` 신규 / `Club.season.mentoringGroups` / `YouthSystem.cs` 갱신 (CA 캡 / 풀 전체) / `MentoringSystem.cs` 신규 / `algorithms.md` #4 V0.5 갱신 / UI `MentoringScene` 신규 / `YouthPromotionSuggestedEvent / YouthSignedByOtherEvent` 신규.

### V1.0 갱신 (Stage I, #524) — 수렴 모델 = 격차 비례

**변경 (사용자 피드백):** 고정 `±mentoringRateModifier`/월 수렴 폐기 → **격차 비례 수렴**.
- 월 스텝 = `clamp(round(|mentor−mentee| × mentoringConvergenceFraction), 1, min(rateCap, |gap|))`. 부호는 mentor 방향.
- `mentoringConvergenceFraction = 0.15` (신규), `mentoringRateModifier = 5` 는 이제 **상한(cap)** 으로 의미 전환.
- **차이 클수록 빠르고 멘토 수치에 가까울수록 느려짐**, 상한 = 멘토의 해당 수치 (초과 불가), 최소 1/월 (결국 도달).
- **이유:** 고정값은 "멘티가 항상 +5" 라 사기적이고 비현실적. 격차 비례 = FM식 체감 + 밸런스(저능력 멘티는 빠르게 따라잡고, 거의 따라잡으면 둔화).
- **대상 Hidden Attrs (V1.0 확정):** `professionalism / ambition / loyalty` 3종 (구 명세의 `determination` 은 도메인에 없음 — 정정).
- **UI (`MentoringScene`):** 멘토 단일선택 리스트(`MentorSelectItem`, 드롭다운 폐기) + 멘티 토글 + 멘티별 진행률 바 3개(`MenteeProgressRow`, 즉시 표시) + `+N/월`. 멘토 추천 = `MentorRecommender` (leadership + age + 계약 + Hidden 평균).
- **영향:** `MentoringSystem.cs` (ProjectedMonthlyStep/ConvergencePercent 공개 헬퍼) / `GameBalanceSO.mentoringConvergenceFraction` + Mentor 추천 4필드 / `MentorRecommender.cs` / `MentorSelectItem.cs` / `MenteeProgressRow.cs` 신규.

### V0.5+ 보완 포인트 (V1.0)

- **추가 스카우트 (data-flows #4 [3-c])** — 비용 차감 + 정보 정확도 ↑. V1.0.
- **계약 기간 차등** — 시설 / 나이 / PA 기반. V0.5 균등 → V1.0 차등.
- **AI 클럽 인스펙션** — V0.5 = 유저 클럽만. V1.0 다른 클럽도 인스펙션 + 영입 결정.

---

## 51. 시즌 시스템 V0.5 — 시상 + 보드 평가 + 재정 결산 + 매니저 평판 (`#38` V0.5+ 실현)

**결정:** V0.1 미구현 5종 본격 도입.

**시상 (V0.5 신규):**
- `SeasonAward.cs` + `AwardType` enum 7종: LeagueMVP / TopScorer / TopAssist / YoungPlayer / BestEleven / GoldenGlove / ManagerOfSeason.
- `SeasonEndProcessor` 계산 단계 추가.
- 수상 선수 morale +10 / happiness +10.

**월간 어워드:**
- 매월 1일 `DailyProcessor` 가 직전 월 통계 계산.
- Manager of the Month — boardConfidence +5.
- Player of the Month — 평점 + 골/어시 기반 / 사기 +10.

**보드 평가 / 경질:**
- `Club.season.boardConfidence` 변동 (V0.1 50 고정 → V0.5 본격):
  - 매월: (실제 순위 vs 목표 순위) × multiplier
  - 매치: 패배 -2, 빅매치 패배 -5, 승리 +1
  - 보드 약속 미이행 -20
- < 30 → 경질 경고. < 10 → 경질 (V0.5 = Game Over).

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
- V0.5: 효과 = boardConfidence 가산. V1.0 다른 구단 부임 / 미디어 / 국대.

**시즌 통계 저장 (사용자 피드백 2.8):**
- `Player.career: List<SeasonStat>` (V0.1 정의됨, 미사용) 채움.
- 시즌 종료 시 각 선수 그 시즌 통계 → `career` 에 추가.
- `League.history: List<SeasonHistory>` 신규 — 시즌별 순위 / 시상 보존.

**Match 데이터 압축 (`#8` 실현):**
- 시즌 종료 시 직전 시즌 외 Match `events / playerStats` 비움. 우승 / 강등 / 시상만 보존.

**이유:**
- **`#38` V0.5+ 실현**: 시즌 시스템의 의사결정 깊이 = 시상 / 보드 / 재정 정산.
- **사용자 피드백 2.8**: 리그 시즌 통계 저장 직역.
- **보드 평가**: FM 매니저 게임 핵심 — 시즌 운영의 동기 (경질 회피 / 보드 신뢰 ↑).

**영향 범위:** `SeasonAward / BoardPromise / SeasonHistory` 신규 / `Club.season.boardConfidence / boardPromises / captainPlayerId` / `GameState.managerReputation / activeAwards` / `SeasonEndProcessor` 단계 추가 (5종) / `DailyProcessor` 월간 어워드 단계 / UI `SeasonSummaryScene` 신규 / `algorithms.md` #11 시상 알고리즘 / event-bus `AwardWonEvent / BoardConfidenceChangedEvent / ManagerSackedEvent` 신규.

### V0.5+ 보완 포인트 (V1.0)

- **다른 구단 부임 (경질 후)** — Game Over 대신 다른 구단 오퍼. V1.0.
- **재정 정교화** — 스폰서십 / 광고 보드 / 부채 / 대출. V1.0.
- **사기 / 모랄 정산** — 우승팀 +, 강등팀 -, 약속 출전시간 미달자. V1.0.
- **보드 본격 인터랙션** — 예산 요청 / 비전 / 야망. V1.0.

---

## 52. 인프라 V0.5 — String Table + Localization + Save Migration (사용자 피드백 2.11)

**결정:** 3종 인프라 V0.5 도입.

**String Table (사용자 피드백):**
- 현재: UI 한글 직박 ("리롤" / "확정" / "다음 경기").
- 변경: `LocalizationSystem` 키 기반 조회.
- 데이터: `LocalizationSO` (CSV / JSON 임포트 가능).
- API: `Localization.Get(key, args)` static.
- 매치 텍스트 이벤트 (#44) 의 `textKey / textArgs` 도 같은 시스템.
- 마이그레이션: 기존 UI 코드 한글 → key 추출 (Stage A.3).

**Localization (사용자 피드백):**
- 영어 + 한국어 (V0.5 2 언어).
- `LocalizationSystem.CurrentLanguage: Language` enum.
- 게임 시작 시 시스템 언어 감지 / 옵션 변경.
- 폰트 — NotoSansKR 유지 (한·영 둘 다).
- V1.0: 일본어 / 중국어 / 스페인어.

**Save Migration (사용자 피드백 + Q8):**
- `GameState.saveVersion: int` 신규 (디폴트 2 = V0.5, V0.1 = 1).
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
- **V0.1 → V0.5 마이그레이션 = Q8 결정: 미지원** (V0.5 신규게임만). 단 인프라는 도입 (V0.5 → V1.0 등 후속 대비).

**자동 저장 (`data-flows.md` TBD):**
- 시즌 종료 시 자동 — `SeasonEndProcessor` 가 `SaveSystem.Save(state, "autosave_season_{year}")` 호출.
- 옵션: 매일 자동 (Dashboard 설정).
- 슬롯명: `autosave_001 ~ autosave_005` 순환 (5슬롯).

**이유:**
- **사용자 피드백 2.11 직역**: String Table + 영/한 + Save Migration.
- **Q8 결정**: V0.5 = 큰 재구조화. 마이그레이션 가치 < 비용. 단 SaveMigration 인프라는 V1.0+ 대비 필수.

**영향 범위:** `LocalizationSystem.cs / LocalizationSO` 신규 / 기존 UI 코드 ~11 씬 전수 한글 → key 추출 / `SaveSystem.Save / Load` 에 saveVersion 처리 / `SaveMigration.cs` 골격 / `GameState.saveVersion` 필드 / `coding-conventions.md` Localization 패턴 추가.

### V0.5+ 보완 포인트 (V1.0)

- **추가 언어** — 일본어 / 중국어 / 스페인어. V1.0.
- **Save 파일 압축 (gzip)** — 크기 ↓ ~50%. V1.0.
- **자동 저장 정교화** — 매일 / 시간별 / 매치 후 옵션. V1.0.
- **클라우드 동기화** — Steam Cloud 등. V1.0+.

---

## 53. 시설 효과 본격 적용 — Training + Medical + Gym (V0.5 D.4)

**결정:** V0.5 D.4 에서 3 시설 효과 본격 도입 (`algorithms.md` V0.5-10 + V0.5-11). Stadium / Scout / Youth* 은 후속 Stage (M.6 / E.2 / L.1-3) 의존.

**Training — Player Growth System (V0.5-10):**
- 매월 1일 `GrowthSystem.Tick(state, balance)` 호출 (V0.1 ProcessSchedule 패턴 일관).
- 1군 선수 대상 — Relative stats 만 변동 (Absolute = ×0.10 페널티).
- **2단계 모델**: (a) 발생 확률 = `growthBaseChance (0.01) × ageFactor × absoluteFactor × trainingBonus × gymBonus(피지컬) × paFactor`. (b) 발생 시 size 추첨 = `[+1, +2, +3]` 분포 `[75, 20, 5]` (peak youth = `[60, 30, 10]`).
- Training Lv N → `1 + N × 0.10` (Lv1 ×1.1, Lv10 ×2.0).
- decline (ageFactor < 0) 대칭 — `-1 / -2 / -3` 같은 분포.
- 결정성 — 시드 = `state.randomSeed ^ player.id ^ (year×12 + month)`.

**Medical — Injury Recovery + Rate (V0.5-11):**
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
- CA = static field (generation 시점 고정). V1.0 = derived from stats 검토 (`#24` V0.5+ 보완 포인트).

**임대 선수 (Stage K.3 Loan):**
- 현재 소속 (`currentClubId`) 클럽의 시설 영향. 원 소속 (`parentClubId`) X.
- 부상 회복 도중 임대 이동 — `expectedReturn` 고정 (V0.5). V1.0 재계산.

**Stadium / Scout / Youth* — D.4 책임 X:**
- **Stadium** → Stage M.6 (SeasonEndProcessor 재정 결산 시 `baseStadiumIncome × stadiumLevel × clubReputation × homeMatches`)
- **Scout** → Stage E.2 (ScoutingSystem 명단 크기 / 정확도 — `FacilityLevelSO(Scout).scoutPoolSize / scoutAccuracyRange` 활용)
- **YouthCoach / YouthRecruitment / YouthFacility** → Stage L.1-3 (유스 PA / 풀 크기 / 성장률)

**이유:**
- **D.4 스코프 한정 — 직접 효과 3 시설**: Stadium / Scout / Youth* 은 시즌 / 검색 / 유스 시스템의 일부라 해당 Stage 가 책임. D.4 에서 다 처리하면 후속 Stage 와 중복 + PR 사이즈 폭증.
- **성장 시스템 = 신규 시스템**: V0.1 = 선수 stat 시즌 내내 고정. V0.5 = 매월 변동 도입 (V0.5 의 핵심 시뮬레이션 깊이 추가).
- **결정성 보존**: V0.1 #17 시드 모델 일관 — 같은 시드 = 같은 성장 시퀀스.

**영향 범위:**
- `Application/GrowthSystem.cs` 신규 (Stateless)
- `Application/InjurySystem.cs` 신규 (Stateless — `ComputeRecoveryDays / ComputeInjuryRate / ProcessRecovery`)
- `DailyProcessor` 통합 — 매월 1일 `GrowthSystem.Tick` + 매일 `InjurySystem.ProcessRecovery`
- `GameBalanceSO` 신규 ~10 필드 (`growthBaseChance / growthAbsoluteFactor / growthTrainingCoeff / growthGymCoeff / growthPaGapNormalizer / growthYouthFactor / growthYouthPeakAge / growthPrimePeakAge / growthDeclineStartAge / injuryMedicalRecoveryCoeff / injuryGymRecoveryCoeff / injuryMedicalRateCoeff`)
- `Utils/StatMetadata.cs` — `IsPhysical(stat)` 메서드 추가 (피지컬 8 stat 판별, B.4 와 짝)
- `event-bus-catalog.md` — `PlayerStatChangedEvent` (V1.0 UI 알림 용도, V0.5 = 도메인 이벤트만) / `PlayerInjuryRecoveredEvent` 신규 등록

### V0.5+ 보완 포인트 (V1.0)

- **개인 훈련 (Individual Training)** — 유저가 특정 선수 / stat 집중 훈련 (FM 표준).
- **시즌 외 프리시즌 캠프** — 6/1~8/15 추가 성장 (현 V0.5 = 매월 동일).
- **부상 중 성장** — 영향 X → ×0.5 검토.
- **Mentoring stat 영향** — Stage L.4 = Hidden 만. V1.0 = stat 도 일부.
- **부상 multi-phase** — 회복 / 재활 / 컨디션 회복 단계.
- **CA derived from stats** — V0.1 #24 V0.5+ 보완 포인트 일관.
- **시설 → Staff 도입** (`#49` V1.0) 시 코치 quality 추가 입력.

---

## 54. 매치 fatigue 임계 모델 (V0.5 — OFM 선형 대체)

**결정:** OFM 의 선형 `effective_overall = overall × (condition/100)` 대신 **임계 기반** fatigue 영향. 사용자 결정 (2026-05-27).

```
fatigue ≤ 50              → 경기력 보정 없음 (perf = 1.0)
fatigue > 50             → 경기력 ↓ (1점당 -1%, floor 0.6)
fatigue > 40             → 부상 발생률 × 1.5
```
(우리 `PlayerState.fatigue`: 0 = 최상, 100 = 완전 피로. OFM condition = 100 - fatigue 관점)

**외부화:** `fatiguePerfThreshold(50)` / `fatiguePerfFloor(0.6)` / `fatiguePerfPenaltyPerPoint(0.01)` / `fatigueInjuryThreshold(40)` / `fatigueInjuryMultiplier(1.5)`.

**이유:**
- **과도 로테이션 방지** (사용자 핵심 의도) — OFM 처럼 100부터 선형 감소하면 매니저가 항상 풀 컨디션만 쓰려고 과도 로테이션. 실제 FM 도 컨디션 ~50%(우리 fatigue 50)까진 경기력 영향 미미.
- **임계 분리** — 부상(fatigue>40)과 경기력(fatigue>50)을 다른 임계로 — "조금 피곤하면 부상 위험만, 많이 피곤하면 경기력까지" 자연스러운 단계.
- form / morale / dressingRoomMood / homeAdvantage 와 함께 곱셈 합류 (I.8).

### V0.5+ 보완 포인트
- **회복 곡선** — 시설(Medical/Gym) + 나이 + Natural Fitness 에 따라 fatigue 회복 속도 차등 (현재 `fatigueRecoveryPerDay` 균등).
- **부상 multi-phase** — fatigue 누적이 장기 부상(`fitness`)으로 전이 (#53 / OFM `fitness` 필드).

---

## 55. 매치 엔진 5-Zone 모델 + Background 동일 엔진 (V0.5)

**결정:** 매치 엔진은 OFM 5-zone Markov 채택 (#44). **활성 / 비활성(background) 매치 동일 엔진** 사용 — `collectEvents` 플래그로 텍스트 로그만 분기. 통계는 양쪽 수집. 사용자 결정 (2026-05-27).

**5-zone:** `HomeBox / HomeDefense / Midfield / AwayDefense / AwayBox`. ball 이 한 zone 씩 전진(성공)/후퇴·턴오버(실패). 점유 contest 로 possession 전환.

**Background 정책:**
- 별도 Poisson 경량 경로 (초안 SimulateLite) **폐기** — Markov 통일.
- `collectEvents = false` → `Match.events` 텍스트 로그만 생략. 점유율/슛/패스/카드/평점 통계는 수집 (사용자 요구: "다른 팀 경기도 통계 다 확인").
- `MatchPostProcessor.Process(..., publishEvent: false)` — UI 갱신 비용 ↓.

**이유:**
- **연산 부담 0** (검증) — 매치 ~9K 산술, 1 라운드 10매치 < 1ms. 단일 리그 V0.5 에서 full Markov 도 문제 없음. 다중 리그 V1.0 도 ~수 ms.
- **코드 일관성** — 한 엔진, 플래그 분기. 두 코드 경로 유지보수 부담 제거. OFM 도 instant/live 모드가 동일 core resolution 공유.
- **통계 완전 정확** — 비활성 매치도 점유율/슛 등 정확 (Poisson 근사보다 우월).

### V0.5+ 보완 포인트
- **다중 리그 대규모** (V1.0) — 라운드당 매치 수 ↑ 시 경량 모드 (action 1개 고정) 옵션 검토.
- **15-zone 정밀화** — zone 세분화 + transition matrix.

---

## 56. 컵 대회 + 연장 / 승부차기 (V0.5 — 스코프 확대)

**결정:** 원래 V1.0 였던 컵 대회 + 연장전 + 승부차기를 V0.5 으로 끌어옴. 사용자 결정 (2026-05-27, "기왕 하는 거 추가").

**분리:**
- **연장 / 승부차기 (I.11)** — 매치 엔진(#44) 내부. `MatchPhase` 확장 (ExtraTimeFirstHalf/HalfTime/SecondHalf/End + PenaltyShootout). 컵 매치 동점 시 발동 (`match.type` 분기 + `allowsExtraTime`).
- **컵 대회 (Stage Q 신규)** — 대진표 / 녹아웃 / 스케줄 / 시드 배정. 매치 엔진(I.11) + 시즌 시스템(M) 둘 다 의존. I.11 선행 필요.

**연장:** 91~105 + 106~120 (각 stoppage). 여전히 동점 → 승부차기 (`penaltyShootoutRounds(5)` 교대 → sudden death). 각 킥 `penaltyTaking vs GK reflexes×handling`.

**리그 매치:** `allowsExtraTime = false` → FullTime 종료 (무승부 허용). 기존 V0.5 리그 영향 없음.

**이유:**
- 사용자: 탄탄한 게임 지향 — 컵 대회는 시즌 깊이 + 회고 가치 (FA컵 우승 등).
- 매치 엔진 5-zone 재작성 _하는 김에_ MatchPhase 확장 = 한계비용 낮음.

### V0.5+ 보완 포인트
- **다중 컵** (리그컵 + FA컵) — V0.5 단일 컵 → 여러 대회.
- **유럽 대회** (챔피언스리그 류) — V1.0 다중 리그와 짝.
- **2-leg 녹아웃** (홈 앤 어웨이 합산) — V0.5 단판 → 합산 방식.

---

## 57. TacticImpact — 이벤트 "주체 선택" 가중치 (V0.5 J.4)

**결정:** `TacticImpact.ComputeEventWeight` 는 매치 이벤트의 **주체 선수 선택** (`MatchSimulator.SnapPlayer`) 에서 같은 팀 같은 라인 후보 간 **상대 가중치** 만 산출한다. `roleWeight × dutyWeight × statWeight` 3요소. Mentality / 외부 영향 (form·morale·fatigue·mood) 은 **미포함**.

**왜 selection-weighting 인가 (frequency 아님):**
- 5-zone Markov 엔진 (#44) 은 이미 *이벤트 발생 빈도* 를 자체 결정 (zone 전이 + success ratio). TacticImpact 가 추가로 빈도를 곱하면 엔진 구조와 충돌.
- J.4 가 채우는 빈틈은 "어느 선수가 그 이벤트의 주체인가" — Poacher 가 Target Man 보다 슛을 더 자주 잡는다. 이건 후보 풀 내 상대 가중치. → `SnapPlayer` 의 균등 추첨을 가중 추첨으로 교체.

**왜 Mentality / 외부영향 제외 (double-counting 방지):**
- **Mentality**: 팀 전체 곱셈 → 같은 팀 후보 선택에서 *상수로 상쇄* (선택 확률 불변). 게다가 J.3 (`MentalityShotMult`/`PressMult`/`KeyPassMult`) 가 zone 전이 빈도에 이미 적용. 둘 다 → 선택식에 넣을 이유 없음 + 넣으면 빈도 중복.
- **외부영향 (form/morale/fatigue/mood)**: `MatchSimulator.Eff()` 가 성공률(rating)에 이미 곱셈 적용. 선택식에도 넣으면 지친 선수가 "덜 뽑히고 + 덜 성공" 2중 페널티.
- `stat` 은 예외로 포함 — 선택(주체)과 성공(Eff)은 다른 축이고, 명사수가 *더 자주 + 더 정확히* 슛하는 건 현실적 (해로운 중복 아님). 스펙도 `statWeight` 명시.

**매직넘버 외부화 (#11):** Duty 가중치 (`tacticDutyPrimaryWeight=1.5` / `tacticDutySecondaryWeight=1.0` / `tacticDutyOffWeight=0.5` / `tacticDutyKeyPassSupportWeight=1.3`) 는 `GameBalanceSO` 외부화 → `ComputeEventWeight(..., balance)` 시그니처 (원 스펙과 일치). statWeight 분모(10000)만 구조적 상수 (`MatchSimulator` stat 조합 `/4.0` 와 동일 — 선택은 상대값이라 스케일이 결과에 무관). Role 보정 = `PlayerRoleSO.eventModifiers`, Mentality = `GameBalanceSO.mentalityShotMultiplier` (J.3) — 이미 SO. 시드 영향: production 은 `HasLineup` 가 J.5 까지 false 라 duty 경로 미실행 → 시드 재생성 전이라도 무해 (테스트는 fresh `CreateInstance` 라 initializer 값 사용).

**assignedPlayerId 의존 (J.5 선행):** ComputeEventWeight 는 `slot.assignedPlayerId == playerId` 로 Role/Duty 조회. J.2 디폴트 Tactic 은 전부 `-1` (미배정). → `SnapPlayer` 에 `HasLineup` 가드: 배정된 슬롯이 하나도 없으면 균등 추첨 (기존 동작, **T1~T12 회귀 0**). **J.5 LineupScene** 에서 선수↔슬롯 배정 후 본격 작동. J.4 단계에서 MatchSimulator auto-assign 은 **도입 안 함** — J.5 자동 라인업 책임과 중복 회피 (단순성).

**검증 — 가중치 비율 (unit) vs emergent 카운트:** ComputeEventWeight 가중치 비율은 정확히 role×duty 비율 (T1 2.0 / T4 3.0). 그러나 5-zone 매치의 emergent 슛 *카운트* 비율은 점유/zone 동학으로 증폭됨 (실측 ~3.3 / ~5.7). 따라서 T1/T4 는 **가중치 비율** 을 정밀 검증 (스펙의 "~2×/~3×" 의 실체) + 별도 통합 테스트는 **방향성** (Poacher 슛 > Target Man) 만 검증. emergent 정확 비율은 비검증 (엔진 동학 특성).

**영향 범위:** `Application/TacticImpact.cs` 신규 / `MatchSimulator.SnapPlayer` 가중 추첨 + `HasLineup` (ResolveShot 슈터·ResolveMidfield/AttackingThird 공격수·수비수에 eventType 전달) / `GameBalanceSO` tacticDuty* 4필드 / `MatchSimulatorTests` T1+T4+통합 / `algorithms.md` V0.5-7.

### V0.5+ 보완 포인트
- **자동 라인업 (J.5)** — Role 호환 + top CA + 폼/사기 가산 + 부상/정지 제외 자동 배정. 그 후 TacticImpact 가 모든 매치에서 활성.
- **cross 카테고리** — 윙어 `cross` 보정 (시드에 이미 존재) 은 5-zone 에 독립 cross resolution 단계 부재로 V0.5 휴면. zone 세분화 시 활성.
- **Mentality 단일 파이프라인** — zone 전이(J.3) 와 선택(J.4) 으로 분산된 Tactic 영향을 단일 가중치 경로로 통합 검토.

---

## 58. UI 글로벌 네비게이션 — TopBar + SideBar 영구 레이어 (V1.0)

**결정:** 모든 컨텐츠 씬 (Dashboard / Squad / Tactic / ... 11+) 에 **단일 prefab (`GlobalNavPrefab`)** 이 자동 주입. TopBar (80px) + SideBar (200px) 영구 표시. 뒤로가기 / 저장 / 옵션 / 인박스 / 메인 메뉴는 항상 고정 위치.

**이유:**
- **사용자 핵심 피드백 (2026-05-29)**: "씬이 전환되더라도 저런 기본적인 버튼들 (뒤로가기, 저장, 옵션 등)은 고정된 위치에 있도록 해야해." V0.5 에서 씬마다 버튼 위치/형태 제각각 → V1.0 일관성.
- **FM 시리즈 표준** — FM26 도 동일 패턴 (TopBar + SideBar). 사용자 친숙.
- **씬 간 우회 제거** — V0.5 는 저장 → Dashboard → 다른 씬으로 우회. V1.0 은 어디서든 TopBar [저장] 으로 직접.
- **단일 prefab 관리** — UI 일관성 변경 시 한 곳만 수정.

**구조 (`v1.0-plan.md §3.19` 참조):**
- TopBar 좌측: [뒤로] [날짜] [자금] [토큰] [인박스 + 배지]
- TopBar 우측: [옵션] [저장] [홈]
- SideBar: 9 메인 메뉴 (대시보드 / 스쿼드 / 전술 / 라인업 / 이적 / 일정 / 순위 / 시설 / 유스 / 멘토링)
- 인박스 클릭 = 우측 슬라이드 패널 (어디서든 동일 동작)

**예외 (GlobalNav 없는 씬):**
- MainMenu / ClubSelect / Gacha — 게임 시작 흐름. 자체 메뉴.
- SeasonSummary / MatchText / Options — TopBar 만 (SideBar 없음, 특수 흐름).

**영향 범위:**
- `UI/GlobalNavController.cs` 신규 (씬별 인스턴스 — 생명주기 항목 참조)
- `Assets/Imported/FMLite UI/Prefabs/GlobalNavPrefab.prefab` 신규
- 11+ 컨텐츠 씬에 prefab baked-in (씬마다 인스턴스 포함)
- 각 씬 컨트롤러의 자체 [뒤로] / [저장] 버튼 제거 (중복 회피)
- `DashboardController.savePanel` → `GlobalSavePanel` (모달, 한 곳)

### 생명주기 모델 — 씬별 인스턴스 (2026-06-01 정정)

**초안 모순:** 초안은 "싱글톤 + DontDestroyOnLoad" (영구 1개) 와 "11+ 모든 씬에 prefab baked-in" (씬마다 1개) 을 동시에 적었으나 두 모델은 양립 불가. baked-in + DDOL 이면 씬 전환마다 중복 생성 → Awake guard 로 파괴는 되나, 제외 씬 (MainMenu / Gacha / ClubSelect) 진입 시 영구 nav 를 별도 Hide/Destroy 하는 정리 로직이 필요해짐. (`GameManager` / `SoundManager` 의 DDOL 싱글톤은 "모든 씬 baked-in" 이 아니라 부트스트랩 1회 생성이라 이 문제가 없음 — 패턴이 다름.)

**결정 (사용자, 2026-06-01): 씬별 인스턴스 (DontDestroyOnLoad 없음).**
- 각 컨텐츠 씬에 `GlobalNavPrefab` baked-in. 씬 로드마다 새로 생성/소멸.
- `GlobalNavController.Instance` 는 "현재 씬의 nav" 접근자 (Awake 에서 set, OnDestroy 에서 clear). DontDestroyOnLoad 안 함. 중복 파괴 guard 불필요 (씬당 1개).
- **데이터는 영구 보관 X** — nav 자체는 상태 없음. 매 씬 진입 시 `Start` 에서 `GameManager.Instance.State` 를 읽어 TopBar (날짜 / 자금 / 토큰 / 인박스 배지) 갱신. 진실의 원천은 이미 DDOL 인 `GameManager`.
- **제외 씬은 prefab 을 안 넣음** → 정리 로직 0. MainMenu / Gacha / ClubSelect 는 nav 자체가 없음. SeasonSummary / MatchText / Options 는 TopBar 만 있는 변형 (또는 prefab 의 SideBar 비활성).

**이유:**
- **단순/견고** — 씬당 1개라 중복 guard·제외 씬 Hide 로직 모두 불필요. baked-in 이 곧 "그 씬에 nav 가 있다" 의 단일 표현.
- **상태 비보유 일관** (`#3` Stateless 정신) — nav 는 GameManager 를 읽어 표시만. 영구 객체가 들고 다닐 가변 상태 없음.
- **트레이드오프** — 씬 전환 시 nav 가 재생성 (미세 flicker). V1.0 은 정적 UI (애니메이션 X) 라 체감 거의 없음. 진짜 끊김 없는 영구 레이어는 V1.x DOTween 도입 시 재검토.

### V1.x 보완 포인트
- **DOTween 슬라이드 애니메이션** (인박스 패널 / 모달 등) — V1.0 정적, V1.x 도입.
- **영구 레이어 재검토** — 씬 전환 재생성 flicker 가 거슬리면 단일 DDOL 인스턴스 + sceneLoaded 갱신 모델로 전환 (이때 제외 씬 Hide 로직 동반).
- **테마 토글** — UI Manager 가 다크 ↔ 라이트 전환 (V1.x).

---

## 59. Options 시스템 — PlayerPrefs + AudioMixer + 4 카테고리 (V1.0)

**결정:** OptionsScene 신규. 사운드 / 언어 / 통화 / UI Scale / 자동 저장 / 단축키 안내. 모든 값 PlayerPrefs 저장 (GameState 외부).

**왜 PlayerPrefs:**
- 게임 진행 (GameState) 과 무관한 사용자 환경 설정 — 세이브 슬롯 X.
- SaveSystem 영향 0. SaveMigration 영향 0.
- 한 줄 API (`PlayerPrefs.GetFloat / SetFloat`).
- 단점 (레지스트리 저장 / 사용자 편집 불편) 은 V1.0 스코프에선 무시.

**옵션 항목 (사용자 합의):**

| 항목 | UI | 저장 키 |
|---|---|---|
| Master 볼륨 | MUIP Slider 0-100 | `FMLite.Options.Master` |
| SFX 볼륨 | Slider | `FMLite.Options.SFX` |
| BGM 볼륨 | Slider | `FMLite.Options.BGM` |
| 언어 | HorizontalSelector (KO/EN) | `FMLite.Options.Language` |
| 통화 | HorizontalSelector (£/$/€/₩) | `FMLite.Options.Currency` |
| UI Scale | HorizontalSelector (90/100/110/125) | `FMLite.Options.UIScale` |
| 자동 저장 | Switch (ON/OFF) | `FMLite.Options.AutoSave` |
| 단축키 안내 | ModalWindow (정적 표) | (저장 X) |

**자동 저장 트리거 (Q10 합의):**
- 매월 1일 / 시즌 시작 (6/1) / 시즌 종료 (5/15) 시 자동.
- 슬롯명: `autosave_<클럽>_<YYYY-MM>` 또는 `autosave_<클럽>_<season>_<event>`.
- 최근 3개 순환, 나머지 자동 삭제.

**디버그 모드 토글은 미포함** — 사용자 합의 (별도 디버그 메뉴 유지).

**영향 범위:**
- `UI/OptionsController.cs` 신규
- `Application/OptionsManager.cs` (static, PlayerPrefs 어댑터)
- `Scenes/OptionsScene.unity` 신규
- AudioMixer 도입 (Master / SFX / BGM 채널) — #60 과 짝
- LocalizationSystem.SetLanguage 연동 (기존)
- GlobalNavController 의 [옵션] 버튼 → OptionsScene 진입 (스택)

### V1.x 보완 포인트
- **그래픽 옵션** (해상도 / 풀스크린 모드 / VSync) — V1.x 풀빌드 시.
- **컨트롤 매핑** — 단축키 사용자 수정.
- **클라우드 동기화** — Steam Cloud 등.

---

## 60. 사운드 시스템 — 무료 라이센스 + AudioMixer + CREDITS.md (V1.0)

**결정:** AudioMixer 기반 (Master → SFX + BGM). 무료 라이센스 (CC0/CC-BY) 에셋 활용. BGM 3곡 + SFX 12종. 출처/라이센스 명시 (`Assets/_Project/Audio/CREDITS.md`).

**이유:**
- 자체 제작 부담 X (V1.0 마감 일정 우선).
- CC0 = 재배포 자유 (public repo 안전).
- CC-BY = 크레딧만 표기. README V1.0 갱신 시 사운드 섹션 추가.
- AudioMixer = Master/SFX/BGM 별도 슬라이더 (Options) 와 자연 연동.

**카탈로그:**
- **BGM 3곡**: MainMenu / Dashboard / Match. 각 ~2-3분 loop. crossfade 전환.
- **SFX 12종**: button_click / button_hover / inbox_received / goal / card_yellow / card_red / injury / substitution / match_kickoff / match_fulltime / save_complete / season_summary.

**소스 후보:**
- freesound.org (CC0/CC-BY 필터)
- Pixabay Music (CC0)
- OpenGameArt.org (다양한 라이센스, 사전 확인 필수)

**구현:**
- `Application/SoundManager.cs` 신규 (DontDestroyOnLoad, GameManager 패턴).
- `SoundManager.PlaySFX(SfxId)` / `PlayBGM(BgmId)` API.
- 씬 전환 시 BGM crossfade.

**영향 범위:**
- `Application/SoundManager.cs` 신규
- `Assets/_Project/Audio/` 신규 폴더 + Music + SFX + **CREDITS.md**
- `Assets/_Project/Audio/MasterMixer.mixer` 신규 (AudioMixer)
- 모든 UI 버튼 OnClick → `SoundManager.PlaySFX(SfxId.ButtonClick)`
- MatchSimulator → MatchTextController SFX 트리거 (Goal / Card / Injury / etc)
- OptionsController → AudioMixer.SetFloat 변환 (Mathf.Log10 × 20)

### V1.x 보완 포인트
- **자체 제작 BGM** — V1.x 폴리시.
- **상황별 BGM 변형** — 매치 종반 긴장 / 시즌 마지막 매치 등.
- **3D 사운드** — V1.x 매치 시각화 도입 시.

---

## 61. 통화 시스템 — GBP base 고정 환율표, 표시 변환만 (V1.0)

**결정:** 도메인은 항상 GBP (£) base 저장. 표시 시점에만 사용자 통화로 변환. 환율 고정 (GameBalanceSO 상수).

```csharp
public enum Currency { GBP, USD, EUR, KRW }

// GameBalanceSO 상수 (사용자 수정 X)
public static readonly Dictionary<Currency, float> ExchangeRates = new() {
    { Currency.GBP, 1.00f },   // base
    { Currency.USD, 1.27f },
    { Currency.EUR, 1.16f },
    { Currency.KRW, 1700f }
};
```

**이유:**
- **EPL 모티브** = GBP 기반 자연.
- **도메인 영향 0** — GameState 는 항상 GBP int. SaveMigration 영향 X.
- **환율 고정** = 시즌별 변동 / 사용자 수정 = V1.x 스코프. 단순화 우선.
- **표시 시점 변환** — `CurrencyFormatter.Format(int gbpAmount)` 헬퍼. 자동 M/K 단위.

**적용 범위:**
- TopBar 자금 표시 / Transfer 검색·오퍼 / Facility 비용·자금 / SeasonSummaryScene 재정 결산.
- MatchReport 의 결과 영향 X (이적료는 매치와 무관).

**영향 범위:**
- `Utils/CurrencyFormatter.cs` 신규
- `GameBalanceSO.exchangeRates / currencySymbols` 필드 추가 (직렬화 X — readonly)
- 11+ UI 컨트롤러의 `£X.XM` 직박 → `CurrencyFormatter.Format(amount)` 교체

### V1.x 보완 포인트
- **사용자 수정 환율** — GameBalanceSO 인스펙터에서.
- **시즌별 변동 환율** — 거시 경제 변동 (영국 파운드 약세 등).
- **인플레이션** — 시즌 진행에 따라 시장가 ↑.

---

## 62. 매치 디테일 V1.0 — viewMode 폐기 + 모든 핵심 이벤트 텍스트 + 5-Zone 골 빈도 밸런싱 (V1.0)

**결정:** 3가지 변경.

**(1) viewMode 폐기 (Q7 합의)**:
- V1.0 초안의 `viewMode (KeyOnly / GoalsOnly / All)` 폐기.
- **모든 매치는 단일 모드** — 모든 핵심 이벤트 (Goal / KeyPass / Save / Card / Injury / SubstitutionAI / Cross / Foul / Penalty / Free Kick / Corner) 텍스트로 노출.
- **사소한 이벤트** (성공 패스 / Midfield 점유 갱신) 는 통계만, 텍스트 비기록.
- `collectEvents` 플래그는 유저 매치 / 비활성 매치 분기에만.

**(2) 5-Zone 골 빈도 재밸런싱 (P0 hotfix)**:
- V0.5 플레이테스트 발견: 11대 5 같은 비현실적 스코어 빈번. EPL 평균 (2.7) 대비 과다.
- **목표**: `avgGoalsPerMatch` 2.7 ± 0.3 수렴.
- **조정 후보** (`algorithms.md` V1.0-2):
  - Shot success rate 분모 가중 (GK reflexes/handling ↑)
  - Box 진입 확률 ↓
  - 매 분 1~3 action 평균값 ↓
  - homeAdvantage 곱셈 ↓ (1.1 → 1.05)
- **검증**: 시즌 1회 완주 후 380 매치 평균 골수 측정. 2.4~3.0 진입까지 반복 튜닝.

**(3) 매치 텍스트 생동감 강화**:
- V0.5 ~40 키 → V1.0 ~150 키 확장.
- 같은 이벤트 5종 변형 (예: 골 = "환상적 슈팅" / "행운의 굴절" / "PK 침착" / "헤더 결정" / "장거리 폭격").
- 시드 기반 표현 회전.

**이유:**
- **사용자 피드백 2026-05-29**: "골이 너무 많이 나옴" + "텍스트 출력 안됨" — V0.5 매치 시뮬의 두 핵심 문제.
- **viewMode 폐기** = 사용자 인지 부담 ↓. 텍스트 폭주 회피는 사소 이벤트 제외로 해결.
- **밸런싱 P0**: 매치는 게임의 핵심 — 비현실적 스코어는 몰입감 파괴.

**영향 범위:**
- `MatchSimulator` 5-zone 파라미터 튜닝
- `GameBalanceSO` avgGoalsPerMatch 등 재산정
- `Match.events` collectEvents 분기 명세 갱신
- LocalizationSO 매치 이벤트 키 ~150 확장
- MatchTextController viewMode 제거

### V1.x 보완 포인트
- **xG / heatmap / 슈팅 위치** — V1.0 부분 도입 (§3.23 매치 결과 대시보드). V1.x 정밀화.
- **유저 코칭 인터럽트** — 전반 종료 / 중요 이벤트 시 외침. V1.x.
- **15-zone 정밀화** — V1.x.

---

## 63. 훈련 시스템 V1.0 — 개인 + 그룹 + GrowthSystem 통합 (사용자 추가 요청)

**결정:** GrowthSystem (매월 1일 자동 성장) 위에 유저 개입 레이어 추가. 그룹 훈련 (포지션별 강도) + 개인 훈련 (1 stat 4주).

**이유:**
- **사용자 추가 요청 (2026-05-29)**: "훈련 시스템 (개인 / 그룹 훈련 지시)".
- **FM 표준** — 훈련 / Mentoring 이 V0.5 design-decisions.md #53 V1.0 보완 포인트로도 기록되어 있음.
- **유저 영향력 ↑** — V0.5 까지 유저는 시설만 결정. V1.0 은 개별 선수 성장 방향 결정.

**그룹 훈련 (Squad-level):**
- `Tactic` 화면에 [훈련] 탭 (또는 별도 TrainingScene).
- 포지션 그룹별 강도: GK / DF / MF / AT 의 Low / Medium / High.
- 강도 ↑ → 성장률 ×1.2 / fatigue 누적 ↑ / 부상 위험 ↑.
- 그룹별 stat 집중 (예: AT → Finishing/Off the Ball 가중 / DF → Tackling/Marking 가중).

**개인 훈련 (Player-level):**
- PlayerProfile [개인 훈련] 버튼 → Modal.
- 1명당 1 stat 집중 (예: "Marcus Rashford / Crossing / 4주").
- 효과: 해당 stat 성장률 ×1.5, 기간 4주.
- 동시 가능 인원 = `Club.facilities.trainingLevel` (Lv1=2명, Lv5=6명, Lv10=10명).

**GrowthSystem 통합:**
- 매월 1일 호출 시 그룹/개인 modifier 적용.
- `algorithms.md` V1.0-4 신규 명세.

**영향 범위:**
- `Application/TrainingSystem.cs` 신규
- `Domain/Club.trainingDirective: TrainingDirective` 신규
- `Domain/Player.individualTraining: IndividualTraining?` 신규
- `GrowthSystem` modifier 입력 추가
- TacticScene [훈련] 탭 (또는 Squad 별도 탭)
- PlayerProfile [개인 훈련] Modal

### V1.x 보완 포인트
- **프리시즌 캠프** — 6/1~8/15 추가 성장.
- **개인 코치 배정** — FM 표준 Staff 시스템 (V1.x).
- **훈련 매치 (Friendly)** — V1.x.
- **Mentoring stat 영향** — V0.5 은 Hidden 만. V1.x stat 도.

---

## 64. V1.0 명세 정책 — Save Migration 무효 + 일정 마감 없음 + DOTween V1.x 미루기

**결정 (V1.0 일괄 정책):**

**(1) Save Migration V0.5 → V1.0 = 무효 (Q-MIG, Q8 V0.5 패턴 일관)**:
- V0.5 세이브 로드 시 `NotSupportedException`.
- V1.0 신규게임만.
- 이유: 도메인 변경 폭 큼 (Player.physical / Inbox / Club.colors / training / 등 신규). 마이그레이션 가치 < 비용.

**(2) 일정 마감 없음 (Q11)**:
- V0.5 와 동일. Stage 단위 GitHub Issue + 보드 (#50) 추적.
- 자유도 최대.

**(3) DOTween V1.x 미루기 (Q12)**:
- V1.0 = 정적 UI 로 마감.
- project-context.md "DOTween (V1.0 onwards)" 표현은 보강 필요 (V1.0 미적용).
- V1.x 도입 시 InboxPanel 슬라이드 / Modal Open / 탭 전환 / 등 부드러운 전환.

**이유:**
- **V0.5 → V1.0 도메인 변경 광범위** — 보존 시도가 부담. 사용자 합의 (Q8 패턴).
- **마감 없음** = V0.5 패턴 일관. 자유도 우선.
- **DOTween 미루기** = V1.0 시각 다듬기보다 시스템 (UI 일관성 / Options / 매치 대시보드 / 훈련 / 비교) 우선.

### V1.x 보완 포인트
- **V1.0 → V1.x Save Migration** 도입 검토 (도메인 안정화 후).
- **DOTween 일괄 도입** — 모든 모달 / 패널 / 토스트.
- **부드러운 폰트 폴리시** (Pretendard 등).

---

## 65. Unity MCP — Stage 0 첫 작업 + 4단계 fallback (V1.0)

**결정:** V1.0 의 **첫 작업 (Stage 0)** = Claude Code ↔ Unity Editor 직접 통신 셋업.

```
[옵션 A] CoplayDev/unity-mcp (커뮤니티 표준)        ← 1순위
   ↓ 실패 시
[옵션 B] Unity 공식 MCP (com.unity.ai.assistant)   ← 2순위
   ↓ 실패 시
[옵션 C] IvanMurzak/Unity-MCP                       ← 3순위
   ↓ 실패 시
[옵션 D] 기존 unity-ai 지시서 패턴 유지              ← Fallback (현 상태 유지)
```

**이유:**
- **사용자 명시 우선순위 (2026-05-29)**: "맨 처음 작업으로 Unity MCP 연결을 했으면 좋겠어. 네가 직접 씬을 보고 Unity AI 한테 직접 명령할 수 있게. 전에 시도했는데 잘 안돼서 포기했었거든."
- **V1.0 의 거의 모든 작업이 UI 폴리시 / 새 씬 생성 / 통합** — 씬 작업 효율이 V1.0 일정 결정.
- 성공 시: V0.5 대비 2~3배 빠른 작업 (사용자 추정).
- **실패해도 손실 없음** — 옵션 D 로 V0.5 패턴 유지. 블로킹 X.

**핵심 함정 (Windows 네이티브):**
- Issue #773 — uvx --from 자동 config 실패. `uv --directory` 수동 우회.
- Unity 6.3 + Unity 공식 MCP — System.Collections.Immutable DLL 충돌. 수동 fix.
- WSL2 ↔ Windows 통신 — http transport + portproxy (현재 환경은 네이티브 PowerShell 이라 해당 X).

**상세 셋업 절차 / 트러블슈팅 / 롤백:** `docs/unity-mcp-setup.md` (전용 문서).

**완료 조건:**
- Smoke Test 통과 (MCPTest GameObject 생성)
- DashboardScene Hierarchy 읽기 가능
- 의도하지 않은 .cs / 씬 / prefab 변경 없음

### V1.x 보완 포인트
- **MCP 도구 자동화** — V1.0 = 수동 호출. V1.x = 일반화된 작업 패턴 (예: "씬 X의 모든 버튼에 SFX 추가") 자동.
- **자체 MCP 도구 추가** — IvanMurzak/Unity-MCP 패턴 (C# 메서드 → 도구) 으로 우리 시스템 (MoraleSystem.Tick / GrowthSystem.Apply 등) 노출.
- **EditMode 테스트 자동 실행** — V1.0 = 사용자 수동. V1.x = MCP 자동.

---

## 66. Inbox 도메인 + 정책 (V1.0 — Task A.1)

**결정:** V1.0 에서 모든 인게임 알림을 `GameState.inbox: List<InboxItem>` 에 영구 저장한다. V0.5 DashboardController 의 in-memory 메시지 목록을 대체.

**도메인 타입:**

```csharp
[Serializable]
public class InboxItem {
    public int id;
    public InboxCategory category;     // Match / Transfer / Morale / Board / Youth / Cup / Award
    public InboxPriority priority;     // Low / Medium / High / RequiresAction
    public DateTime createdAt;
    public DateTime? deadline;         // null = 기한 없음
    public bool isRead;
    public string titleKey;
    public Dictionary<string, string> titleArgs;
    public string bodyKey;
    public Dictionary<string, string> bodyArgs;
    public InboxAction action;         // None / OpenScene / OpenDialog
    public string actionTargetSceneOrDialogId;
}

public enum InboxCategory { Match, Transfer, Morale, Board, Youth, Cup, Award }
public enum InboxPriority { Low, Medium, High, RequiresAction }
public enum InboxAction { None, OpenScene, OpenDialog }
```

**GameState 확장:**

```csharp
public List<InboxItem> inbox = new();
public int nextInboxId = 1;
```

**Q1 — 기한 만료 처리:**
- `deadline` 도래 + 미처리 → 자동 거절 / 자동 수락 X.
- 인박스에서 사라지지 않음 (비활성 표시).
- CounterOffer 기한 만료 시에만 예외 — V0.5 동작 유지 (`status = Rejected` 자동).

**Q2 — 시즌 종료 정리:**
- 5/15 (`SeasonEndProcessor`) 호출 시 `isRead == true` 항목 삭제. 안 읽은 항목만 유지.
- 누적 최대치 없음 — 유저가 읽으면 다음 시즌 종료 시 사라짐.

**Q3 — YouthIntakeAvailableEvent 정책 변경 (V1.0):**
- V0.5: GameManager 정지 신호 + 강제 YouthScene 진입.
- V1.0: InboxItem(Youth/RequiresAction, OpenScene:YouthScene) 으로 교체. 정지 신호 제거.
- 이유: 강제 씬 전환 폐기(B.2/B.3) 정책 일관 — 인스펙션은 RequiresAction 표시로 충분.

**InboxRouter (`Application/InboxRouter.cs`):**
- `static void Wire(GameState state)` — 10 이벤트 구독.
- `GameInitializer.NewGame` + `SaveSystem.Load` 직후 `InboxRouter.Wire(state)` 호출.
- 전체 이벤트 목록 / 우선순위 / action 은 `algorithms.md` V1.0-7.3.

**이유:**
- **세션 간 영속** — V0.5 in-memory 알림은 씬 재진입 시 소멸. V1.0 GameState 에 저장해 세이브/로드 후에도 미처리 항목 보존.
- **우선순위 정렬** — RequiresAction → High → Medium → Low 정렬 가능.
- **카테고리 탭 분리** — InboxPanel UI (Stage B.1) 에서 탭별 필터링.
- **강제 씬 전환 폐기** — 정지 신호 없이도 RequiresAction 배지로 사용자 주의 유도.

**명명 충돌 주의 (비블로킹):**
- `FMLite.UI.InboxItem` (MonoBehaviour, Dashboard row, V0.5 잔재) 와 `FMLite.Domain.InboxItem` (data class, V1.0 신규) 가 공존.
- 다른 네임스페이스라 컴파일 충돌 없음. Stage B.1 에서 UI 클래스 `InboxItemRow` 로 rename 권장.

### V1.x 보완 포인트
- **읽음 처리 자동화** — InboxPanel 에서 항목 클릭 시 `isRead = true` 자동.
- **body 필드 활용** — 현재 `bodyKey` 는 비어 있음. 확장 시 팝업 상세 내용.
- **Award 카테고리** — 시상(SeasonAward) 이벤트 InboxRouter 흡수 (V1.x).

---

## 67. Player.physical — 신체 조건 도메인 + PlayerGenerator 추첨 (V1.0 — Task A.2)

**결정:** `Player` 에 `PhysicalAttributes` 컴포지션 필드 추가. 포지션별 실세계 평균으로 생성.

```csharp
[Serializable]
public class PhysicalAttributes {
    public int height;           // cm, Clamp [165, 205]
    public int weight;           // kg, Clamp [60, 100]
    public Foot preferredFoot;   // Left / Right / Both
    public int weakFootAbility;  // 1-5 (별점, 약발 능숙도)
}
```

**생성 규칙 (V1.0-8.2):**
- 포지션별 평균 키/몸무게 (hardcoded 실세계 근사값) + NextNormal(σ=6 cm, σ=8 kg)
- `preferredFoot`: Right 70% / Left ~22.5% / Both ~7.5% (두 번 RNG 호출)
- `weakFootAbility`: 1~5 균등 추첨

**이유:**
- **매치 영향 (V1.0-8.3)**: 헤더(키×점프)/민첩(역키)/발 일치(FK/PK) 등 신체 정보를 매치 시뮬레이션에 반영.
- **플레이어 정체성**: 프로필 화면에 신장/체중/선호발 표시로 캐릭터 다양성.
- **SaveMigration**: V0.5 세이브 무효 (#64). 신규 게임 PlayerGenerator 자동 생성 → 별도 마이그레이션 없음.

**주의 — 명명 혼동 방지:**
- `Player.stats.physical` = `PhysicalStats` (pace, agility 등 능력치 숫자)
- `Player.physical` = `PhysicalAttributes` (신체 치수 데이터)

### V1.x 보완 포인트
- **포지션 평균값 GameBalanceSO 외부화** — 현재 PlayerGenerator static readonly 상수. V1.x 에서 `PositionSO.avgHeight / avgWeight` 로 이전.
- **V1.0-8.3 매치 통합** — Task G.2 에서 신체 조건을 매치 시뮬레이션에 반영. A.2 는 도메인/생성만.

---

## 68. Player.growthHistory + GrowthSystem 월별 스냅샷 (V1.0 — Task A.3)

**결정:** GrowthSystem.Tick (매월 1일) 시작 시 각 선수의 현재 stats 스냅샷을 `Player.growthHistory` 에 저장한다.

```csharp
[Serializable]
public class StatSnapshot {
    public int year;
    public int month;
    public Stats stats;   // Stats.Clone() — 성장 처리 전 상태
}
```

**스냅샷 타이밍:** Tick **시작 시** (성장 처리 전). 이렇게 해야 "이 달 시작 시점의 stats" 를 기록 → 3개월 전 시작 시점과 현재(처리 후) 비교로 변화량 계산.

**변화량 헬퍼:** `GrowthSystem.GetStatChange(player, "technical.passing", 3)` 형태.
- `growthHistory.Count < monthsBack` → 0 반환
- Reflection: `Stats` 서브오브젝트 이름 + 필드 이름으로 조회
- 소비자: PlayerProfile 성장 화살표 (C.4) / Squad 성장 열 (D.2)

**이유:**
- **성장 추세 시각화**: UI 에서 "+2 ↑ / 0 → / -1 ↘" 같은 화살표 표시.
- **순수 도메인 클론**: `Stats.Clone()` 은 외부 의존성 없이 필드 직접 복사.
- **메모리**: 49 int × player 수 × 히스토리 길이. 12개월 × 500명 = ~300K int ≈ 1.2MB. 허용 범위.

### V1.x 보완 포인트
- **히스토리 상한** — 현재 무제한. V1.x 에서 최근 12개월만 유지 (trimming) 검토.
- **CA 변화 추적** — 현재는 stats 49개만. `currentAbility` 도 스냅샷에 포함 시 "CA 성장 곡선" 표시 가능.

---

## 69. 이적 흐름 재구성 — 이적료↔개인조건 분리 + 선수 개인협상 단계/씬 (V1.0 — Stage F 선행, #469)

**결정:** 이적을 FM 표준 2단계로 분리 — (1) 구단 이적료 협상 (역제안), (2) 선수 개인조건 협상 (`Negotiating` 단계, 전용 씬). 명세에 정의됐으나 미사용이던 `OfferStatus.Negotiating` ([3-b][4]) 을 실제 인터랙티브 단계로 구현.

**배경 (결함 3건):**
1. 코드가 `ratio ≥ 1.30` 수락 구간을 `CounterOffer(counterAmount=amount)` 로 위장 → **후한 오퍼도 항상 '역제안'** 으로 표시 (사용자 혼란).
2. 선수 개인협상(`PlayerNegotiate`) 이 코드로 자동 처리될 뿐 **UI(씬) 가 없음** → 구단 합의 후 아무 화면도 안 뜸.
3. `NegotiationScene.offerItemPrefab` 미연결 → 역제안 씬 빈 화면.

**변경 (흐름):**
- `AiRespondToOffer`: `ratio ≥ 1.30` → `status = Negotiating` (구단 이적료 합의). `1.10 ≤ ratio < 1.30` → `CounterOffer` (진짜 역제안, 이적료 흥정). 이하 거절.
- `RespondToCounterOffer.Accept` / release clause → `Negotiating` 으로 수렴 (이적료 합의 완료).
- **신규 `RespondToPersonalTerms(offerId, proposed, playtime)`**: `Negotiating` 오퍼에 주급/계약기간/출전약속 제안 → `EvaluatePlayerAcceptance`(순수, loyalty/ambition/wage/playtime). 수락 → `Accepted` / 거절 → `personalNegotiationRound++`, `maxPersonalNegotiationRounds(4)` 초과 시 `Rejected`, 아니면 `Negotiating` 유지(재제안). **반복 협상.**
- **AI 구매 구단**: 개인협상 UI 없음 → `ProcessOffers` 가 `Negotiating + toClubId != userClubId` 를 `AutoResolveAiPersonalTerms` 로 1회 자동 평가 (V0.1 AI 이적 완결성 유지).
- 오퍼 제출은 **이적료만** (`TransferController`); 주급/계약기간은 개인협상 씬에서 결정 (`SubmitOffer` 에는 공정 주급/3년 placeholder).

**변경 (UI):**
- 신규 `PlayerNegotiationController` + `PlayerNegotiationScene` (NegotiationScene 복제 후 재구성): Negotiating 오퍼 목록 → 선택 → 주급 입력 + 선수반응 라벨(확률 3단계) + [제안] → 결과 피드백(성사/재협상 R{n}/{max}/결렬). `EstimatePlayerAcceptChance` 로 반응 미리보기.
- `NegotiationController` 는 `CounterOffer` 전용으로 축소.
- `offerItemPrefab` 연결 (#4 수정).

**변경 (통지):** `InboxRouter` — `Negotiating` → Transfer/RequiresAction `OpenScene:PlayerNegotiationScene`, `Accepted` → High, `Rejected` → Medium, `CounterOffer` → NegotiationScene. `EventScheduler` Continue 정지 트리거에 `Negotiating` 추가.

**이유:**
- **명세 우선 + FM 표준**: 이적료 합의와 개인조건 협상은 별개 단계. `Negotiating` status 가 본래 그 의도.
- **인터랙티브**: 자동 처리 → 유저가 주급을 조정하며 반복 협상 (거절 시 상향 재제안). 결정성 시드는 round 미포함 — 같은 날 같은 조건=동일 결과, 조건 상향 시 수락 확률↑.

**V1.x 보완 포인트:**
- **출전 시간 약속 = 카테고리 시스템 (정식 구현 필요)**: 현재 `Promise.PlaytimeAgreement` 는 `targets["minPlayRatio"]`(0~100 %) 기반이고, **명명된 스쿼드 지위 카테고리(주전/중요선수/로테이션/비주전/후보 등)가 없음**. FM 표준대로 카테고리 → minPlayRatio 매핑(예: 주전 70 / 중요 50 / 비주전 30 / 후보 10)을 도입해야 함. 추가로 **이적 완료 시 약속 Promise 가 생성되지 않음** — `CreatePlaytimeAgreement` 는 현재 `MoraleSystem`(면담)에서만 호출되고, `TransferSystem.CompleteTransfer` 와 미배선. `offer.includesPlaytimeAgreement`(bool) 는 선수 수락 보너스(+0.2)에만 쓰임. **계약 후 약속 조회·재협상·해제 UI 도 없음**(불만만 누적). → V1.x 에서 (a) 카테고리 enum + 비율 매핑, (b) 오퍼에 카테고리 필드 + 수락 보너스 카테고리별 차등, (c) CompleteTransfer 시 Promise 생성 배선, (d) 선수 프로필/면담 약속 관리 UI 를 한 묶음으로 구현. **v1.0 에서 개인협상 씬의 이진 출전약속 토글은 제거**(카테고리 미구현 상태의 토글은 오해 유발).
- 개인협상 씬 UI 폴리시 (v1.0 적용 완료): 반응 라인에 `주급 £X/주`(통화·단위) + 기분(색 코딩) 표시, 미리보기는 공개 정보(주급)만 → 숨은 능력치로 라운드 유의미. 주급·계약기간 입력 + placeholder 정상화. ResponsePanel 딤 `raycastTarget=false`(전체화면 오버레이가 [뒤로] 버튼 클릭 차단하던 문제 — 협상 후 씬 탈출 불가 수정).
- 기본 오퍼 금액 = `시장가 × 1.30`(= `aiAcceptThreshold`) 라 디폴트 제출이 자주 즉시 수락됨 — 친선 마찰 부족. 디폴트를 흥정 구간(~1.10×)으로 낮추거나 `aiAcceptThreshold` 상향 검토 (밸런스).
- `Rejected` 사유 구분 (구단 거절 vs 선수협상 결렬) — 현재 단일 문구.
- `Accepted` 는 Continue 정지 안 함 (인박스 배지로만 통지).
- 직접 Play(게임 미시작) 시 `GameManager.State == null` → 협상 씬 목록 비어 패널 미표시 (정상). 협상 씬은 실제 오퍼 플로우로만 테스트 가능.

---

## 70. 매치 엔진 xG 찬스-퀄리티 레이어 + 평점 재설계 (V1.0 — Stage G, #474)

**결정:** Stage G.5 재밸런싱을 "블런트 4-파라미터 튜닝" 대신 **찬스-퀄리티(xG) 레이어**로 구현. 동시에 전체 평점 시스템(I.4)을 FM 정합 + 포지션 가중으로 재설계. 5-Zone Markov 구조 / `Simulate(...)` 인터페이스 / 결정성(시드)은 유지 — 바뀌는 건 ResolveShot 전환 모델 + ComputeRatings + MatchResult 출력.

**배경 (사용자 결정 2026-06-04):** "매치는 게임 핵심부 — 명세만 따르지 말고 더 FM 다운 방안 있으면 제안." → xG 레이어 채택. + "빅찬스 미스 평점 급락" + "전체 평점 시스템 깊게 검토·수정."

**(1) xG 찬스-퀄리티 (`algorithms.md` V1.0-1):**
- V0.5 평탄 전환 (`conversion = 0.30 + (shoot-gk)/150`) → 박스 도달 = 동급 찬스 = 과득점 근본원인.
- chanceType (ClearChance/OpenPlay/Header/LongShot/DirectFreeKick) 별 `baseXG`. 기록되는 `shot.xG` = 찬스 품질(situation, 슈터 무관). 실제 골 = `xG × finishMod × gkMod`.
- **밸런싱이 수학**: E[goals] ≈ Σ xG. 팀당 ΣxG ≈ 1.35 → 2.7/경기 직접 산정 (시행착오 X).
- FM 원리 차용 (리서치): "슛 가치는 어떻게 만들어졌나(스루패스/위치/압박)로 결정."

**(2) 평점 재설계 (`algorithms.md` V1.0-1 평점 subsection):**
- 현 I.4 결함: 포지션 무가중(수비/미드 저평점) / 패스·점유 기여 0 / 실점책임 GK 독점 / 부진 감점 부족.
- FM 원리(리서치): "포지션이 평점을 만든다" — 수비수 태클·인터셉트·무실점, 미드 패스성공률·키패스, 공격수 슛퀄·골.
- **인위적 라인 곱셈 대신 각 포지션 액션을 충분히 가치화** → 포지션 특성 자연 발현. 라인 게이팅은 무실점/실점(GK+DF)에만.
- 신규: 패스 성공률 티어 보너스, 수비 액션·클리어런스 가치 ↑, DF 무실점/실점 공유, xG 보정(clinical finish +/빅찬스 미스 −), `ratingGoalBonus` 1.0→0.8.

**(3) G.2 신체 영향 통합:** 헤더(키×헤딩×점프) / agility(키 역상관) / pace+weakFoot / PK·FK 발 일치 — V1.0-8.3을 xG/zone resolution에 통합.

**(4) G.1 이벤트 확장:** Offside / ThrowIn / KeeperPunch / LongShot — MatchEventType 추가 (텍스트는 G.6 후속).

**신규 통계:** `PlayerMatchStat.xg / bigChancesMissed / clearances`. `MatchResult.shotMap(ShotPin{side,x,y,xg,outcome}) / zoneOccupancy[5]` — AA.1/AA.2 선당김 (히트맵·슛맵 데이터 엔진에서 생성).

**영향 범위:** `MatchSimulator` ResolveShot/ComputeRatings 재구성 (zone 구조 유지) / `MatchResult`·`PlayerMatchStat`·`Match.MatchEventType` 확장 / `GameBalanceSO` xG·평점 파라미터 신규 / `MatchSimulatorTests` 임계치 갱신 + 측정 하네스 / `GameBalance.asset` reseed (Sub-C).

**V1.x 보완 포인트:**
- xG 좌표(x,y) 정밀화 — V1.0은 chanceType별 대표 위치 근사. V1.x 실제 슛 위치 모델.
- 15-zone 정밀화 / 카운터어택 명시적 chanceType / 듀얼·헤더 승률 통계 → 평점 추가 반영.
- 평점 롤(Role) 별 기대치 차등 (현 V1.0은 라인 단위) — FM 처럼 Role 별 KPI 가중.

---

## 71. 포메이션 기반 라인업 선정 + AI 자동 라인업 (노이즈) (V1.0 — Stage H, #474 후속)

**결정:** 매치 라인업 선정을 **포메이션 정합**으로 재작성. 현 `SelectStartingEleven` 의 "포지션 무시 CA top-11 폴백" 폐기. **미구현 — Stage H 에서 구현** (`algorithms.md` V1.0-14 명세 예약).

**배경 (사용자 결정 2026-06-04):** Stage G(#474) 검증 중 발견된 `StartingEleven_*` 테스트 기존 실패의 근본 원인 = CA top-11 폴백이 포지션을 무시 (GK 여럿·수비 0 같은 비현실 XI 가능). 지금 고치지 말고 **알고리즘 자체를 새로** 설계하기로.

**정책:**
1. **유저 팀**: 라인업(11 슬롯 배정) 미지정 시 **매치 시뮬레이션 차단** — 라인업 의사결정을 강제 (FM 표준). MatchSimulator 자동 폴백 금지.
2. **AI 팀**: 포메이션(`FormationSO.slotPositions`)에 맞춰 자동 배치. 적정 CA 우선 + **노이즈**(`lineupNoiseSigma`)로 항상 최적은 아니게. **세부 stat/trait/시너지/매치업 풀최적화 배제** (AI 완벽 라인업 = 밸런스 붕괴). 단 상식 범위 — 일부러 약체 선발 X.

**이유:**
- 포지션 무시 라인업은 비현실적 + 전술/스카우트/육성 의사결정 무의미화.
- 유저 강제 라인업 = FM 핵심 루프 (전술이 결과에 영향).
- AI 노이즈 = 리그 다양성 + 유저 우위 여지 (AI가 매번 최적이면 추월 불가).

**영향 범위 (Stage H):** `MatchSimulator.SelectStartingEleven` (유저 검증 / AI `AiAutoLineup` 분리) / 매치 진입 게이트 (유저 라인업 차단) / `GameBalanceSO.lineupNoiseSigma` / `MatchSimulatorTests.StartingEleven_*` 재작성 (그때까지 `[Ignore]`).

**현 처리:** PR1(#474)에서는 명세만 추가. 실패 테스트 2건(`StartingEleven_TopByCAExcludingInjured` / `_ExcludesSuspendedPlayers`)은 `[Ignore]` 처리 (구 폴백 동작 검증 → 폐기 예정).

---

## 72. 슈퍼유망주 — 효과 없는 동적 파생 라벨 (V1.0 플레이테스트)

**결정:** `슈퍼유망주`(trait id=14) 를 **랜덤 부여 + 영구 보유 + 성장/시장가 효과** 에서 **나이·CA 기반 동적 파생 라벨 (효과 없음)** 으로 전환.

- 부여 조건: `age ≤ superProspectMaxAge(21) && currentAbility ≥ superProspectMinCA(110)`.
- **매월 재평가** (`GrowthSystem.Tick`): 조건 충족 & 미보유 → 부여 / 조건 실패(22세 도달 또는 CA 미달) & 보유 → **회수**.
- **효과 제거**: 기존 `GrowthRateModifier 1.5 + MarketValueModifier 1.2` → 빈 effects. 순수 표시 라벨.
- `PlayerGenerator` 랜덤 trait 풀에서 id=14 **제외** (생성 시 무작위 부여 X).

**이유:**
- 노장에게 "슈퍼유망주" 가 붙던 버그 = 나이 무관 랜덤 부여 + 회수 로직 부재.
- "잠재 라벨" 과 "성장 가속" 을 한 trait 에 이중으로 묶었던 것을 분리 — 성장 가속은 늦깎이형 / PA 곡선이 담당. 라벨은 관찰 가능 사실(어린 나이 + 높은 CA)에서 파생되므로 가시성도 자연(#73 의 CA 가시성 따름).
- 수치 외부화(#11): `superProspectMinCA` / `superProspectMaxAge` = `GameBalanceSO` (int 라 epsilon 불필요).

**영향 범위:** `GrowthSystem.Tick` (슈퍼유망주 재평가 단계) / `PlayerGenerator.SelectTraits` (풀 제외) / `GameBalanceSO` 신규 2필드 / `SeedV10Data.GenerateTraitsV10` id=14 effects 비움 (에셋 chore) / `algorithms.md` V1.0-11 보강.

**확인 (사용자, 2026-06-05):** CA≥110 & 21세 이하, 매월 재평가·회수, 효과 없음.

### V1.x 보완 포인트
- 나이별 차등 임계 (17세 95 / 21세 115 등) — 현재 절대 110.
- 라벨 단계 확장 (fringe prospect / breakthrough 등).

---

## 73. Trait 가시성 3-tier + trait 검색 폐지 (V1.0 플레이테스트)

**결정:** `TraitSO` 에 `visibility` enum 신규 — `Concealed` / `Public` / `ScoutGated`. trait 기반 검색은 폐지.

| Tier | 노출 | trait |
|---|---|---|
| **Concealed** (영구 비노출, 내부 메커닉) | 자기팀 포함 어떤 화면에도 X | 1 늦깎이형, 2 조숙형 |
| **Public** (항상) | 스카우팅 무관 표시 | 6 만능형, 8 무리한패스, 9 와이드플레이어, 15 멀티포지션, 16 골결정력, 17 수비형윙백, 19 페널티스페셜리스트, 20 프리킥마이스터 |
| **ScoutGated** | 자기팀/완전정찰 표시, 미정찰 가려짐 | 3 부상취약, 4 멘탈강자, 5 빅매치형, 7 클러치형, 10 자국인우대, 11 유리몸, 12 철인, 13 멘탈약자, 18 정신적리더 |

- 14 슈퍼유망주 = CA 파생 라벨 → CA 가시성을 따름(#72).
- **스카우팅 공개 규칙**: `ScoutingSystem` 이 `ScoutReport.revealedTraitIds`(기존 미사용 필드) 를 `scoutLevel`(0~100) 비례로 채움 — 공개 수 `n = round(scoutLevel/100 × 해당선수 ScoutGated trait 수)`, 결정적 순서. 자기팀 = scoutLevel 100 → 전부. 미정찰 = 0 + "추가 정찰 필요" 표시(정확 보유 수 비노출 — 정보 누설 방지).
- **검색 폐지**: `TransferController` trait 드롭다운 + `TransferSystem.SearchPlayers` `filter.traitId` 제거.

**이유:** 트레잇 검색 = 스카우팅 시스템 무력화(사기). 늦깎이/조숙형 등 성장 궤적·잠재는 "신이 아닌 이상 알 수 없음" → Concealed. 멘탈/부상 성향은 조사·시간으로 알게 됨 → ScoutGated. 기록/플레이 스타일은 공개 관찰 가능 → Public.

**영향 범위:** `TraitSO.visibility` 신규 enum 필드 / `ScoutingSystem` revealedTraitIds 채우기 / `PlayerProfileController.BuildTraitsText` (Concealed 제외 + 비자기팀 revealedTraitIds 만) / `TransferController`·`TransferSystem` trait 검색 제거 / `SeedV10Data` visibility 세팅 (에셋 chore) / `algorithms.md` V1.0-15 신규 / Localization "미정찰" 키.

**확인 (사용자, 2026-06-05):** 3-tier 분류 + scoutLevel 비례 공개 + 검색 폐지.

### V1.x 보완 포인트
- 부분 정찰 시 "대략적 표현"(예: "압박에 강할 수도") — 현재 공개/비공개 이분.
- 스카우트 staff 별 정찰 정확도 차등.

---

## 74. 구단 재정 밸런싱 — 주급 차감 + 수입/임금 비율 정상화 + 시설비 상향 (V1.0 플레이테스트)

**결정:** "자금이 너무 넉넉" 의 근본 = **상시 유출(주급) 부재 + 수입이 임금 대비 과소**. 단순 가격 상향이 아니라 현금흐름 정상화로 해결.

- **(1) 주급 월 차감 신설**: `Day==1` (DailyProcessor 훅) 에 `Σ(스쿼드 주급) × (52/12)` 를 `finance.money` 에서 차감. 대상 = 전 구단(isActiveSimulation).
- **(2) 수입 재스케일 + 분산**: 목표 *전형적 구단 연 매출 ≈ 연 임금 ÷ 0.63* (≈임금×1.6 — EPL 임금/매출 63% 앵커). TV 수입 대폭 상향, matchday 는 홈경기마다/월별 분산, 상금 시즌말.
- **(3) 시설비 상향**: 목표 *전 시설 풀업 ≈ 한 시즌 이적예산급* 부담 (현재 자금의 0.2~1% → 의미 있는 %). `FacilityLevelSO` 비용 곡선 재산정.
- **(4) 시작 자금**: rep 차등 유지(= 구단마다 다름, 정상 동작), 절대액은 측정 후 결정.
- **(5) 측정 하네스**: 시즌 1회 완주 후 구단별 순현금흐름 측정. 합격 기준 = *중위 구단 시즌당 본전±(성적 따라 흑/적자), 돈으로 전원 영입 불가*. 사용자 Test Runner 1~2회 미세조정 (G.5 패턴).

**이유:** 주급이 어디서도 안 빠지고(유출 0) 수입만 누적 → 자금 영구 증가. 현실 EPL 은 매출 > 임금(임금 63%) 이나 우리는 수입(rep50 ≈ 연 4.3M) ≪ 임금(≈ 연 32.5M) 으로 비율 역전. 시설비만 올려도 시작자금이 커서 무의미 → 유출 신설이 본질. 수치 외부화(#11) + float 비교 epsilon.

**영향 범위:** `WageSystem`(또는 FinanceSystem 월처리) 신규 / DailyProcessor Day==1 훅 / `FinanceSystem` 수입 재스케일+분산 / `FacilityLevelSO` 비용 곡선 / `GameBalanceSO` 재정 필드 재산정 / `GameBalance.asset`+Facility 에셋 reseed (chore) / `algorithms.md` V1.0-16 신규 + 측정 하네스 / `MatchPostProcessor`(matchday 분산 시).

**확인 (사용자, 2026-06-05):** 유출 신설+비율 정상화 방향 / 주급 월차감·전 구단 / AI=ratio·명성은 선수수락에만(#75) / 측정 기준 동의.

### V1.x 보완 포인트
- 부채/이자, 스폰서·중계권 협상, 인플레이션(시즌별 시장가↑), 운영비(스태프·시설 유지비) 세분.

---

## 75. 이적 시장 현실성 — 선수 개인협상에 영입구단 명성 격차 반영 (V1.0 플레이테스트)

**결정:** 구단(`AiRespondToOffer`)은 이적료 ratio 로만 움직이되(현행 유지), **선수 개인협상 수락(`ComputePlayerAcceptChance`)에 영입 구단 명성 격차 항 추가**.

- `playerExpectedRep` = CA → 명성 스케일 매핑(외부화). `gap = playerExpectedRep − buyingClub.reputation`.
- `acceptChance −= clamp(gap × repGapWeight × (ambition/50), 0, repGapMaxPenalty)` — 야망 높을수록 까다로움.
- **임금 보상 가능**: 기존 wageRatio 항이 높으면 상쇄 → 약체도 고임금이면 영입 가능("엄청난 요구").

**이유:** 현재 구단·선수 평가 둘 다 영입구단 명성 미반영 → "명성 낮은 팀도 돈만 주면 선수가 옴". FM 정합 — 선수는 구단 명성/야망 수준이 자기 기대에 못 미치면 거절, 단 임금으로 보상 가능. "구단=돈, 선수=명성도" 분리는 사용자 지시. 부동소수점 epsilon(#11).

**영향 범위:** `TransferSystem.ComputePlayerAcceptChance`(명성격차 항) / `GameBalanceSO` `repGapWeight`/`repGapMaxPenalty`/CA→rep 매핑 계수 / `algorithms.md` 3.1 Transfer Flow(또는 V1.0-16) / `EstimatePlayerAcceptChance` 미리보기 정합.

**확인 (사용자, 2026-06-05):** 동의 (방향 + 구단/선수 분리).

### V1.x 보완 포인트
- 스쿼드 평균 CA(선수단 수준) 직접 반영 — 현재 club.reputation 근사.
- 유럽대항전 출전 / 감독 명성 / 라이벌 관계 등 추가 변수.

---

## 76. 인박스 대확장 — League 카테고리 신규 + 핵심 신규 이벤트 (V1.0 플레이테스트)

**결정:** 인박스가 한산 → FM 식 자잘한 상시 알림 확충. **핵심 범위만 V1.0** (정찰리포트/마일스톤/출전정지/경기결과요약은 후속).

- **`InboxCategory.League` 신규** (enum **끝에 append** — 직렬화 int 시프트 회피). 순위 변동/역전을 여기로.
- **Tier 1 (이벤트 존재, 라우팅만)**: 부상발생(`PlayerInjuredEvent` → Match), 월간/시즌 시상(SeasonAwardSystem → Award 카테고리 — #66 V1.x 노트의 Award 라우팅을 V1.0 로 당김), 유스 성장(`PlayerStatChangedEvent` → Youth).
- **Tier 2 핵심 (신규 이벤트)**: 선수 불만(`PlayerUnhappyEvent`/Morale), 피로 누적(`PlayerFatiguedEvent`/Morale), 순위 변동(`StandingsChangedEvent`/League), 부상 복귀(`PlayerRecoveredEvent`/Match), 계약 만료 임박(`ContractExpiringEvent`/Transfer).
- 정책: 전부 `deadline=null`, 대부분 `priority=Low`, Q1(기한만료 무효)·Q2(시즌종료 시 읽은 것 삭제) 유지. 카테고리 탭+우선순위 정렬(Stage B)로 폭주해도 중요 알림 안 묻힘.

**이유:** `InboxRouter` 가 10 이벤트만 구독 → 한산. 사용자: "FM 처럼 자잘하게 많이". 비용 차 커서 Tier 분리(라우팅만 vs 신규 이벤트), V1.0 은 핵심만.

**영향 범위:** `InboxCategory` enum +League / `InboxRouter` 구독 확장 / 신규 이벤트 5종 `event-bus-catalog.md` 등재 + 발화 지점(DailyProcessor/MatchPostProcessor/월처리) / Localization 신규 키 / `design-decisions #66` 확장.

**확인 (사용자, 2026-06-05):** League 카테고리 신규(Match 아님) / 핵심만.

### V1.x 보완 포인트
- 정찰 리포트(다음 상대 분석), 마일스톤(연승/무패/연속 클린시트), 출전정지, 경기결과 요약, 훈련 결과 알림.

---

## 77. V1.0 플레이테스트 UX 보강 묶음 — 네비/프로필 액션/단축키/보드 보상/squad/리더보드/폴리싱

**결정:** 플레이 직결 잔여 항목 일괄 (각 subsection 은 독립 작업이나 한 묶음으로 추적).

- **(77-1) 글로벌 선수명 링크 + 프로필 가시성 게이팅**: 모든 씬의 선수 이름 클릭 → 그 선수 PlayerProfile (드롭다운/필터 내 이름 제외). `PlayerNameLinkController`(V0.5 인프라, 미배선) 를 이름 표시 지점에 일괄 부착. `PlayerProfileController` 에 **자기팀/타팀 분기 + 스카우팅 가시성 게이팅(#73)** + PreviousScene 복귀.
- **(77-2) 프로필 액션 버튼**: 자기팀 선수 = [재계약](→ `TransferSystem.RenewContract` 모달 — 로직 기구현). **[콜업]은 유스 선수에게만** 노출(→ `YouthSystem.PromotePlayer`). 둘 다 "선수 정보창" 진입 후 버튼.
- **(77-3) 전역 단축키**: `GlobalNavController`(씬별 인스턴스)에 **Esc=뒤로 / Ctrl+S=저장**(컨텐츠 씬), **Space=Continue**(Dashboard 한정). 매치씬 1/2/3/4 유지. Options 단축키 안내(X.6)와 일치.
- **(77-4) 보드 약속 이행 보상**: `BoardSystem` 이행 경로에 `boardConfidence += boardPromiseFulfillReward` + **managerReputation 가산**(둘 다, 외부화). 현재 거절 페널티만 존재.
- **(77-5) WB/AM squad 생성**: 기존 `FormationConfig` 구조 보존하며 **WB/AM 을 1급 슬롯으로 편입**(GK/CB/LB/RB 패턴 동일) + Formation 에셋 재생성. 현재 randomSlots 운에만 의존하던 결함 해소. (#71 라인업 선정과 별개 — 이건 ClubGenerator 스쿼드 구성.)
- **(77-6) 리그 개인 리더보드**: `SeasonAwardSystem.BuildLeagueStats` 를 시즌중 조회 public 쿼리로 일반화(득점/도움/평점 + GK 클린시트 + 출전). StandingsScene 탭/섹션(자국 선수 강조). SeasonSummary 폴리싱도 사용.
- **(77-7) 씬 폴리싱**: Dashboard/MainMenu/ClubSelect/Gacha/SeasonSummary MUIP 폴리싱 + SeasonSummary 정보 보강. MUIP 함정(ButtonManager 텍스트 리셋 / CustomDropdown→TMP_Dropdown / UIManagerInputField 제거) 회피. **항목 3 UIManagerInputField NRE 동일 원인 핫픽스 포함.**

**이유:** 전부 V0.5/V1.0 의 미배선·갭·플레이 직결 폴리싱. 콜업/재계약은 로직 기구현 → UI 배선만. 단축키는 안내만 있고 미구현. 보드 이행 보상 부재. WB/AM 분배표 누락. 리더보드 = 재미 요소(데이터 기존).

**영향 범위:** `PlayerNameLinkController` 배선 / `PlayerProfileController`(분기·게이팅·버튼) / `GlobalNavController`(단축키) / `BoardSystem`(보상)+`GameBalanceSO` / `ClubGenerator`+`FormationConfig`+Formation 에셋 / `LeaderboardSystem` 쿼리+StandingsScene / 5씬 폴리싱 / `data-flows.md`(네비/단축키) / `v1.0-tasks.md` Stage R·S.

**확인 (사용자, 2026-06-05):** 안A(구조보존 WB/AM) / 리더보드 5종 / 보드 보상=confidence+reputation / 단축키 매핑 / 콜업=유스한정·재계약=프로필 정보창 / 폴리싱 핵심.

### V1.x 보완 포인트
- 선수명 링크에 호버 미니카드(즉시 요약) — 현재 클릭 진입.
- 단축키 사용자 매핑(Options) / 마일스톤·정찰리포트 알림.

---

## Change Log

| Date | Decision | Note |
| --- | --- | --- |
| 2025-05-15 | Initial decisions 1-22 | Pre-coding design phase |
| 2026-05-18 | #22 Malgun Gothic → NotoSansKR | Task 1.3 진행 중 라이선스/플랫폼 사유로 변경 |
| 2026-05-18 | #23 GameTime 자체 상태 보유 추가 | Task 2.2 작업 시 결정 |
| 2026-05-18 | #24~#26 추가 | algorithms.md #1 Player Generation 명세 작성 시 결정 (CA-Stats 분리 / 트레잇 충돌 그룹 / 2차 포지션 affinity) |
| 2026-05-19 | #27, #28 추가 | algorithms.md #5 Club Generation 명세 작성 시 결정. ratio 화로 가변 clubCount/playersPerClub 대응. V0.5+ 보완 포인트 각 결정에 별도 명시. |
| 2026-05-19 | #29 추가 | Task 2.3 마무리 (#76) 작업 중 GameManager 레이어 결정. `Core → Domain` 정통 의존 방향 복원 (`Domain.asmdef` 미사용 Core 참조 제거 + `Core.asmdef` 에 Domain 추가). 세 문서 (project-context / class-diagram / coding-conventions) Core 로 통일. |
| 2026-05-19 | #28 갱신 + #30~32 추가 | algorithms.md #6 Starting Squad Gacha 명세 작성 시 결정. 분배표 정책 `FormationConfig` 단위로 갱신 (필수 23 + 랜덤 2). Gacha 평가 정책 (4라인 + 명성 대비 + ACE). Reroll 재생성 + 새 id (`GameState.nextPlayerId` 신규). V0.1 단일 포메이션 → V0.5 가챠 랜덤화 확장 경로 명시. 출전 시간 시스템은 V0.5+ 보완 포인트로만 기록. |
| 2026-05-19 | #33, #34 추가 | algorithms.md #2 Match Simulation 명세 작성 (Task 9.1 Sub-A, #109) 시 결정. #33 V0.1 정책 (단순 CA 합 + Poisson + 홈 어드밴티지 + 포지션 라인 가중 득점자). #34 V0.5+ 이벤트 시퀀스 진화 경로 — 옐로 2장/부상→교체/외침 등 누적 처리 가능 구조. 인터페이스 유지로 V0.1 호출자 영향 없이 내부 교체 가능. |
| 2026-05-19 | #33 보강 | Sub-C 본 구현 검증 시 (#113) 단순 선형 ratio 의 결정력 부족 발견 — 강팀 원정 승률 51% 로 디자인 의도 부족. `strengthExponent` (k=1.5 기본) 비선형화 도입. V0.1 임시 변통으로 명시 — V0.5+ 매치 엔진 재작성 (#34) 시 폐기 예정. |
| 2026-05-20 | #35, #36 추가 | algorithms.md #4 Youth Pool Generation 명세 작성 (Task 10 Sub-A, #123). #35 V0.1 정책 (PA 진실값 + CA derived 역방향 / 스타 픽 5% PA bonus / 시드=`currentDate.Ticks`+`userActionHash` 결합으로 외부 마이닝+직플 영상 공유 둘 다 방어 / 시설 통합 등급 / 미영입 단순 제거 / 나이 가중치 / 자국 78%). #36 `GameState.nextIntakeId` 단조증가 카운터 (PlayerGen `nextPlayerId` 패턴). V0.5+ 보완 포인트 9개 정리 (시설 분리 / 포지션 가중치 / AI 영입 / CA-Stats 정합성 / 시드 강화 / 추가 스카우트 / 계약 차등 등). |
| 2026-05-20 | #37 추가 | algorithms.md #3 Market Value + Transfer Flow 명세 작성 (Stage 11 Sub-A, #130). 이적시장 (상시) / 이적시장 활성화 기간 (체결만, 6/1~8/31 + 1/1~1/31) 분리 — 미리 협상 가능 + 체결만 시기 제약. Market Value 6 요소 곱셈 공식 (CA pow 4 + PA gap + age + contract + position + injury) — 슈퍼스타 vs 평범 15.7배 차이 (사용자 의도). V0.1 단일 라운드 / 선수 자동 통과 / AI 영입 미구현. AI 응답 ±10% noise. 용어 정정 ("이적창" → "이적시장 활성화 기간"). V0.5+ 보완 포인트 7+ 항목. |
| 2026-05-20 | #38 추가 | Stage 12 시즌 사이클 명세 작성 (Sub-A, #135). 5/15 종료 / 6/1 회계연도 / 8/15 매치 개막 3 시점 변수명 분리 (혼동 회피). V0.1 도입 — FA 전환 + 33+ 확률적 은퇴 + NewSeasonProcessor (토큰/일정/리셋). V0.1 미구현 — 시상 / 보드 평가 / 재정 결산 / 사기 정산 / Match 압축 (모두 V0.5+ 별도 시스템과 짝). 캘린더/요일 dynamic 계산은 V0.5+ ("5월 마지막 토요일" 같은 — 매년 가변 일정). V0.5+ 보완 포인트 10 항목. |
| 2026-05-20 | #38 보강 | Stage 15 통합 테스트 (#59) 작성 시 GameInitializer 가 첫 매치를 seasonStart 당일에 배치 → GameLoop.AdvanceDay 가 시간 진행 후 처리하므로 영원히 미처리 발견. **프리시즌 컨셉 도입**: `seasonStart` = 프리시즌 시작일 (state.currentDate 초기값). 첫 매치 = `newSeasonOpening` (8/15) 부터. GameInitializer.NewGame 이 `firstMatchDate = seasonStart 이후 가장 가까운 newSeasonOpening` 계산 후 ScheduleGenerator 호출. 사용자 합의: "원래 FM 도 프리시즌부터 시작해서 팀 뽑고 전술 / 스탭 만지고 첫 경기 시작할 시간을 줘야 한다". NewSeasonProcessor 는 이미 동일 패턴 (`ComputeNewSeasonOpeningDate` 사용) — 일관성 확보. |
| 2026-05-22 | #39~#52 추가 | V0.1 빌드 마무리 후 V0.5 계획 수립 (`docs/v0.5-plan.md` 작성). 사용자 플레이테스트 피드백 11 카테고리 + 기존 V0.5+ 보완 포인트 + FM 표준 통합. 12 Open Questions 모두 결정 후 본 결정사항 #39~#52 추가. **§ 매핑**: #39 Stats 1-100 + FM 49 (Q1, Q12) / #40 Hidden Attributes (Q4) / #41 Trait 효과 본격화 / #42 Morale + Happiness 분리 (Q7) / #43 Promise + 면담 (Q7) / #44 매치 엔진 V0.5 분 단위 (#34 실현, Q5) / #45 Tactic 중간 스코프 (Q10) / #46 스카우트 이분법 (Q4) / #47 CpuTransferAi 필요 기반 (Q3) / #48 협상 V0.5 + 임대 / #49 시설 8종 × 10단계 + 병렬 / #50 유스 V0.5 (CA 캡 + 시설 분리 + Mentoring) / #51 시즌 V0.5 (시상 + 보드 + 재정) / #52 인프라 (String Table + Localization + Save Migration, Q8). 일정 정책 (Q11) = 마감 없음. |
| 2026-05-26 | #53 추가 | Stage D.4 Sub-A 명세 (`algorithms.md` V0.5-10 + V0.5-11 와 짝). 시설 효과 본격 적용 — Training (Player Growth System 신규) + Medical (Injury Recovery + Rate 보정) + Gym (피지컬 성장 보조 + 회복 일부). Stadium / Scout / Youth* 은 D.4 책임 X — 후속 Stage M.6 / E.2 / L.1-3 의존. 성장 시스템 = 매월 1일 / 2단계 모델 (발생 확률 + size 분포 +1/+2/+3) / Relative only (Absolute ×0.10) / 나이 곡선 4단계 (16-22 peak / 23-26 prime / 27-30 정체 / 31+ decline) / PA 캡. 결정성 시드 = `state.randomSeed ^ player.id ^ (year×12 + month)`. CA = static (V1.0 derived 검토). 부상 회복 결정성 = 발생 시점 `expectedReturn` 고정. 발생률 floor 0.5 (Medical Lv10 도 부상 완전 차단 불가). |
| 2026-05-26 | #53 보강 | 성장 size 분포 도입. V0.5-10 의 초안 (`+1` 단위만) → 사용자 지적 ("특정 스탯 +2 가능") 반영. **2단계 모델**: (1) 발생 확률 `growthBaseChance = 0.01` (월 1% — 초안 0.05 너무 빈번해서 1/5 로 낮춤. 49 stat × 1% = 평범 선수 1년 ~6 stat 변동). (2) 발생 시 size 추첨 `[+1, +2, +3]` 분포 `[75, 20, 5]`. peak youth (ageFactor ≥ 1.3, 16-18세) 는 큰 점프 분포 `[60, 30, 10]`. decline 대칭. 18세 wonderkid 1년 stat 합산 ~12 (peak 추정), 평범 25세 ~5. FM 표준 (15-20 wonderkid / 5-10 평범) 와 일치. |
| 2026-06-01 | #58 정정 | Stage W Sub-A (#416). GlobalNav 생명주기 모순 해소 — 초안의 "싱글톤 + DontDestroyOnLoad" 와 "모든 씬 baked-in" 양립 불가 (중복 생성 + 제외 씬 정리 로직 필요). **씬별 인스턴스 모델 채택** (사용자 결정): 각 씬 baked-in, DDOL 안 함, 상태 비보유 (매 씬 `GameManager.State` 재조회), 제외 씬은 prefab 미포함. `v1.0-plan §3.19.5–6` / `class-diagram.md` Presentation Layer / `data-flows.md §8` / `muip-reference §17` 동기 갱신. TopBar 아이콘 이모지 금지 함정 (NotoSansKR 미지원) 기록. |
| 2026-05-26 | #47 V0.5+ 보완 포인트 2 항목 추가 | F.1+F.2 머지 (#295) 직후 사용자 지적. 현재 V0.5 한계 2가지 명세화 — (1) "매주 월요일 모든 AI 구단 동시" = 비자연스러운 동기화. (2) "구단당 주 1 오퍼" = 여름 윈도우 대규모 리빌딩 시나리오 X. V1.0 진화 = 구단별 cooldown (`Club.lastTransferAttemptDate`) + `DetectTrigger` → `DetectTriggers` (복수) + 자금 트리거별 분배. `aiPersonality` 와 결합 시 FM 식 비동기 + 다발 협상. 결정성 시드는 `lastAttemptDate.Ticks` 로 클럽별 독립 재현성 확보. V0.5 본문 정책 (매주 호출) 은 그대로 유지 — V1.0 스코프. |
| 2026-05-28 | #57 추가 | Stage J.4 TacticImpact (#341). `Application/TacticImpact.cs` 신규 — Role×Duty×Stat 이벤트 주체 *선택* 가중치 (`MatchSimulator.SnapPlayer` 가중 추첨). Mentality 제외 (J.3 zone 전이 중복 + 같은 팀 상쇄) / 외부영향 제외 (Eff 성공률 중복) → double-counting 방지. Duty 가중치 = `GameBalanceSO.tacticDuty*` 4필드 외부화 (#11, `balance` 파라미터 — 원 스펙 시그니처와 일치) / Role = `PlayerRoleSO.eventModifiers` 외부화 / stat 분모(10000)만 구조적 상수. `HasLineup` 가드 (assignedPlayerId 미배정 시 균등 추첨 → T1~T12 회귀 0, J.5 라인업 후 본격 작동). `algorithms.md` V0.5-7 실제 코드 정합 갱신 (string eventType / roleId / mentality·external 제외 / T2=T12·T3=T13 대체). **검증**: T1/T4 = ComputeEventWeight **가중치 비율** 정밀 검증 (2.0/3.0 — 스펙 "~2×/~3×" 의 실체) + 통합 테스트 방향성 (emergent 슛 카운트는 zone 동학으로 증폭되어 정확 비율 비검증). |
| 2026-05-27 | #17 V0.1 한정 표시 + #34 갱신 + #44 전면 개정 + #54/#55/#56 신규 | openfootmanager(OFM) 매치 엔진 분석 후 Stage I 5-zone Markov 재설계 (이슈 #319, Sub-A 명세). **#17** "결과 미리 산출" V0.5 완전 폐기 — forward simulation, 결정성은 시드 고정에서만. **#34** 5-zone Markov 채택 명시 (초안 "양 팀 독립 추첨" 폐기 근거). **#44** 분 단위 독립 → 5-zone Markov 상태 전이 전면 개정 (ballZone + possession, 49 stat zone별 매핑, OFM 18→FM 49). **#54** fatigue 임계 (>50 경기력↓ / >40 부상↑, OFM 선형 대체 — 과도 로테이션 방지). **#55** 5-zone + background 동일 엔진 (collectEvents 플래그, 통계 양쪽 수집, 연산 부담 0 검증). **#56** 컵 대회 + 연장/승부차기 V0.5 스코프 확대 (I.11 연장 + Stage Q 컵). `algorithms.md` V0.5-2 재작성 + `v0.5-tasks.md` Stage I 재구성 + Stage Q 신규와 짝. |
| 2026-05-29 | #58~#65 추가 | V0.5 빌드 마무리 후 V1.0 계획 수립 (`docs/v1.0-plan.md` 보강). 사용자 V0.5 플레이테스트 피드백 + 추가 요청 (Unity MCP / 매치 결과 대시보드 / 훈련 / 비교 도구 / Options / 통화) 통합. 12 Open Questions 모두 결정 + 추가 결정사항 흡수. **§ 매핑**: #58 글로벌 네비 (TopBar + SideBar 영구 레이어 — 사용자 핵심 피드백 "씬 전환되도 기본 버튼 고정 위치") / #59 Options (PlayerPrefs + AudioMixer + 4 카테고리: 사운드 / 언어 / 통화 / UI Scale / 자동 저장 / 단축키) / #60 사운드 (무료 라이센스 + AudioMixer + CREDITS.md, BGM 3 + SFX 12) / #61 통화 (GBP base 고정 환율, 표시 변환만) / #62 매치 디테일 V1.0 (viewMode 폐기 + 모든 핵심 이벤트 텍스트 + 5-Zone 골 빈도 P0 밸런싱) / #63 훈련 시스템 (개인 + 그룹 + GrowthSystem 통합) / #64 V1.0 정책 (Save Migration 무효 + 일정 마감 없음 + DOTween V1.x 미루기) / #65 Unity MCP (Stage 0 첫 작업 + 4단계 fallback, `unity-mcp-setup.md` 별도 명세). |
| 2026-05-31 | #66 추가 | Task A.1 Sub-A — Inbox 도메인 + 정책 (V1.0). InboxItem 도메인 클래스 / GameState 확장 / InboxRouter (10 이벤트 흡수) / 정책 Q1(기한 만료 비효과) + Q2(시즌 종료 정리) + Q3(YouthIntakeAvailableEvent 정지 제거). `algorithms.md` V1.0-7 참조 번호 #68 → #66 정정. |
| 2026-05-31 | #67 추가 | Task A.2 Sub-A — Player.physical 신체 조건 도메인 (V1.0). PhysicalAttributes 클래스 + PlayerGenerator GeneratePhysical 단계. `algorithms.md` V1.0-8 참조 번호 #70 → #67 정정. 명명 혼동 주의 (`stats.physical` vs `Player.physical`). |
| 2026-05-31 | #68 추가 | Task A.3 Sub-A — Player.growthHistory + GrowthSystem 월별 스냅샷 (V1.0). StatSnapshot 도메인 / Stats.Clone() / GrowthSystem.Tick 스냅샷 단계 + GetStatChange 헬퍼. `algorithms.md` V1.0-11 신규. |
| 2026-06-04 | G.3/G.4 통합 (#478) | 시너지·포메이션 상성 = 단일 teamMod(Π시너지×매치업). **xG 비율에만 적용** 결정 — 모든 Eff 곱셈은 점유 독점 폭주+비단조+xG/골 불일치 (측정 확인) → xG-only 가 단조·bounded·골 직접반영·비폭주. `ResolveShotXg` rng outcome-무관 2회 소비로 결정성 강화(페어드 비교 정상화). SynergyCondition 확장(List<Position>/minCount/sameNat). `algorithms.md` V1.0-3/9. |
| 2026-06-04 | #71 추가 | #474 후속 (Stage H) — 포메이션 기반 라인업 선정 + AI 자동 라인업(노이즈) 명세 예약. 현 CA-top11 포지션무시 폴백 폐기 예정 / 유저 라인업 미지정 시 매치 차단 / AI 포메이션 정합+노이즈. `StartingEleven_*` 기존 실패 2건 근본원인 = 포지션 무시 폴백. PR1 에선 명세만 + 테스트 [Ignore]. `algorithms.md` V1.0-14. |
| 2026-06-04 | #70 추가 | Stage G (#474) — 매치 엔진 xG 찬스-퀄리티 레이어 + 평점 재설계. (1) G.5 재밸런싱 = 블런트 4-파라미터 튜닝 폐기 → chanceType(ClearChance/OpenPlay/Header/LongShot/DirectFreeKick)별 baseXG, 기록 xG=찬스품질, 골=xG×finishMod×gkMod. E[goals]≈ΣxG 로 2.7 직접 산정. (2) 평점 전면 재설계 (FM 리서치 정합) — 포지션이 평점을 만든다: 패스성공률 티어/수비액션·클리어 가치↑/DF 무실점·실점 공유/xG 보정(clinical +/빅찬스 미스 −, 사용자 #74 요청)/goalBonus 1.0→0.8. (3) G.2 신체영향(헤더·agility·pace·발일치) xG 통합 (4) G.1 이벤트(Offside/ThrowIn/KeeperPunch/LongShot). 신규 통계 xg/bigChancesMissed/clearances + MatchResult.shotMap/zoneOccupancy(AA.1/AA.2 선당김). `algorithms.md` V1.0-1 재작성. 5-Zone 구조·인터페이스·결정성 유지. |
| 2026-06-03 | #69 추가 | Stage F 선행 (#469) — 이적 흐름 FM 2단계 재구성. (1) 이적료 협상(역제안): `AiRespondToOffer` 의 `ratio≥1.30` 위장 CounterOffer → `Negotiating`(구단 합의). (2) 선수 개인협상: `Negotiating` 단계를 실제 인터랙티브 구현 — 신규 `RespondToPersonalTerms`(반복 협상, `maxPersonalNegotiationRounds=4`) + `PlayerNegotiationController`/`PlayerNegotiationScene`(신규). AI 구매 구단은 `AutoResolveAiPersonalTerms` 자동. 오퍼=이적료만(`TransferController`). 오퍼 결과 인박스(Negotiating/Accepted/Rejected) + EventScheduler Negotiating 정지. `offerItemPrefab` 미연결(#4) 수정. 신규 로컬라이즈 키 (`inbox_personal_negotiation_fmt` / `pnego_*` / `inbox_offer_*`) — 시드 갱신 별도 chore. `PersonalTermsResult` enum / `TransferOffer.personalNegotiationRound` 신규. |
| 2026-06-05 | #72~#77 추가 | V1.0 플레이테스트 피드백 18항목 트리아지 후 결정 (`v1.0-plan` 외 별도 — 코드 조사 5클러스터 + FM 웹 리서치 기반). #72 슈퍼유망주 동적 라벨(효과 X, CA≥110·21세 이하 매월 재평가·회수). #73 Trait 가시성 3-tier(Concealed/Public/ScoutGated)+검색 폐지(스카우팅 무력화 회피). #74 재정 밸런싱(주급 월차감 신설+수입/임금 63% 비율 정상화+시설비 상향+측정 하네스 — 단순 가격상향 아닌 현금흐름 정상화). #75 명성 기반 선수 수락(구단=ratio 유지, 선수 개인협상에 영입구단 명성격차 항, 임금 보상 가능). #76 인박스 대확장(`InboxCategory.League` append + Tier1 라우팅[부상/시상/유스성장]+Tier2 핵심 신규이벤트[불만/피로/순위변동/부상복귀/계약만료], 핵심만 V1.0). #77 UX 보강 묶음(글로벌 선수명 링크+프로필 가시성 게이팅 / 프로필 액션버튼[재계약 자기팀·콜업 유스한정] / 전역 단축키[Esc·Ctrl+S 컨텐츠씬·Space Dashboard] / 보드 이행 보상[confidence+reputation] / WB·AM squad 1급 슬롯 편입[구조보존] / 리그 리더보드[득점·도움·평점·클린시트·출전] / 5씬 MUIP 폴리싱+항목3 UIManagerInputField NRE 핫픽스). `algorithms.md` V1.0-11/15/16 + 3.1, `event-bus-catalog.md` 신규 이벤트, `v1.0-tasks.md` Stage R/S 와 짝. |
