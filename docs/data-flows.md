# Data Flows

주요 시스템의 데이터 흐름과 호출 시퀀스. 코드 구현 시 이 문서를 참조한다.

`design-decisions.md`의 결정사항을 전제로 작성됨.

---

## 1. 게임 시작 흐름

### Trigger
- 메인 메뉴 → "New Game" 클릭
- 또는 "Load Game" → 세이브 슬롯 선택

### Sequence (New Game)

```
[1] UI: 시드 입력 또는 자동 생성
       시드는 GameState.randomSeed에 저장

[2] GameInitializer (Application Layer):
    a. 빈 GameState 생성
    b. LeagueConfigSO 로드 → League 인스턴스 생성
    c. ClubGenerator.Generate(rng, leagueConfig, balance, ...):
       - 명성별 Club 인스턴스 생성 (clubCount 기본 20, 가변 대응 — algorithms.md #5)
       - 내부에서 PlayerGenerator 호출 (각 구단당 playersPerClub 명)
       - 반환: ClubGenerationResult { List<Club> Clubs, List<Player> Players }
    d. GameInitializer 가 GameState 에 일괄 등록:
       - foreach club in result.Clubs:  state.AddClub(club)
       - foreach player in result.Players: state.AddPlayer(player)
    e. ScheduleGenerator: 시즌 일정 생성 → League.schedule 추가
    f. GameState.BuildIndexes() 호출 (AddClub/AddPlayer 가 인덱스 동기화하므로 사실상 no-op, 안전망)
    g. GameState.currentDate = 시즌 시작일

[3] UI: 구단 선택 화면 표시
       유저가 구단 선택 → GameState.userClubId 설정

[4] StartingSquadGacha 시스템 실행:
    a. 선택 구단의 스쿼드를 4라인 티어로 평가
    b. UI에 표시 (정확한 수치 숨김)
    c. 유저가 리롤 결정 (최대 3회)
       - 리롤 시 해당 구단 선수만 재생성 (PlayerGenerator 재호출)
       - 다른 구단은 그대로
    d. 유저가 "확정" 클릭

[5] SaveSystem: 초기 세이브 자동 생성

[6] 메인 게임 화면으로 전환
```

### Sequence (Load Game)

```
[1] UI: 세이브 슬롯 목록 표시 → 유저 선택

[2] SaveSystem.Load(slot):
    a. JSON 파일 읽기
    b. GameState 역직렬화
    c. GameState.BuildIndexes() 호출 (런타임 인덱스 재빌드)
    d. EventBus.Publish(new GameLoadedEvent())

[3] 메인 게임 화면으로 전환
```

### Key Points
- 게임 시작 시 모든 구단의 선수 일괄 생성. 시간 소요 있을 수 있음 → UI에 로딩 표시.
- 시드는 모든 랜덤 생성의 기반. 같은 시드 = 같은 초기 상태.
- Load 후 인덱스 재빌드 필수 (`[JsonIgnore]` 필드들).

---

## 2. 하루 진행 흐름

### Trigger
- UI: "Continue" / "Next Day" 버튼

### Sequence

```
[1] GameTime.Advance(1):
    GameState.currentDate += 1 day
    EventBus.Publish(new DayAdvancedEvent(newDate))

[2] DailyProcessor (Application Layer): 백그라운드 처리
    a. 모든 활성 선수 컨디션 회복 (+소량)
    b. 부상자 회복일 카운트다운
    c. 계약 만료일 카운트다운 → 임계값 도달 시 알림 이벤트

[3] EventScheduler: 오늘 발생할 이벤트 체크
    오늘 이벤트가 있으면 처리 (아래 분기):

    [3-a] 경기일 → MatchDay 흐름 진입
    [3-b] 이적창 오픈/마감 → TransferWindow 흐름
    [3-c] 유스 인스펙션일 → YouthIntake 흐름
    [3-d] 보드 리뷰일 → BoardReview 흐름
    [3-e] 시설 업그레이드 완료일 → 시설 적용
    [3-f] 시즌 종료일 → SeasonEnd 흐름

[4] BackgroundSimulator: 비활성 구단 경량 처리
    (경기일이면 다른 리그의 경기들도 시뮬)

[5] UI 갱신:
    EventBus가 발행한 이벤트들을 UI가 구독해서 표시

[6] 유저 입력 대기:
    - 강제 정지 이벤트 (보드 리뷰, 경기일 등) 있으면 멈춤
    - 아니면 자동으로 다음 날 진행 가능
```

