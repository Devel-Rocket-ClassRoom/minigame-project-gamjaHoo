# GitHub Workflow

GitHub Issues, Projects 보드, 브랜치, PR 관리 규칙. 이슈 생성 시 반드시 이 문서를 참조한다.

---

## 1. Branch Strategy

### Branch Naming

- `main` — 보호 브랜치. 직접 푸시 금지.
- 작업 브랜치 prefix:
  - `feature/<이슈번호>-<요약>` — 새 기능
  - `fix/<이슈번호>-<요약>` — 버그 수정
  - `chore/<이슈번호>-<요약>` — 빌드/설정/도구
  - `docs/<이슈번호>-<요약>` — 문서만 변경
  - `refactor/<이슈번호>-<요약>` — 동작 변경 없는 리팩터링

### Workflow

1. **GitHub Issue 생성** (메타데이터 필수 — §3 참조)
2. 이슈에서 브랜치 생성 (`Create a branch` 기능 → 이슈 번호 자동 연결)
3. 개발 → 로컬 커밋
4. PR 생성 → 본문에 `Closes #이슈번호` 명시 → 셀프 머지
5. 머지 시 이슈 자동 클로즈 + 브랜치 삭제

---

## 2. Commit & PR Convention

### Commit Message

```
<Prefix>: <50자 이내 요약>

<선택: 변경 이유 / 컨텍스트>
```

**Prefix:** `Feat` / `Fix` / `Chore` / `Docs` / `Refactor` / `Test`

- 한글 사용 가능. 제목은 명령형/현재형.
- 큰 변경은 본문에 *왜* 그렇게 했는지 적기 (변경 내용은 diff가 말해줌).
- 명세 참조: 알고리즘/시스템 구현 시 명세 위치 적기 (예: `algorithms.md #1 v0.3`)

**예시:**
```
Feat: PlayerGenerator 기본 구현

algorithms.md #1 (v0.3) 명세 반영.
명성 기반 CA 분포 + 포지션별 키 스탯 가중치.
```

### PR Template

```markdown
## 변경 사항
- (불릿)

## 관련 이슈
Closes #이슈번호

## 명세 참조
- (해당 시) algorithms.md #X
- (해당 시) data-flows.md §X

## 체크리스트
- [ ] design-decisions.md와 충돌 없음
- [ ] coding-conventions.md 스타일 준수
- [ ] 작업 Task 완료 조건 충족 (v0.1-tasks.md)
- [ ] 명세 참조 시 버전 일치 확인
- [ ] 새 결정 사항은 design-decisions.md에 추가됨
- [ ] 새 이벤트는 event-bus-catalog.md에 등록됨

## 스크린샷 / GIF
(UI 변경 시 첨부)
```

---

## 3. Issue Metadata

> 이슈를 만들 때는 반드시 다음 메타데이터를 함께 채운다. 메타데이터가 없는 이슈는 Projects 보드에서 누락되어 가시성이 무너진다.

### 필수 메타데이터

| 항목 | 값 / 규칙 |
| --- | --- |
| **Title** | `[영역] 동사형 작업명` 예: `[Domain] Player 클래스 정의` |
| **Labels** | `type:*` (1개) + `area:*` (1개 이상) — 아래 라벨 가이드 참조 |
| **Milestone** | `V0.1` / `V1.0` / `V1.x` 중 하나 |
| **Priority** | Projects 보드 `Priority` 필드 — `P0` / `P1` / `P2` (라벨 아님) |
| **Size** | Projects 보드 `Size` 필드 — `XS` / `S` / `M` / `L` / `XL` (라벨 아님) |
| **Projects** | 프로젝트 보드에 추가 → `Status` 자동 분류 |
| **Assignee** | 본인 |

### Title 규칙

`[영역] 동사형 작업명` 형식.

- 영역은 라벨의 area 카테고리와 일치 (Combat → `[Domain]`, `[UI]` 등)
- 동사형: "구현", "추가", "수정", "리팩터링", "조사" 등
- 25자 내외 권장

