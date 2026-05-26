# EventBus Catalog

게임 내 발행되는 모든 이벤트의 목록. 각 이벤트의 발행자 / 구독자 / 페이로드 정의.

새 이벤트 추가 시 이 문서에 등록.

---

## EventBus API

```csharp
public static class EventBus {
    public static void Publish<T>(T evt) { ... }
    public static void Subscribe<T>(Action<T> handler) { ... }
    public static void Unsubscribe<T>(Action<T> handler) { ... }
}
```

## Naming Convention

- 이벤트 클래스: `XxxEvent` 접미사
- 과거형 (이미 일어난 일을 알림): `MatchFinishedEvent`, `TransferCompletedEvent`
- 진행 중 알림은 `XxxStartedEvent`, `XxxRequestedEvent`

## File Location

```
Assets/_Project/Scripts/Core/Events/
├─ GameEvents.cs       (게임 메타 이벤트)        [V0.1 작성됨]
├─ MatchEvents.cs      (경기 관련)               [V0.1 일부 — MatchDayEvent 만, FinishedEvent 는 Stage 9]
├─ TransferEvents.cs   (이적 관련)               [Stage 11 작업 시 작성]
├─ YouthEvents.cs      (유스 관련)               [Stage 10 작업 시 작성]
└─ SeasonEvents.cs     (시즌 관련)               [Stage 12 작업 시 작성]
```