### Key Points
- 시간 진행은 결정적 (turn-based). 매 프레임 호출이 아님.
- 강제 정지 이벤트는 EventScheduler가 발행. UI는 이를 듣고 "Continue" 버튼 비활성화.
- 백그라운드 시뮬은 같은 트랜잭션 내에서 처리 (세이브 일관성).

---

## 3. 경기 시뮬 흐름

### Trigger
- 하루 진행 시 경기일 이벤트 발생
- 유저 구단 경기 = 강제 정지 (라인업 결정)
- 비활성 구단 경기 = 자동 시뮬

### Sequence (유저 구단 경기)

```
[1] EventScheduler: MatchDayEvent 발행 → 정지

[2] UI: 경기 준비 화면
    a. 자동 추천 라인업 표시
    b. 전술 프리셋 선택 (V0.1: 1개 디폴트)
    c. 유저가 라인업/전술 조정
    d. 유저가 "Play Match" 클릭

[3] MatchSimulator.Simulate(match, state):
    a. 시드 고정: rng = new Random(match.id ^ state.randomSeed)
    b. 결과 산출 (V0.1):
       - 양 팀 전력 계산
       - 골 분포 샘플링 → 스코어 결정
       - 득점자 선정
    c. MatchResult 반환

[4] MatchPostProcessor: 결과 적용
    a. match.result = result
    b. 선수별 처리:
       - PlayerMatchStat 기록 (출전시간, 평점)
       - 피로도 누적
       - 폼 / 사기 갱신
    c. 리그 순위 갱신: League.standings.Update(result)
    d. 부상자 발생 처리 (V1.0~)
    e. 카드 누적 처리 (V1.0~)
    f. EventBus.Publish(new MatchFinishedEvent(match))

[5] UI: 경기 결과 화면
    a. 스코어, 득점자, 평점 표시
    b. "Continue" 클릭 시 다음 날로

[6] 같은 라운드의 다른 경기들:
    BackgroundSimulator가 자동 처리 (경량 시뮬)
    유저 구단과 같은 리그면 약간 더 디테일하게
```

### Sequence (비활성 구단 경기)
- MatchSimulator.SimulateLite(match, state) 호출
- 결과만 산출 (선수별 디테일 없음)
- 순위만 갱신
- 이벤트 발행 안 함 (UI 갱신 불필요)

### Key Points
- 시드는 매치 ID + 게임 시드 조합. 같은 매치는 항상 같은 결과.
- V0.1은 스코어만. V1.0에서 텍스트 이벤트 / 통계 추가.
- 결과 적용은 별도 단계로 분리 (테스트 시 시뮬만 돌려보고 적용 안 하기 가능).

---

## 4. 유스 인스펙션 흐름

### Trigger
- 6월 중순 (메인) 또는 1월 중순 (보조)
- EventScheduler가 YouthIntakeEvent 발행

### Sequence

```
[1] YouthSystem.GenerateIntake(userClub, state):
    a. PlayerGenerator로 후보 선수 생성 (15~30명)
       - 유스 시설 등급에 따라 PA 분포 결정
       - 트레잇 부여 (확률 기반)
       - 나이: 16~18세
    b. YouthIntake 객체 생성
       - candidatePlayerIds = 생성된 선수들의 ID
    c. 생성된 후보 선수들을 GameState.allPlayers에 추가
       - currentClubId = -1 (아직 미소속)
       - origin = YouthIntake
       - youthClubId = userClub.id (영입 안 되어도 출처 기록)
    d. club.intakeHistory에 추가
    e. EventBus.Publish(new YouthIntakeAvailableEvent(intake))

[2] UI: 유스 풀 화면 표시
    a. 후보 선수 목록 표시
       - 시설 등급에 따라 정보 정확도 다름 (PA 추정치, 트레잇 노출 정도)
    b. 유저 결정 옵션:
       [영입]      → 인원 제한 내에서 선택
       [리롤]      → 토큰 1개 소모
       [추가 스카우트] → 비용 지불, 정보 정확도 ↑
       [완료]      → 결정 마무리

[3-a] 영입 결정:
    - 영입된 선수들:
      currentClubId = userClub.id
      club.youthSquadIds에 추가
      intake.signedPlayerIds에 추가
    - 미영입 후보들 처리:
      일정 확률로 다른 구단에 영입 (AI)
      나머지는 무명으로 사라짐 (GameState에서 제거 또는 보관)
      intake.rejectedPlayerIds에 추가
    - EventBus.Publish(new YouthSignedEvent)

[3-b] 리롤 결정:
    a. state.rerollTokens -= 1
    b. intake.rerollsUsed += 1
    c. 기존 candidatePlayerIds의 선수들을 GameState에서 제거 (영입 안 된 것들)
    d. 새 풀 생성 ([1] 단계 반복, 같은 intake 객체에 덮어쓰기)
    e. UI 갱신

[3-c] 추가 스카우트:
    a. 비용 차감
    b. UI 정보 정확도 향상 (intake 자체는 변경 X, UI 표시 옵션만)
```