**좋은 예:**
- `[Domain] Player 클래스 정의`
- `[Simulation] MatchSimulator 기본 구현`
- `[Youth] 리롤 토큰 시스템 추가`
- `[UI] 유스 풀 화면 레이아웃 구성`

**나쁜 예:**
- `Player 만들기` (영역 누락)
- `Player를 구현하고 Stats도 같이 추가하고 Contract도...` (스코프 과다)
- `버그` (구체성 없음)

---

## 4. Labels

### Type 라벨 (필수, 1개)

| 라벨 | 용도 |
| --- | --- |
| `type:feature` | 새 기능 |
| `type:bug` | 버그 수정 |
| `type:task` | 잡일/설정/리팩터링/조사·스파이크 (조사는 본문에 시간 박스 명시) |

### Area 라벨 (필수, 1개 이상)

| 라벨 | 영역 |
| --- | --- |
| `area:domain` | 데이터 클래스 / 도메인 모델 |
| `area:simulation` | 경기 시뮬레이션 / AI 구단 |
| `area:youth` | 유스 인스펙션 / 리롤 |
| `area:transfer` | 이적 시스템 / 협상 |
| `area:gacha` | 스타팅 스쿼드 가챠 |
| `area:season` | 시즌 사이클 / 일정 / 보드 |
| `area:save` | 세이브 / 로드 |
| `area:ui` | UI / 화면 / 와이어프레임 |
| `area:data` | ScriptableObject / 밸런싱 데이터 |
| `area:infra` | EventBus / GameTime / 인프라 |
| `area:editor` | 디버그 도구 / 커스텀 에디터 |
| `area:docs` | 문서 |

### 상태/특수 라벨 (필요 시)

| 라벨 | 의미 |
| --- | --- |
| `blocked` | 다른 이슈/외부 요인 대기 중 |
| `playtest-needed` | 플레이테스트로 수치 확정 필요 |
| `out-of-scope-candidate` | 스코프 검토 필요 |
| `question` | 결정 사항 / 사용자 입력 대기 |

> **Priority와 Size는 라벨이 아니다.** Projects 보드의 단일선택 필드로 관리.

---

## 5. Projects Board

### 보드 필드 (커스텀)

| 필드 | 옵션 | 의미 |
| --- | --- | --- |
| **Status** | `Todo` / `In progress` / `Done` | 진행 상태 |
| **Priority** | `P0` / `P1` / `P2` | 우선순위 |
| **Size** | `XS` / `S` / `M` / `L` / `XL` | 작업량 추정 |

### Priority 의미

- **P0** — 즉시 필수 (코어 루프 / 마일스톤 게이트)
- **P1** — 콘텐츠 / 마감 단계
- **P2** — 가용 시간 내 추가

### Size 의미

- **XS** — 0.5일 이하 (30분~수 시간)
- **S** — 1일 이내
- **M** — 1~2일
- **L** — 2~3일
- **XL** — 3일 초과 → 이슈 분할 검토

### Status 컬럼

- **Todo** — 등록·대기 중 (백로그 + 다음 작업 후보)
- **In progress** — 진행 중 (동시 1~2개 권장, PR 오픈·셀프 리뷰 단계 포함)
- **Done** — 머지 완료

### 보드 뷰

한눈에 백로그를 보려면 보드를 **Milestone** 또는 **Priority** 로 그룹핑.

---

## 6. Issue Body Templates

### Feature 템플릿

```markdown
## 목표
(한 줄, 무엇을 달성하면 close 가능한가)

## 컨텍스트 / 근거
- v0.1-tasks.md Stage X / Task X.X
- design-decisions.md #X
- (관련 결정/링크)

## 완료 기준 (DoD)
- [ ] (검증 가능한 항목)
- [ ] (검증 가능한 항목)

## 명세 참조
- algorithms.md #X (해당 시)
- data-flows.md §X (해당 시)
- event-bus-catalog.md (해당 시)

## 비고
(스코프 경계 / 미정 사항 / 의존 이슈)
```

