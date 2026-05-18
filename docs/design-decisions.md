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

## 22. 폰트 — Malgun Gothic

**결정:** 한국어 UI는 Malgun Gothic 베이스로 시작.

**이유:**
- Windows 기본 폰트 (호환성)
- 가독성 양호

**대체:** V1.x에서 Pretendard 등 더 나은 폰트 고려.

---

## Change Log

| Date | Decision | Note |
| --- | --- | --- |
| 2025-05-15 | Initial decisions 1-22 | Pre-coding design phase |