### Key Points
- 후보 선수도 일반 Player 인스턴스. GameState.allPlayers에 들어감.
- 영입 안 된 후보는 게임 세계에서 사라지는 게 아니라 다른 구단으로 갈 수 있음.
- 리롤 시 미영입 후보를 어떻게 처리할지 결정 필요 (TBD: 모두 삭제 vs 일부 다른 구단 가게 유지).
- 인스펙션 이력은 영구 저장 (회고 재미).

---

## 5. 이적 흐름

### Trigger
- 이적창 오픈 기간 중 유저 행동 (영입 / 판매)
- AI 구단의 자동 이적 (V1.0+)

### Sequence (유저 영입)

```
[1] UI: 이적 검색 화면
    a. 필터 입력 (포지션, 연령, 능력치 등)
    b. TransferSystem.SearchPlayers(filter, state) 호출
    c. 결과 목록 표시 (스카우팅 범위 내 + 능력치 정확도 시설 등급 영향)

[2] 유저가 특정 선수 선택 → 오퍼 작성:
    a. 이적료 입력
    b. 제안 계약 조건 입력 (주급, 기간)
    c. "Submit Offer" 클릭

[3] TransferSystem.SubmitOffer():
    a. TransferOffer 생성, status = Pending
    b. state.activeOffers에 추가
    c. EventBus.Publish(new OfferSubmittedEvent)

[4] TransferSystem.ProcessOffers() (매일 호출):
    - 각 Pending 오퍼에 대해 판매 구단 의사 결정
      · 시장 가치 대비 오퍼 평가
      · 구단 명성, 선수 잉여 여부, 라이벌 관계 등 고려
    - 결과:
      [수락] → status = Negotiating, 선수 개인 협상 단계로
      [거절] → status = Rejected, 유저에게 알림
      [역제안] → status = CounterOffer, 새 금액 제시

[5] 선수 개인 협상 (status = Negotiating):
    - 선수가 제시된 계약 조건 평가
    - 명성, 출전시간 기대, 야망 등 고려
    - 결과:
      [수락] → status = Accepted, 이적 완료 처리
      [거절] → status = Rejected
      [더 좋은 조건 요구] → 유저 추가 결정

[6] 이적 완료 처리 (status = Accepted):
    a. Player.currentClubId 변경
    b. 양 구단 squad 리스트 갱신
    c. 양 구단 finance 갱신 (이적료 이동)
    d. Player.contract 갱신
    e. status = Completed
    f. EventBus.Publish(new TransferCompletedEvent)
```

### Key Points
- V0.1은 단순화: 오퍼 → 협상 → 체결 3단계.
- V1.0에서 협상 / 메디컬 / 에이전트 단계 추가.
- 같은 선수에 대해 동시에 여러 오퍼 가능.
- 이적창 마감 시 미체결 오퍼 자동 무산.

---

## 6. 시즌 종료 / 신규 시즌 흐름

### Trigger
- 시즌 마지막 경기 종료
- EventScheduler가 SeasonEndEvent 발행

### Sequence

```
[1] SeasonEndProcessor 실행:
    a. 리그 최종 순위 확정
    b. 승강 처리 (V0.1은 단일 리그라 생략, V1.0+)
    c. 시상 (MVP, 득점왕 등)
    d. 보드 시즌 평가:
       - 목표 vs 실제 성적 비교
       - 보드 신뢰도 변동
       - 경질 가능성 체크
    e. 재정 결산 (입장료, 중계권, 상금)

[2] 사기 / 모랄 정산:
    - 우승팀 선수: 사기 ++
    - 강등팀 선수: 사기 --
    - 약속 출전시간 미달자: 불만 ↑
    - 시즌 베스트 선수: 사기 ↑

[3] 계약 처리:
    - 만료 선수 → 자유계약(FA) 전환
      Player.currentClubId = -1
    - 갱신 협상 시작 (만료 6개월 전부터)

[4] 은퇴 처리:
    - 33세 이상 + 능력치 하락폭 큰 선수 확률적 은퇴
    - 은퇴 선수는 GameState에서 제거하거나 isRetired 플래그

[5] Match 데이터 압축:
    - 이번 시즌 경기들 → 요약만 남기고 디테일 제거
    - 우승 / 강등 / 시상 정보만 보존

[6] 신규 시즌 준비 (회계연도 6/1):
    - GameState.currentDate = 6/1
    - 리롤 토큰 지급 (state.rerollTokens += 3, 최대 5)
    - 보드 신규 시즌 목표 제시
    - 이적 예산 / 연봉 예산 재배정
    - 새 시즌 일정 생성 (ScheduleGenerator)
    - 선수 나이 +1, 컨디션 / 폼 리셋

[7] EventBus.Publish(new NewSeasonStartedEvent)

[8] UI: 시즌 요약 화면 → 신규 시즌 목표 화면
```