### Bug 템플릿

```markdown
## 증상
(무슨 일이 일어나는가)

## 재현 절차
1. 
2. 
3. 

## 예상 동작
(어떻게 동작해야 하는가)

## 실제 동작
(실제로는 어떻게 동작하는가)

## 환경
- Unity 버전:
- 발견 시점 (커밋 해시):

## 비고
```

### Task 템플릿

```markdown
## 목표
(한 줄)

## 작업 내용
- [ ] 
- [ ] 

## 완료 기준
- [ ] 

## 비고
(조사·스파이크면 시간 박스 명시. 예: "최대 2시간 조사 후 결정")
```

---

## 7. V0.1 Task → Issue 매핑

`v0.1-tasks.md`의 각 Task는 GitHub Issue로 1:1 매핑한다.

### 매핑 예시

| v0.1-tasks Task | Issue Title | Type | Area | Milestone | Priority | Size |
| --- | --- | --- | --- | --- | --- | --- |
| Task 1.1 | `[Infra] Unity 프로젝트 생성 및 폴더 구조` | task | infra | V0.1 | P0 | S |
| Task 2.1 | `[Infra] EventBus 구현` | feature | infra | V0.1 | P0 | S |
| Task 3.2 | `[Domain] 핵심 도메인 클래스 정의` | feature | domain | V0.1 | P0 | M |
| Task 6.1 | `[Domain] PlayerGenerator 구현` | feature | domain,data | V0.1 | P0 | L |
| Task 9.1 | `[Simulation] MatchSimulator 기본 구현` | feature | simulation | V0.1 | P0 | M |
| Task 10.1 | `[Youth] YouthSystem.GenerateIntake 구현` | feature | youth | V0.1 | P0 | M |

### 일괄 생성 시점

V0.1 작업 시작 전, 모든 Task를 이슈로 일괄 생성하면 백로그가 보드에 정리되어 진행도 가시화 가능.

---

## 8. Anti-Patterns

### ❌ 메타데이터 없는 이슈

```
Title: Player 만들기
(라벨 없음, 마일스톤 없음, Priority 없음)
```

→ 보드에서 누락됨. 백로그 가시성 무너짐.

### ❌ 너무 큰 이슈

```
Title: [Domain] 전체 도메인 모델 구현 (모든 클래스)
Size: XL
```

→ 분할 필요. Player, Club, League 등 별도 이슈로.

### ❌ 영역 라벨 누락

```
Title: PlayerGenerator 구현
Labels: type:feature (area 없음)
```

→ 영역별 필터링 불가. `area:domain` 추가 필수.

### ❌ DoD 없는 이슈

```
## 목표
PlayerGenerator 만들기

## 완료 기준
(비어있음)
```

→ "완료"의 기준이 모호. 검증 가능한 항목 필수.

---

## 9. Workflow Cheatsheet

새 작업 시작 시:

```
1. v0.1-tasks.md에서 다음 Task 선택
2. GitHub Issue 생성:
   - Title: [영역] 동사형 작업명
   - Type 라벨 + Area 라벨
   - Milestone (V0.1/V1.0/V1.x)
   - Projects 보드 추가 → Priority, Size 설정
   - 본문 템플릿 채우기
3. 이슈에서 브랜치 생성 (feature/123-xxx)
4. 개발
5. PR 생성, Closes #123 명시
6. 셀프 머지
```

작업 완료 시:

```
1. v0.1-tasks.md의 해당 Task 체크박스 갱신 ([x])
2. 결정 사항 있으면 design-decisions.md 추가
3. 새 이벤트 있으면 event-bus-catalog.md 등록
4. Change Log 갱신
```

---

## Change Log

| Date | Change |
| --- | --- |
| 2025-05-15 | 초안 작성 (FM-Lite 영역 라벨 반영) |
