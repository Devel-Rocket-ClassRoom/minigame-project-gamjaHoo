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

## 28. 초기 스쿼드 — 고정 포지션 분배표 (V0.1)

**결정:** V0.1 에서 모든 구단이 같은 포지션 분배표로 25명 스쿼드 생성. 구단별 색깔(전술 프리셋 / 스카우팅 편향)은 V1.0+ 도입.

```
GK 3 / CB 4 / LB 2 / RB 2
DM 2 / CM 3 / AM 2 / LM 1 / RM 1
LW 1 / RW 1 / ST 2 / CF 1   = 25
```

**이유:**
- V0.1 단순화. "초기 스쿼드가 라인업 못 짤 정도로 빈약" 같은 시스템 오류 회피.
- 외부화 (`GameBalanceSO.squadGK ~ squadCF` 13개 필드) → 1줄 수정으로 밸런싱 가능.

**가변 `playersPerClub` 대응:** 분배표 합(25) ≠ `LeagueConfigSO.playersPerClub` 일 경우 V0.1 은 **분배표 합 기준으로 진행 + 경고**. ratio 변환은 V1.0 보완 포인트.

### V1.0+ 보완 포인트

- **ratio 화** — 현재 13개 절대 int. V1.0 에서 float ratio (Σ=1.0) 로 전환해 `playersPerClub` 변화에 자동 정합 (15명 리그, 30명 리그 등).
- **전술 프리셋별 분배표** — 4-3-3 / 4-4-2 / 3-5-2 포메이션별 분배표 도입. 새 `TacticPresetSO` 와 연결.
- **구단별 색깔** — 명성·예산·유스 시설에 따라 스쿼드 편향 (빅클럽=veteran/외국인 ↑, 강등권=youth/자국인 ↑).
- **homegrown 시설 연동** — 현재 모든 구단 20% 고정. 유스 시설 Lv5 → 35%, Lv1 → 10% 등 시설 연동.

---

## Change Log

| Date | Decision | Note |
| --- | --- | --- |
| 2025-05-15 | Initial decisions 1-22 | Pre-coding design phase |
| 2026-05-18 | #22 Malgun Gothic → NotoSansKR | Task 1.3 진행 중 라이선스/플랫폼 사유로 변경 |
| 2026-05-18 | #23 GameTime 자체 상태 보유 추가 | Task 2.2 작업 시 결정 |
| 2026-05-18 | #24~#26 추가 | algorithms.md #1 Player Generation 명세 작성 시 결정 (CA-Stats 분리 / 트레잇 충돌 그룹 / 2차 포지션 affinity) |
| 2026-05-19 | #27, #28 추가 | algorithms.md #5 Club Generation 명세 작성 시 결정. ratio 화로 가변 clubCount/playersPerClub 대응. V1.0+ 보완 포인트 각 결정에 별도 명시. |