### Key Points
- 시즌 종료는 게임의 자연스러운 정지점. 자동 저장.
- 압축 시점이 명확해야 세이브 크기 관리 가능.
- 신규 시즌은 거의 새 게임 시작과 비슷한 양의 초기화 필요.

---

## 7. 세이브 / 로드 흐름

### Save Sequence

```
[1] SaveSystem.Save(state, slotName):
    a. PreSave 훅: 인덱스 / 캐시 정리
    b. JsonConvert.SerializeObject(state)
       - [JsonIgnore] 필드 제외
       - DateTime ISO 8601 포맷
    c. 파일 쓰기: ~/saves/{slotName}.json
       - 임시 파일에 먼저 쓰고 atomic rename (corruption 방지)
    d. 메타 파일 갱신: ~/saves/{slotName}.meta
       - 저장 시각, 게임 내 날짜, 구단명 등 (로드 화면 표시용)
    e. EventBus.Publish(new GameSavedEvent)
```

### Load Sequence

```
[1] SaveSystem.Load(slotName):
    a. ~/saves/{slotName}.json 읽기
    b. JsonConvert.DeserializeObject<GameState>()
    c. PostLoad 훅:
       - GameState.BuildIndexes() 호출
       - 시스템들 초기 상태 복원 (없음, 모두 stateless)
    d. EventBus.Publish(new GameLoadedEvent)
    e. 메인 게임 화면으로
```

### Auto Save

- 시즌 종료 시 자동
- 매일 진행 시 N번째마다 자동 (옵션)
- 슬롯명: "autosave_001", "autosave_002" 등 순환 (3슬롯)

### Key Points
- 직렬화는 GameState 단일 객체에 집중. 다른 클래스는 전부 GameState 안에 있음.
- ID 참조 패턴 덕분에 순환 참조 없음.
- 로드 후 인덱스 재빌드 필수.
- 임시 파일 + atomic rename으로 corruption 방지.
- V0.1엔 마이그레이션 미고려. 클래스 구조 동결 후 V1.0부터 버전 필드 추가.

---

## Common Patterns

### Stateless System 호출 패턴

```csharp
// 시스템은 입력으로 GameState를 받음
public class SomeSystem {
    public Result DoSomething(InputData input, GameState state) {
        // 1. 읽기
        var entity = state.GetXxx(input.id);
        
        // 2. 계산
        var result = Compute(entity, input);
        
        // 3. 상태 변경
        ApplyChanges(state, result);
        
        // 4. 이벤트 발행
        EventBus.Publish(new SomethingHappenedEvent(result));
        
        return result;
    }
}
```

### UI 갱신 패턴

```csharp
// UI는 EventBus 구독으로 갱신
public class SquadView : MonoBehaviour {
    void OnEnable() {
        EventBus.Subscribe<TransferCompletedEvent>(OnTransfer);
        EventBus.Subscribe<MatchFinishedEvent>(OnMatch);
    }
    
    void OnDisable() {
        EventBus.Unsubscribe<TransferCompletedEvent>(OnTransfer);
        EventBus.Unsubscribe<MatchFinishedEvent>(OnMatch);
    }
    
    private void OnTransfer(TransferCompletedEvent e) {
        Refresh();
    }
}
```

### 인덱스 동기화 패턴

```csharp
// 추가/삭제는 항상 GameState 헬퍼 거치기
public class GameState {
    public void AddPlayer(Player p) {
        allPlayers.Add(p);
        _playerById[p.id] = p;
    }
    
    public void RemovePlayer(int id) {
        if (_playerById.TryGetValue(id, out var p)) {
            allPlayers.Remove(p);
            _playerById.Remove(id);
        }
    }
    
    // ❌ state.allPlayers.Add() 직접 호출 금지
}
```

---

## TBD (이 문서에서 나중에 결정)

- 유스 인스펙션 리롤 시 미영입 후보 처리 방식 (모두 삭제 vs 일부 다른 구단)
- 자동 저장 빈도 (옵션 / 매일 / 시즌만)
- 비활성 구단의 "경량 시뮬" 구체적 알고리즘
- 이적창 마감 시 미체결 오퍼 처리 (자동 무산 외 추가 처리?)