> **V0.1 진행 상태**:
> - `GameEvents.cs` — DayAdvancedEvent / GameLoadedEvent / GameSavedEvent ✓
> - `MatchEvents.cs` — MatchDayEvent ✓ (Stage 8 Task 8.1, #103) / MatchFinishedEvent ✓ (Stage 9 Task 9.2, #117). PlayerInjuredEvent 는 V1.0+ (부상 시스템 도입 시).
> - `YouthEvents.cs` — 3 이벤트 ✓ (Stage 10 Sub-B, PR #125).
> - `TransferEvents.cs` — 3 이벤트 ✓ (Stage 11 Sub-B, PR #132).
> - `SeasonEvents.cs` — 명세 확정 (#135 Sub-A, 2026-05-20). Stage 12 Sub-B 에서 코드 추가 (2 이벤트: SeasonStartedEvent / SeasonEndedEvent. BoardReviewEvent 는 V1.0+ 보드 시스템).
> - `TokenGrantedEvent` — Youth 와 Facility 양쪽에서 발행. Stage 12 작업 시 통합 검토.

---

## V0.1 Event Catalog

### Game Meta Events

#### GameLoadedEvent
- **Publisher:** SaveSystem
- **Subscribers:** All UI screens (refresh)
- **Payload:** 없음
- **Trigger:** 세이브 파일 로드 완료 직후

```csharp
public class GameLoadedEvent { }
```

#### GameSavedEvent
- **Publisher:** SaveSystem
- **Subscribers:** UI (저장 완료 토스트)
- **Payload:** 슬롯명
- **Trigger:** 세이브 완료 직후

```csharp
public class GameSavedEvent {
    public string slotName;
}
```

#### DayAdvancedEvent
- **Publisher:** GameTime
- **Subscribers:** UI (날짜 표시 갱신), DailyProcessor
- **Payload:** 새 날짜
- **Trigger:** 하루 진행 시

```csharp
public class DayAdvancedEvent {
    public DateTime newDate;
}
```

---

### Match Events

#### MatchDayEvent
- **Publisher:** EventScheduler
- **Subscribers:** UI (경기 준비 화면 띄움), GameManager (정지 신호)
- **Payload:** 오늘 경기 목록
- **Trigger:** 유저 구단 경기일 도래

```csharp
public class MatchDayEvent {
    public List<int> matchIds;
    public bool isUserMatch;
}
```

#### MatchFinishedEvent
- **Publisher:** MatchPostProcessor
- **Subscribers:** UI (결과 화면), LeagueStandingsView (순위 갱신)
- **Payload:** 매치 ID, 결과
- **Trigger:** 경기 시뮬 + 결과 적용 완료 후

```csharp
public class MatchFinishedEvent {
    public int matchId;
    public MatchResult result;
}
```

#### PlayerInjuredEvent (V1.0+)
- **Publisher:** MatchPostProcessor
- **Subscribers:** UI (알림), SquadView
- **Payload:** 선수 ID, 부상 정보
- **Trigger:** 경기 중 부상 발생

```csharp
public class PlayerInjuredEvent {
    public int playerId;
    public InjuryInfo injury;
}
```

#### PlayerInjuryRecoveredEvent (V1.0 D.4)
- **Publisher:** `InjurySystem.ProcessRecovery` (DailyProcessor 호출)
- **Subscribers:** UI (인박스 알림), SquadView (출전 가능 상태 갱신)
- **Payload:** 선수 ID
- **Trigger:** `expectedReturn` 도래 → 부상 sentinel 리셋

```csharp
public class PlayerInjuryRecoveredEvent {
    public int playerId;
}
```

#### PlayerStatChangedEvent (V1.0 D.4)
- **Publisher:** `GrowthSystem.Tick` (매월 1일)
- **Subscribers:** UI (선수 프로필 알림 — V1.x 인박스 / 토스트)
- **Payload:** 선수 ID, stat 이름, 이전 값, 새 값
- **Trigger:** Growth/Decline 발생 중 **size ≥ 2** (큰 점프만 발행 — `+1` 노이즈 회피)

```csharp
public class PlayerStatChangedEvent {
    public int playerId;
    public string statName;
    public int oldValue;
    public int newValue;
}
```

---

### Transfer Events

#### OfferSubmittedEvent
- **Publisher:** TransferSystem
- **Subscribers:** UI (확인 토스트)
- **Payload:** 오퍼 ID
- **Trigger:** 유저가 오퍼 제출

```csharp
public class OfferSubmittedEvent {
    public int offerId;
}
```

#### OfferRespondedEvent
- **Publisher:** TransferSystem.ProcessOffers
- **Subscribers:** UI (알림), InboxView
- **Payload:** 오퍼 ID, 응답 종류
- **Trigger:** AI 구단이 오퍼에 응답

```csharp
public class OfferRespondedEvent {
    public int offerId;
    public OfferStatus newStatus;
}
```

#### TransferCompletedEvent
- **Publisher:** TransferSystem
- **Subscribers:** SquadView, FinanceView, all relevant UI
- **Payload:** 오퍼 ID, 선수 ID, 양 구단 ID
- **Trigger:** 이적 최종 체결

```csharp
public class TransferCompletedEvent {
    public int offerId;
    public int playerId;
    public int fromClubId;
    public int toClubId;
    public int amount;
}
```

#### TransferRequestEvent (V1.0 G.1)
- **Publisher:** `MoraleSystem.OnPromiseBroken` (Happiness 가 `transferRequestThreshold` 미만으로 떨어진 시점)
- **Subscribers:** Dashboard 인박스 / TransferController (Q9 유저 승인 패턴 — V1.0 G.4 / K.4 UI 와 짝)
- **Payload:** 선수 ID
- **Trigger:** 약속 미이행 / 출전시간 미달 등으로 Happiness < 20

```csharp
public class TransferRequestEvent {
    public int playerId;
}
```

---

### Youth Events

#### YouthIntakeAvailableEvent
- **Publisher:** YouthSystem.GenerateIntake
- **Subscribers:** UI (유스 풀 화면 자동 띄움), GameManager (정지 신호)
- **Payload:** YouthIntake 객체
- **Trigger:** 유스 인스펙션일 도래 + 풀 생성 완료

```csharp
public class YouthIntakeAvailableEvent {
    public int intakeId;
    public int clubId;
}
```

#### YouthRerolledEvent
- **Publisher:** YouthSystem.UseRerollToken
- **Subscribers:** UI (유스 풀 화면 갱신), TokenDisplay (토큰 수 갱신)
- **Payload:** 인테이크 ID, 남은 토큰
- **Trigger:** 리롤 토큰 사용

```csharp
public class YouthRerolledEvent {
    public int intakeId;
    public int remainingTokens;
}
```

#### YouthSignedEvent
- **Publisher:** YouthSystem
- **Subscribers:** SquadView, InboxView
- **Payload:** 영입된 선수 ID들
- **Trigger:** 유저가 유스 영입 확정

```csharp
public class YouthSignedEvent {
    public int intakeId;
    public List<int> signedPlayerIds;
}
```

#### TokenGrantedEvent
- **Publisher:** GameManager / FacilityUpgradeProcessor
- **Subscribers:** UI (토스트 알림), TokenDisplay
- **Payload:** 지급된 개수, 사유
- **Trigger:** 시즌 시작 / 시설 업그레이드 / 보드 미션 달성

```csharp
public class TokenGrantedEvent {
    public int amount;
    public string reason;
    public int newTotal;
}
```

---

### Season Events

#### SeasonStartedEvent
- **Publisher:** SeasonEndProcessor (신규 시즌 시작 시)
- **Subscribers:** UI (시즌 목표 화면), all displays
- **Payload:** 시즌 연도, 새 시즌 목표
- **Trigger:** 회계연도 시작 (6/1)

```csharp
public class SeasonStartedEvent {
    public int seasonYear;
    public SeasonState target;
}
```

#### SeasonEndedEvent
- **Publisher:** SeasonEndProcessor
- **Subscribers:** UI (시즌 요약 화면), GameManager (정지 신호)
- **Payload:** 시즌 결과 요약
- **Trigger:** 시즌 마지막 경기 종료

```csharp
public class SeasonEndedEvent {
    public int seasonYear;
    public SeasonSummary summary;
}
```

#### BoardReviewEvent
- **Publisher:** EventScheduler
- **Subscribers:** UI (보드 리뷰 화면), GameManager (정지 신호)
- **Payload:** 리뷰 단계 (1/4, 2/4, 3/4, 시즌 종합)
- **Trigger:** 보드 리뷰일 도래

```csharp
public class BoardReviewEvent {
    public BoardReviewStage stage;
    public int currentConfidence;
}
```

---

### Facility Events (V0.1)

#### FacilityUpgradeStartedEvent
- **Publisher:** FacilitySystem
- **Subscribers:** UI (시설 화면), FinanceView
- **Payload:** 시설 종류, 새 등급, 완료일
- **Trigger:** 유저가 업그레이드 발주

```csharp
public class FacilityUpgradeStartedEvent {
    public FacilityType type;
    public int newLevel;
    public DateTime completionDate;
}
```

#### FacilityUpgradeCompletedEvent
- **Publisher:** FacilitySystem
- **Subscribers:** UI (알림), TokenSystem (토큰 +1 트리거)
- **Payload:** 시설 종류, 새 등급
- **Trigger:** 업그레이드 완료일 도래

```csharp
public class FacilityUpgradeCompletedEvent {
    public FacilityType type;
    public int newLevel;
}
```

---

## V1.0+ Events (Future)

V1.0 작업 시 추가될 이벤트들:

- ~~`TransferRequestEvent`~~ — V1.0 G.1 등록 완료 (Transfer Events 섹션)
- `PromiseCreatedEvent / PromiseFulfilledEvent / PromiseBrokenEvent` (V1.0 G.2)
- `ContractRenewedEvent` (V1.0 H.1)
- `PlayerMoraleChangedEvent` (사기 시스템 — 큰 변동 시점만 발행 검토, V1.x)
- `BoardConfidenceChangedEvent` (보드 신뢰도, V1.0 M.4)
- `ManagerSackedEvent` (경질, V1.0 M.4)
- `MatchEventOccurredEvent` (경기 중 텍스트 이벤트, V1.0 I.5)
- `PressConferenceEvent` (기자회견, V1.x)

---

## Subscription Lifecycle

### MonoBehaviour Pattern

```csharp
public class SomeView : MonoBehaviour {
    void OnEnable() {
        EventBus.Subscribe<MatchFinishedEvent>(OnMatchFinished);
    }
    
    void OnDisable() {
        EventBus.Unsubscribe<MatchFinishedEvent>(OnMatchFinished);
    }
    
    private void OnMatchFinished(MatchFinishedEvent e) {
        Refresh();
    }
}
```

### 항상 Subscribe / Unsubscribe 짝맞추기

메모리 누수 방지. 특히 씬 전환 시 중요.

### 발행자는 구독자 알 필요 없음

EventBus의 핵심 원칙. 시스템은 "이런 일이 일어났다"만 알리고, 누가 듣는지 모름.

---

## Anti-Patterns

### ❌ 이벤트로 명령 전달

```csharp
// 나쁨: 명령은 직접 호출
EventBus.Publish(new SaveGameCommand()); 
```

```csharp
// 좋음: 명령은 직접
SaveSystem.Save(state, slotName);

// 이벤트는 결과 알림용
EventBus.Publish(new GameSavedEvent { slotName = slotName });
```

### ❌ 발행자가 구독자 가정

```csharp
// 나쁨
public class TransferSystem {
    public void Complete(...) {
        // ...
        UIManager.Instance.RefreshSquadView(); // 직접 호출은 결합도 ↑
    }
}
```

```csharp
// 좋음
public class TransferSystem {
    public void Complete(...) {
        // ...
        EventBus.Publish(new TransferCompletedEvent(...));
    }
}
```

### ❌ 이벤트 안에 큰 객체

```csharp
// 나쁨: 객체 통째로 넣기
public class PlayerUpdatedEvent {
    public Player player; // 전체 직렬화 위험
}
```

```csharp
// 좋음: ID만
public class PlayerUpdatedEvent {
    public int playerId;
}
// 구독자는 state.GetPlayer(id)로 조회
```

---

## Change Log

| Date | Change |
| --- | --- |
| 2025-05-15 | V0.1 카탈로그 초안 작성 |
| 2026-05-26 | V1.0 G.1 — TransferRequestEvent 등록 (MoraleSystem.OnPromiseBroken 발행, Happiness < 20). V1.0+ Future 섹션 갱신 (G.2 Promise* / H.1 ContractRenewed / M.4 Board* / I.5 MatchEvent / V1.x Press 분류). |
