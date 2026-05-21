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

### PR Metadata (필수)

> PR 도 이슈와 동일하게 메타데이터를 채운다. PR 만 보드에 안 들어가면 진행도 추적 누락.

| 항목 | 값 |
| --- | --- |
| **Labels** | 이슈와 동일한 `area:*` 라벨 (1개 이상) |
| **Milestone** | `V0.1` / `V1.0` / `V1.x` |
| **Assignee** | 본인 (`@me`) |
| **Projects** | `FM-Lite` 보드 (#50) 추가 |

**gh CLI 예시:**

```bash
# PR 생성 (label / milestone / assignee 한 번에)
gh pr create --title "..." --label "area:simulation,area:infra" \
  --milestone "V0.1" --assignee "@me" --body "..."

# Projects 보드 추가 (gh pr create 가 받지 못하는 필드 — 별도 호출)
gh project item-add 50 --owner Devel-Rocket-ClassRoom \
  --url https://github.com/Devel-Rocket-ClassRoom/minigame-project-gamjaHoo/pull/<n>
```

> 이슈 / PR 생성 시점에 보드 자동 추가 안 됨. **항상 `gh project item-add` 를 별도로 호출**.

### PR 본문 — `Closes` 구문 함정

PR 본문에 닫을 이슈 명시 시 **각 이슈 앞에 키워드 필요**. 콤마로만 묶으면 GitHub 가 **첫 번째만 close**.

```
❌ Closes #39, #40, #41          → #39 만 close (#40, #41 OPEN 남음)
❌ Closes #39 #40 #41             → #39 만 close
✅ Closes #39, closes #40, closes #41
✅ - Closes #39
   - Closes #40
   - Closes #41
```

여러 이슈를 한 PR 로 닫을 때 매번 검증 — 머지 후 GitHub 가 닫은 이슈 목록 확인.

### 이슈 Close 시 — Start / Target Date / Iteration 자동 입력 (필수)

> **이슈 close 시 Claude 가 즉시 보드 업데이트** (사용자 명시 요청, 2026-05-20). FM-Lite 작업 패턴 = "같은 날 시작 + 같은 날 완료" 라 `Start = Target = closedAt`.

| 필드 | 값 | API 종류 |
| --- | --- | --- |
| **Start date** | `closedAt` 의 date 부분 (`yyyy-MM-dd`) | **Issue field** (조직 차원) |
| **Target date** | 동일 (`closedAt`) | **Issue field** (조직 차원) |
| **Iteration** | `closedAt` 이 속한 iteration (1=5/18~5/24, 2=5/25~5/31, 3=6/1~6/7) | **Project field** (보드 차원) |

> **중요 — 두 API 구분**:
> - **Start / Target date 는 Issue field** (`updateIssueFieldValue` mutation, organization 차원). `updateProjectV2ItemFieldValue` 로 시도하면 **에러** ("Issue field values cannot be updated using the updateProjectV2ItemFieldValue mutation").
> - **Iteration 은 여전히 Project field** (`updateProjectV2ItemFieldValue`).
> - **Priority / Size 도 Project field** (기존과 동일).

**FM-Lite 프로젝트 필드 ID 참조** (한 번만 조회해서 기록):

```
# Project + Project fields
Project ID:        PVT_kwDODykJwc4BYAHm
Iteration:         PVTIF_lADODykJwc4BYAHmzhTWM5Q  (Project field)
Priority:          PVTSSF_lADODykJwc4BYAHmzhTJKJg
Size:              PVTSSF_lADODykJwc4BYAHmzhTJKKY

# Issue fields (조직 Devel-Rocket-ClassRoom)
Start date:        IFD_kgDOAk3m_w
Target date:       IFD_kgDOAk3nAA
Priority (Issue):  IFSS_kgDOAk3m_g     # Project Priority 와 별개
Effort:            IFSS_kgDOAk3nAQ
```

**처리 흐름** (PR 머지 직후 또는 이슈 수동 close 시):

```powershell
# 1. 보드 미등록이면 추가
gh project item-add 50 --owner Devel-Rocket-ClassRoom --url <issue-url>

# 2. 이슈 + closedAt + node ID 가져옴
$issue = gh issue view <n> --json id,closedAt | ConvertFrom-Json
$dateStr = $issue.closedAt.ToString("yyyy-MM-dd")

# 3-a. Start date — Issue field
$mut = "mutation { updateIssueFieldValue(input: { issueId: ""$($issue.id)"", issueField: { fieldId: ""IFD_kgDOAk3m_w"", dateValue: ""$dateStr"" } }) { issue { id } } }"
gh api graphql -f query=$mut

# 3-b. Target date — Issue field (같은 dateStr)
$mut = "mutation { updateIssueFieldValue(input: { issueId: ""$($issue.id)"", issueField: { fieldId: ""IFD_kgDOAk3nAA"", dateValue: ""$dateStr"" } }) { issue { id } } }"
gh api graphql -f query=$mut

# 3-c. Iteration — Project field (보드 item ID 필요)
$boardItem = (gh project item-list 50 --owner Devel-Rocket-ClassRoom --format json --limit 200 | ConvertFrom-Json).items | Where-Object { $_.content.type -eq "Issue" -and $_.content.number -eq <n> } | Select-Object -First 1
$iterId = "90778f22"  # closedAt 이 속한 iteration ID
$mut = "mutation { updateProjectV2ItemFieldValue(input: { projectId: ""PVT_kwDODykJwc4BYAHm"", itemId: ""$($boardItem.id)"", fieldId: ""PVTIF_lADODykJwc4BYAHmzhTWM5Q"", value: { iterationId: ""$iterId"" } }) { projectV2Item { id } } }"
gh api graphql -f query=$mut
```

**일괄 처리 스크립트**는 메모리 `feedback_issue_close_workflow.md` 참조.

> **GitHub Projects 의 한계**: GitHub Action / webhook 없이는 close 시 자동 채워지지 않음. Claude 가 매 close 시점에 처리 책임.
>
> **2026-05-20 발견된 함정** — Start/Target date 가 보드 field 아닌 **organization 차원 Issue field** 로 마이그레이션됨. 보드 필드 ID (`PVTF_...`) 가 아닌 **Issue field ID (`IFD_...`)** 사용 필수. `gh api graphql -f query='{ organization(login: "...") { issueFields(first: 50) { nodes { __typename ... on IssueFieldDate { id name } } } } }'` 로 조회.

---

## 3. Issue Metadata

> 이슈를 만들 때는 반드시 다음 메타데이터를 함께 채운다. 메타데이터가 없는 이슈는 Projects 보드에서 누락되어 가시성이 무너진다.

### 필수 메타데이터

| 항목 | 값 / 규칙 |
| --- | --- |
| **Title** | `[영역] 동사형 작업명` 예: `[Domain] Player 클래스 정의` |
| **Type** | GitHub Issue Type 필드 — `Feature` / `Task` / `Bug` 중 하나 (라벨 아님, 사이드바 `Type` 섹션) |
| **Labels** | `area:*` (1개 이상) — 아래 라벨 가이드 참조. Type 은 라벨이 아니다. |
| **Milestone** | `V0.1` / `V1.0` / `V1.x` 중 하나 |
| **Priority** | Projects 보드 `Priority` 필드 — `P0` / `P1` / `P2` (라벨 아님) |
| **Size** | Projects 보드 `Size` 필드 — `XS` / `S` / `M` / `L` / `XL` (라벨 아님) |
| **Projects** | **`FM-Lite` 보드 (#50, owner: `Devel-Rocket-ClassRoom`)** 추가 → `Status` 자동 분류. 이슈/PR 둘 다 `gh project item-add 50 --owner Devel-Rocket-ClassRoom --url <url>` 로 별도 추가 (생성 명령에서 자동 안 됨). |
| **Assignee** | 본인 |

> **Type 은 GitHub Issue Type 필드** (2024년 도입). 사이드바 "Type" 섹션에서 선택. `type:*` 라벨 (구 방식) 은 2026-01-12 마이그레이션으로 폐지됨.

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

## 4. Issue Type & Labels

### Issue Type (필수, 1개) — 라벨이 아닌 GitHub 자체 필드

이슈 사이드바의 **Type** 섹션에서 선택. 라벨 아님.

| Type | 용도 |
| --- | --- |
| `Feature` | 새 기능 |
| `Task` | 잡일/설정/리팩터링/조사·스파이크 (조사는 본문에 시간 박스 명시) |
| `Bug` | 버그 수정 |

> **이력**: V0.1 초기엔 `type:feature` / `type:task` / `type:bug` **라벨**로 운영. 2026-01-12 GitHub Issue Type 필드로 마이그레이션 (이슈 60개 일괄 처리 + 라벨 3개 삭제).

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

> **Type / Priority / Size 는 라벨이 아니다.** Type 은 GitHub Issue Type 필드. Priority / Size 는 Projects 보드의 단일선택 필드.

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

| v0.1-tasks Task | Issue Title | Type (필드) | Area (라벨) | Milestone | Priority | Size |
| --- | --- | --- | --- | --- | --- | --- |
| Task 1.1 | `[Infra] Unity 프로젝트 생성 및 폴더 구조` | Task | infra | V0.1 | P0 | S |
| Task 2.1 | `[Infra] EventBus 구현` | Feature | infra | V0.1 | P0 | S |
| Task 3.2 | `[Domain] 핵심 도메인 클래스 정의` | Feature | domain | V0.1 | P0 | M |
| Task 6.1 | `[Domain] PlayerGenerator 구현` | Feature | domain,data | V0.1 | P0 | L |
| Task 9.1 | `[Simulation] MatchSimulator 기본 구현` | Feature | simulation | V0.1 | P0 | M |
| Task 10.1 | `[Youth] YouthSystem.GenerateIntake 구현` | Feature | youth | V0.1 | P0 | M |

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
Type: Feature (area 라벨 없음)
```

→ 영역별 필터링 불가. `area:domain` 추가 필수.

### ❌ Type 을 라벨로 추가

```
Labels: type:feature, area:domain
```

→ V0.1 초기 방식. 현재 폐지. **Type 은 사이드바 Issue Type 필드** 사용.

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
   - Type 필드 (사이드바, 라벨 X) — Feature / Task / Bug
   - Area 라벨 1개 이상
   - Milestone (V0.1/V1.0/V1.x)
   - Projects 보드 #50 (FM-Lite) 추가 → Priority, Size 설정
   - 본문 템플릿 채우기
3. 이슈에서 브랜치 생성 (feature/123-xxx)
4. 개발
5. PR 생성 (Closes #123 명시 + 메타데이터 다 채움 — 이슈와 동일)
6. 셀프 머지
```

> **gh CLI 예시 (이슈)**:
> ```bash
> # 1. 라벨 + milestone + assignee 까지만 생성 가능 (Issue Type 은 별도 API)
> gh issue create --title "[Area] 작업명" --label "area:domain" \
>   --milestone "V0.1" --assignee "@me" --body "..."
>
> # 2. 생성된 이슈 # 확인 후 Type 설정
> gh api -X PATCH "repos/{owner}/{repo}/issues/{n}" -f type="Feature"
>
> # 3. Projects 보드 추가 (자동 안 됨 — 항상 별도 호출)
> gh project item-add 50 --owner Devel-Rocket-ClassRoom \
>   --url https://github.com/Devel-Rocket-ClassRoom/minigame-project-gamjaHoo/issues/{n}
> ```

> **gh CLI 예시 (PR)**:
> ```bash
> # 1. PR 생성 — 이슈와 동일하게 메타데이터 채움
> gh pr create --title "..." --label "area:*" --milestone "V0.1" --assignee "@me" --body "..."
>
> # 2. Projects 보드 추가 (자동 안 됨)
> gh project item-add 50 --owner Devel-Rocket-ClassRoom \
>   --url https://github.com/Devel-Rocket-ClassRoom/minigame-project-gamjaHoo/pull/{n}
> ```

작업 완료 시 (PR 머지 직후):

```
1. v0.1-tasks.md의 해당 Task 체크박스 갱신 ([x])
2. 결정 사항 있으면 design-decisions.md 추가
3. 새 이벤트 있으면 event-bus-catalog.md 등록
4. Change Log 갱신
5. PR 본문 Closes #N 이 여러 이슈 닫는 경우 GitHub 가 실제로 close 했는지 검증
   (콤마만 사용 시 첫 항목만 close — §2 "Closes 구문 함정" 참조)
6. 닫힌 이슈마다 보드 메타데이터 입력:
   - 보드 미등록이면 `gh project item-add 50 --owner Devel-Rocket-ClassRoom --url <url>` 추가
   - Start date = Target date = closedAt
   - Iteration = closedAt 이 속한 iteration
   - (§2 "이슈 Close 시 — Start / Target Date / Iteration 자동 입력" 참조)
```

---

## Change Log

| Date | Change |
| --- | --- |
| 2025-05-15 | 초안 작성 (FM-Lite 영역 라벨 반영) |
| 2026-01-12 | Issue Type 마이그레이션 (#90) — `type:feature/task/bug` 라벨 → GitHub Issue Type 필드. 60개 이슈 일괄 마이그레이션 + 라벨 3개 삭제. §3 메타데이터 표 / §4 라벨 가이드 / §7 매핑 표 / §8 Anti-Patterns / §9 Cheatsheet 갱신. |
| 2026-05-20 | PR 메타데이터 규칙 명시 (#121) — 그동안 PR 생성 시 label/milestone/assignee/Projects 누락. §2 "PR Metadata" 신규 섹션 + §3 Projects 항목에 FM-Lite 보드 (#50, owner Devel-Rocket-ClassRoom) 정보 + §9 PR gh CLI 예시 추가. 본 PR 부터 메타데이터 완비 적용. |
| 2026-05-20 | 이슈 close 시 보드 메타데이터 규칙 + `Closes` 구문 함정 — §2 "Closes 구문 함정" 섹션 (콤마만 쓰면 첫 항목만 close, PR #125 가 #40/#41 누락한 사례) + "이슈 Close 시 Start/Target Date/Iteration 자동 입력" 섹션 (사용자 요청: "같은 날 시작/완료라 closedAt 통일"). §9 Cheatsheet 의 작업 완료 흐름에 보드 메타데이터 입력 단계 추가. 그동안 보드 미등록 닫힌 이슈 22개 일괄 추가 + 닫힌 이슈 57개 전체 Start/Target/Iteration 입력 (PowerShell + gh api graphql 일괄 처리). |
