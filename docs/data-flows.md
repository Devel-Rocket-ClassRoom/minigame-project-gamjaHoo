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
    b. 결과 산출 (V0.1, algorithms.md #2 명세):
       - starting11 자동 선정 (top-11 by CA, 부상자 제외)
       - 양 팀 전력 계산 (starting11 CA 합)
       - λ 계산 + Poisson 샘플링 → home/away 골수 결정 (홈 어드밴티지 가산)
       - 골수 만큼 라인 가중치 × CA 비례 추첨으로 득점자 선정
    c. MatchResult 반환 (스코어 + 득점자 + starting11 + playerStats)
       - playerStats: goals + minutesPlayed=90 만 채움 (V0.1)
       - assists / rating / yellowCards / redCards 는 0 (V1.0+)
       - match.result 에 쓰지 않음 — Task 9.2 가 적용
       - MatchFinishedEvent 발행하지 않음 — Task 9.2 가 발행

[4] MatchPostProcessor: 결과 적용 (Task 9.2)
    a. match.result = result  (사전: match.result == null, 재처리 시 InvalidOperationException)
    b. 선수별 처리:
       - PlayerMatchStat 기록 — MatchSimulator 가 이미 채움 (V0.1: goals + minutesPlayed=90 만)
       - 피로도 누적 — starting11 22명에 balance.fatigueGainPerMatch (30) 가산, Clamp 0..100
       - 폼 / 사기 갱신 — **V0.1 미구현** (design-decisions.md #30 V1.0+ 보완 포인트)
         · 평점 시스템 부재 + 사기 시스템 자체가 V1.0+ → 폼/사기 묶음으로 V1.0 도입
    c. 리그 순위 갱신 — match.type == League 일 때만. Standings.entries 의 양 팀 entry 갱신
       (played+1 / goalsFor / goalsAgainst / 승무패 / points: 승 3 · 무 1 · 패 0)
    d. 부상자 발생 처리 — V1.0+ (`design-decisions.md` #34 이벤트 시퀀스 도입 시 자연 발생)
    e. 카드 누적 처리 — V1.0+ (위와 동일)
    f. EventBus.Publish(new MatchFinishedEvent { matchId, result })

[5] UI: 경기 결과 화면
    a. 스코어, 득점자, 평점 표시
    b. "Continue" 클릭 시 다음 날로

[6] 같은 라운드의 다른 경기들:
    BackgroundSimulator가 자동 처리 (경량 시뮬)
    유저 구단과 같은 리그면 약간 더 디테일하게
```

### Sequence (비활성 구단 경기)

**[V0.1]** 활성 / 비활성 구분 알고리즘 / 이벤트 발행 모두 **동일** (옵션 A — 단순화).
- `BackgroundSimulator.SimulateDay(state, balance)` (Task 9.3) 가 `state.currentDate` 모든 매치 일괄 처리.
- 각 매치: `MatchSimulator.Simulate` → `MatchPostProcessor.Process`.
- **모든 매치 `MatchFinishedEvent` 발행** — V0.1 UI 없으므로 구독자 0, `EventBus.Publish` 비용 ~0. 인터페이스 단순화 우선.
- 이미 처리된 매치 (`match.result != null`) 는 스킵 (PostProcessor 의 재처리 예외 회피).

> **V1.0+ 보완 포인트**:
> 1. **UI 도입 시 `publishEvent` 옵션** — `MatchPostProcessor.Process(..., publishEvent: bool)` 추가. 유저 구단 매치만 `true` (UI 결과 화면 갱신용), 비활성 매치는 `false` (백그라운드).
> 2. **분 단위 이벤트 시뮬 도입 시 경량 경로** — 비활성 구단 경기는 텍스트 이벤트 생성 비용 회피 위해 별도 `SimulateLite` 분리 검토 (`design-decisions.md` #34).

### Key Points
- 시드는 매치 ID + 게임 시드 조합. 같은 매치는 항상 같은 결과 (`algorithms.md` #2 1단계).
- V0.1은 스코어 + 득점자만. V1.0에서 텍스트 이벤트 / 평점 / 카드 / 부상 추가 (`design-decisions.md` #34).
- 결과 적용은 별도 단계 (Task 9.2 `MatchPostProcessor`) 로 분리 — 테스트 시 시뮬만 돌려보고 적용 안 하기 가능.
- 활성 / 비활성 분기는 V0.1 에선 이벤트 발행 여부만 다름.

---

## 4. 유스 인스펙션 흐름

### Trigger
- **메인 인스펙션 — 6/15** (시즌 종료 직후)
- **보조 인스펙션 — 1/15** (시즌 중간)
- `EventScheduler` 가 `state.currentDate` 가 위 날짜 (월/일) 도달 시 `YouthSystem.GenerateIntake(userClub, state, ...)` 호출 + `YouthIntakeAvailableEvent` 발행 + 정지 신호 (UI 유스 풀 화면)
- 외부화: `GameBalanceSO.youthIntakeMainMonth/Day = 6/15`, `youthIntakeSecondMonth/Day = 1/15`

### Sequence

```
[1] YouthSystem.GenerateIntake(club, state, balance, db, leagueConfig) (algorithms.md #4 명세):
    a. 시드 고정 (외부 마이닝 + 직플 영상 공유 방어):
       rng = new Random(
           state.randomSeed
           ^ unchecked((int)state.currentDate.Ticks)   # 옵션 2 — 시점별 시드
           ^ userActionHash                            # 옵션 3 — 유저 행동 반영
           ^ club.id ^ intake.id ^ intake.rerollsUsed
       )
       userActionHash = club.finance.money ^ squad.Count*7919 ^ youthSquad.Count*9973 ^ tokens*16007
    b. 풀 사이즈 결정 = FacilityLevelSO(Youth, club.facilities.youthLevel).youthPoolSize
       - V0.1: 시설 = 유소년 시스템 종합 등급 (시설+코치+모집 통합, design-decisions.md #35)
       - V1.0+: Club.youthCoachLevel / youthRecruitmentLevel 분리
    c. 후보 선수 N명 생성 (algorithms.md #4 3단계):
       - PA 추첨: 5% 스타 픽 (PA 평균 +50) / 95% 일반 (NextNormal(facility.youthAvgPA, σ=15))
       - CA 추첨: PA 역방향 (PA - NextNormal(caGap, σ=25) — 의존성 약화로 PA 추정 차단)
       - 나이: 16=40% / 17=40% / 18=20% (PersonalInfo.birthDate 저장, age 별도 X)
       - 국적: 자국 78% / 외국 22% (ClubGen 0.70 보다 ↑)
       - 포지션: V0.1 균등 / V1.0+ 라운드별 가중치 변동
       - 트레잇: PlayerGenerator 재활용
    d. YouthIntake 빌드:
       intake.id = state.nextIntakeId++  (design-decisions.md #36)
       candidatePlayerIds = 생성된 선수들 ID
       currentClubId = -1, youthClubId = club.id, origin = YouthIntake
    e. club.intakeHistory.Add(intake)
    f. EventBus.Publish(new YouthIntakeAvailableEvent { intakeId, clubId })

[2] UI: 유스 풀 화면 표시
    a. 후보 선수 목록 표시
       - 시설 등급에 따라 정보 정확도 다름 (PA 추정치, 트레잇 노출 정도)
    b. 유저 결정 옵션:
       [영입]      → 인원 제한 내에서 선택
       [리롤]      → 토큰 1개 소모
       [추가 스카우트] → 비용 지불, 정보 정확도 ↑
       [완료]      → 결정 마무리

[3-a] 영입 결정 — YouthSystem.SignPlayers(intake, playerIds, club, state):
    - 영입된 선수들:
      currentClubId = userClub.id
      club.youthSquadIds에 추가
      intake.signedPlayerIds에 추가
    - 미영입 후보들 처리 (V0.1 단순화, design-decisions.md #35):
      **V0.1: 모두 GameState에서 제거 — intake.rejectedPlayerIds에 ID만 보관 (#7 영구 저장, 회고용)**
      V1.0+: 일정 확률 (youthRejectedToOtherClubRatio) 로 AI 다른 구단 영입 (algorithms.md #4 V1.0 Notes)
    - intake.candidatePlayerIds.Clear() — signed/rejected 로 모두 이동
    - EventBus.Publish(new YouthSignedEvent { intakeId, signedPlayerIds })

[3-b] 리롤 결정 — YouthSystem.UseRerollToken(intake, club, state, balance, db, leagueConfig):
    a. state.rerollTokens -= 1 (사전: > 0, 아니면 InvalidOperationException)
    b. intake.rerollsUsed += 1 → 시드 변경 보장 (algorithms.md #4 1단계)
    c. 기존 candidatePlayerIds 중 영입 안 된 선수만 GameState 에서 제거 (이미 영입된 signed 는 유지)
    d. 새 풀 생성 ([1] 단계 c~d 반복) — rerollsUsed 가 +1 됐으므로 자동으로 다른 풀
    e. EventBus.Publish(new YouthRerolledEvent { intakeId, remainingTokens })

[3-c] 추가 스카우트 — V1.0+ (V0.1 미구현):
    V0.1: 정보 정확도 시스템 부재 — UI 가 항상 풀 정보 그대로 표시
    V1.0+: 비용 차감 + UI 정보 정확도 향상 (PA 추정치 범위 좁힘 / 트레잇 노출 정도). intake 자체 변경 X.
```

### Key Points
- 후보 선수도 일반 Player 인스턴스. GameState.allPlayers에 들어감 (`origin = YouthIntake`, `currentClubId = -1`).
- **V0.1: 미영입 후보 모두 GameState 제거** + `rejectedPlayerIds` 에 ID 만 보관 (`design-decisions.md` #7 영구 저장 / #35).
- **V1.0+: 일정 확률 AI 다른 구단 영입** — `algorithms.md #4` V1.0 Migration Notes.
- 인스펙션 이력 (`intakeHistory`) 은 ID 만이라도 영구 저장 (회고 재미).
- **결정성 정신 보존 (`#17`)** — 같은 newgame + 같은 행동 → 같은 풀. 다만 `currentDate.Ticks` + `userActionHash` 로 외부 마이닝 + 직플 영상 공유 사실상 차단.

---

## 5. 이적 흐름

> **용어 — 이적시장 vs 이적시장 활성화 기간 (사용자 합의, `design-decisions.md` #37)**:
> - **이적시장 (Transfer Market)**: 검색·오퍼·협상 시스템 전체 — **상시 활성**
> - **이적시장 활성화 기간 (Transfer Window)**: 실제 체결 발효 시기 (6/1~8/31 여름 + 1/1~1/31 겨울) — **체결만 시기 제약**
> - 영어 변수명 `transferWindow*` 그대로 (도메인 표준). 한국어 docs / UI 표현만 정정.

### Trigger
- **검색·오퍼**: 시점 제약 X (V0.1, 사용자 클럽 능동 행동)
- **AI 응답**: DailyProcessor 가 매일 호출 (시점 제약 X)
- **체결**: Accepted 오퍼가 이적시장 활성화 기간 진입 시 자동
- **AI 구단의 자동 이적**: V1.0+ (V0.1 미구현)

### Sequence (유저 영입, V0.1, algorithms.md #3 명세)

```
[1] TransferMarket.SearchPlayers(filter, state) — 상시 호출 가능 (시점 제약 X)
    a. UI 또는 호출자가 TransferSearchFilter 구성 (position / minAge,maxAge / minCA,maxCA / excludeUserClub)
    b. state.allPlayers LINQ 필터링 → 매칭 선수 반환
    c. V0.1: 모든 선수 정확한 CA/PA 노출 (스카우트 시스템 V1.0+)

[2] 유저가 특정 선수 선택 → 오퍼 작성:
    a. 이적료 입력
    b. 제안 계약 조건 입력 (주급, 기간)
    c. "Submit Offer" 클릭

[3] TransferSystem.SubmitOffer() — 시점 제약 X (활성화 기간 외에도 가능, 미리 협상):
    a. 사전 검증: 양 구단 존재 / 선수 fromClubId 소속 / amount > 0
    b. TransferOffer 생성: status = Pending, id = state.nextOfferId++
    c. state.activeOffers에 추가
    d. EventBus.Publish(new OfferSubmittedEvent { offerId })

[4] TransferSystem.ProcessOffers() — DailyProcessor.Run 안에서 매일 호출:
    foreach offer in state.activeOffers:
      switch offer.status:
        case Pending:
            # AI 응답 (algorithms.md #3.1 [3-a])
            rng = new Random(state.randomSeed ^ offer.id ^ currentDate.Ticks)
            marketValue = CalculateMarketValue(player, state, balance)
            aiPerceivedValue = marketValue * rng.NextNormal(1.0, balance.aiValueNoiseSigma)  # ±10% noise
            ratio = offer.amount / aiPerceivedValue
            if ratio >= balance.aiAcceptRatio (1.20):
                status = Accepted
            else:
                status = Rejected
            EventBus.Publish(new OfferRespondedEvent)
        
        case Accepted:
            # 활성화 기간 시 자동 체결
            if IsTransferWindowOpen(state.currentDate, balance):
                CompleteTransfer(offer, state)
        
        # Rejected / Completed: skip

[5] 선수 개인 협상 — V0.1 자동 통과 (V1.0+ 협상 시스템):
    - V0.1: status = Negotiating 단계 스킵 → AI 판매 구단 Accepted = 곧바로 체결 대기
    - V1.0+: 주급 / 명성 / 출전시간 기대 / 야망 평가 + 다중 라운드

[6] 이적 완료 처리 (CompleteTransfer, status = Accepted → Completed):
    a. Player.currentClubId 변경 (fromClubId → toClubId)
    b. 양 구단 squad 리스트 갱신 (fromClub.seniorSquadIds 제거 / toClub 추가)
    c. 양 구단 finance.money 갱신 (이적료 이동)
    d. Player.contract = offer.proposed (새 계약 적용)
    e. status = Completed
    f. EventBus.Publish(new TransferCompletedEvent { offerId, playerId, fromClubId, toClubId, amount })
```

### 자연스러운 시나리오 — 미리 협상 + 활성화 기간 자동 체결

```
11/15 (시즌 중간) — 유저가 다른 클럽 슈퍼스타 검색 → 오퍼 (시장가 9.5M, offer 12M)
                     status = Pending
11/16             — DailyProcessor.ProcessOffers → AI 응답: ratio=1.26 ≥ 1.20 → Accepted
                     OfferRespondedEvent 발행, status = Accepted (대기)
11/17 ~ 12/31     — 매일 ProcessOffers 호출. Accepted 이지만 IsTransferWindowOpen=false → 체결 보류
1/1 (윈터 시작)   — IsTransferWindowOpen=true → CompleteTransfer
                     선수 이적 발효 + TransferCompletedEvent 발행
```

### Key Points

- **이적시장 vs 활성화 기간 분리** — 검색·오퍼·협상 상시 / 체결만 시기 제약. 실제 축구 메커닉 반영.
- **V0.1 단일 라운드** — AI 응답 (Accept/Reject) 만. 역제안 / 다중 협상 / 선수 협상 V1.0+.
- **AI 구단 능동 영입 V0.1 미구현** — 사용자 클럽만 오퍼. V1.0+ CpuTransferAi.
- **시드 결정성 (`design-decisions.md` #17)** — AI 응답이 결정적. ±10% noise 로 평가 부정확성 표현.
- **같은 선수 여러 오퍼 가능** — 각자 독립 처리. 첫 체결 시 player.currentClubId 변경 → 후속 오퍼는 fromClubId 불일치 스킵.
- **V1.0+ 보완** — `algorithms.md #3` V1.0 Migration Notes 30+ 항목 (AI 협상 / 스카우트 / 임대 / FA / 트랜스퍼 리스트 등).

---

## 6. 시즌 종료 / 신규 시즌 흐름

### Trigger

> V0.1 — **3 시점 변수명 분리** (혼동 회피, `design-decisions.md #38`):
> - `seasonEndMonth/Day = 5/15` — **시즌 종료** (마지막 매치 시점)
> - `fiscalYearStartMonth/Day = 6/1` — **회계연도 / 신규 시즌 행정 처리**
> - `newSeasonOpeningMonth/Day = 8/15` — **매치 개막** (ScheduleGenerator 가 새 시즌 첫 매치 배치)
>
> V1.0+ 트리거: 캘린더 / 요일 정보 도입 시 "5월 마지막 토요일" 같은 dynamic 계산 + 매년 가변 일정.

- **5/15 (시즌 종료) 도래** → `EventScheduler` 가 `SeasonEndProcessor.Run` 호출 + `SeasonEndedEvent` 발행 + 정지 신호
- **6/1 (회계연도) 도래** → `EventScheduler` 가 `NewSeasonProcessor.Run` 호출 + `SeasonStartedEvent` 발행 + 정지 신호

### Sequence (V0.1)

```
[5/15] SeasonEndProcessor.Run(state, balance):
    a. 리그 최종 순위 확정 (이미 매치 완료된 상태 — 변경 X)
    b. 승강 처리 — V0.1 단일 리그 미구현 (V1.0+ 다중 리그)
    c. 시상 — V0.1 미구현 (V1.0+ 시상 시스템)
    d. 보드 시즌 평가 / 경질 — V0.1 미구현 (V1.0+ 보드 시스템)
    e. 재정 결산 — V0.1 미구현 (V1.0+ 입장료/중계권/상금)
    f. 사기 / 모랄 정산 — V0.1 미구현 (#30, V1.0+ 사기 시스템)
    g. 계약 만료 → FA 전환 (V0.1 도입):
       foreach player in state.allPlayers:
           if player.contract.endDate <= state.currentDate:
               from = state.GetClub(player.currentClubId)
               from?.seniorSquadIds.Remove(player.id)
               from?.youthSquadIds.Remove(player.id)
               player.currentClubId = -1
    h. 은퇴 처리 (V0.1 도입):
       rng = new Random(state.randomSeed ^ state.currentDate.Ticks)
       foreach player in state.allPlayers (V0.1: copy):
           if GetAge(player) >= balance.retirementMinAge (33)
              && rng.NextDouble() < balance.retirementProbabilityPerYear (0.15):
               state.RemovePlayer(player.id)
               # club squad 도 정리 (헬퍼)
    i. Match 데이터 압축 — V0.1 미구현 (V1.0+ 저장 최적화)
    j. EventBus.Publish(new SeasonEndedEvent { seasonYear, summary })

[5/16 ~ 5/31] 오프시즌 — UI 정산 / 인스펙션 대기

[6/1] NewSeasonProcessor.Run(state, balance, db, leagueConfig):
    a. state.currentDate 갱신 (GameTime.Reset + 동기화)
    b. state.rerollTokens += balance.seasonRerollTokenGrant (3)
       state.rerollTokens = min(state.rerollTokens, balance.maxRerollStockpile (5))
    c. 모든 선수 fatigue = 0 / form = 50 리셋
    d. 모든 League.standings.entries 초기화 (played/won/drawn/lost/goals/points = 0)
    e. 새 시즌 매치 일정 생성:
       foreach league in state.leagues:
           ScheduleGenerator.Generate(...)  # newSeasonOpening 부터 ~ seasonEnd 까지
           league.seasonYear += 1
    f. 클럽별 season 갱신:
       targetLeaguePosition = i + 1  # 명성 순위 (#27 패턴)
       boardConfidence = balance.initialBoardConfidence (50)
       cupTarget = CupTarget.None  # V0.1 컵 미구현
    g. EventBus.Publish(new SeasonStartedEvent { seasonYear, target })

[6/15] Stage 10 메인 인스펙션 자동 트리거 (이미 통합됨)
[6/1 ~ 8/31] Stage 11 여름 이적시장 활성화 기간 (이미 통합됨)
[8/15] 새 시즌 첫 매치 (ScheduleGenerator 가 배치)
```

### Key Points
- **시즌 종료 5/15 / 매치 개막 8/15** = 사용자 명시 V0.1 고정값. V1.0+ 캘린더/요일 dynamic.
- **3 시점 변수명 분리** — 5/15 (matches end) / 6/1 (fiscal year) / 8/15 (opening day). 사용자 혼동 회피 요청 (2026-05-20).
- **시즌 종료는 자동 정지점** — UI 시즌 요약 화면. 자동 저장.
- **나이는 birthDate 로 자동 계산** — 신규 시즌에서 별도 +1 처리 X (PlayerGen 패턴 그대로).
- **V0.1 도입 vs V1.0+ 미루기** 분기 명확. `design-decisions.md #38` 참조.

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

- ~~유스 인스펙션 리롤 시 미영입 후보 처리 방식~~ — 해결됨 (`#35`, 2026-05-20). V0.1 모두 제거 / V1.0+ AI 영입 트리거.
- 자동 저장 빈도 (옵션 / 매일 / 시즌만)
- 비활성 구단의 "경량 시뮬" 구체적 알고리즘
- 이적창 마감 시 미체결 오퍼 처리 (자동 무산 외 추가 처리?)
