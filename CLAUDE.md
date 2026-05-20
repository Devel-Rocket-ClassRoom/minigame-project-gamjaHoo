# CLAUDE.md

## ⚠️ STOP — Session Start Required Reading

**새 세션이라면 작업 시작 전에 반드시 다음을 모두 읽어라. 토큰 절약 위해 건너뛰면 사용자가 발견하고 되돌리느라 시간 낭비. 다 읽어서 컨텍스트 확보가 최우선.**

매 세션마다:

1. **이 CLAUDE.md** — 끝까지 (특히 ⚠️ Common Pitfalls 섹션)
2. **`docs/design-decisions.md`** — 전체. 결정 사항을 무시한 코드는 거의 확실히 사용자 정정 받음.
3. **`docs/v0.1-tasks.md`** Change Log 마지막 ~15줄 — 현재 진행 상태 파악
4. **작업 종류별 추가 docs**:
   - 도메인 클래스 → `docs/class-diagram.md`
   - 알고리즘 / 생성기 / 시뮬레이션 → `docs/algorithms.md` 해당 섹션 **전부**
   - 시스템 흐름 / 시퀀스 → `docs/data-flows.md` 해당 시퀀스
   - 이벤트 / EventBus → `docs/event-bus-catalog.md`
   - 이슈 / PR / 브랜치 → `docs/github-workflow.md`
   - 코딩 컨벤션 의문 → `docs/coding-conventions.md`

> **읽지 않고 추측으로 진행했을 때 일어난 일들**: `GameManager` 를 Application 으로 옮기자고 잘못 추천 → 사용자 정정. `type:*` 라벨 자동 사용 → 폐지된 방식. `main` 직접 푸시 시도 → 워크플로우 위반. 같은 실수 반복 = 사용자 신뢰 ↓.

---

## ⚠️ Common Pitfalls — 자주 빠지는 함정

**아래는 이 프로젝트에서 반복적으로 발생한 실수들. 작업 전에 한 번 더 의식하라.**

### 아키텍처

- **`GameManager` 는 `FMLite.Core` Layer** (Application 아님). State 보유 + 진입점 역할만. 흐름 조율 (`AdvanceDay` 등) 은 `GameLoop` (Application). 근거: `design-decisions.md` #29.
- **의존 방향**: Domain (외부 의존 0) ← Core ← Application. 역방향 금지. `Domain.asmdef.references = []`. `Core → Application` 참조는 **순환 의존** (불가).
- **Stateless 시스템 (#3)**: Application 시스템 (`MatchSimulator`, `PlayerGenerator` 등) 은 자체 필드 X. `GameState` 입력받아 변경. 예외: `GameManager` 싱글톤.
- **SaveSystem 위치**: Persistence Layer (I/O 어댑터). Application 아님.

### Git / GitHub

- **`main` 직접 푸시 절대 금지**. 항상 **이슈 → 브랜치 → PR → 머지** 패턴. 브랜치 prefix: `feature/<n>-` / `fix/<n>-` / `chore/<n>-` / `docs/<n>-`. `github-workflow.md` §1.
- **이슈 Type = GitHub Issue Type 필드** (사이드바). `type:*` 라벨 사용 금지 — 2026-01-12 폐지됨. gh CLI 로 생성 후 `gh api -X PATCH ... -f type=Feature` 로 설정.
- **커밋 메시지에 `Co-authored-by: Claude ...` 트레일러 추가 금지**. 사용자가 명시적으로 요청 안 했음.
- **PR 본문에 "🤖 Generated with Claude Code" 같은 footer 금지** (위와 같은 이유).

### Unity 환경

- **EditMode / PlayMode 테스트는 사용자가 Unity Test Runner 에서 실행**. Claude 가 직접 실행 불가. 코드 / 테스트 작성 후 사용자에게 ⏸️ 확인 요청 → 통과 후 commit.
- **SO 시드 asset (`Balance/GameBalance.asset` 등) 갱신은 사용자가 `FM-Lite/Seed/Generate V0.1 Data` 메뉴 실행 후 별도 chore PR**. 코드 PR 과 분리.
- **LF/CRLF 경고** — Windows + git core.autocrlf 정상 동작. 무시.

### 코드

- **매직 넘버 금지** — `GameBalanceSO` 외부화. `design-decisions.md` #11.
- **부동소수점 비교에 epsilon** — `balance.float * x` 후 비교 시 `0.8f`, `1.20f` 등이 정확히 표현 안 됨. `tierEliteRatio - TierEpsilon` 패턴.
- **ID 기반 참조** — 도메인 객체 간 직접 참조 금지. `int playerId` 등 ID 만. `design-decisions.md` #1.
- **명세 우선** — 알고리즘 / 시스템 구현 시 `algorithms.md` 또는 `data-flows.md` 의 의사코드 / 시퀀스 그대로 따라라. 명세와 코드가 다르면 명세 갱신 또는 코드 정정.

### 표면 정합성 함정 — 본질 분석 필수

**여러 문서가 X 라 적어도 X 가 정답이 아닐 수 있다.** GameManager 사례:
- `project-context.md` + `class-diagram.md` 가 "Application Layer — GameManager" 로 적혀있었으나
- 두 문서가 자기모순 (`class-diagram.md` Layer Overview 와 본문 섹션 분류 불일치)
- 본질 분석 (인프라/컨테이너 vs Stateless 도메인 변환) 결과 Core 가 정답

**작업 시작 전 체크**: 표면적 일관성에 끌리지 말고 *왜 이렇게 되어 있나* 질문. 디자인 의도 / 본질 / 다른 시스템과의 응집도. `design-decisions.md` 의 V1.0+ 보완 포인트도 함께 검토.

---

## Project: FM-Lite

컴팩트 FM 클론. 육성 중심 축구 매니저 게임. Unity / C# / 1인 개발 / 3주 스코프.

- **프로젝트 컨텍스트**: `docs/project-context.md`
- **설계 결정사항**: `docs/design-decisions.md`
- **코딩 컨벤션**: `docs/coding-conventions.md`
- **클래스 다이어그램**: `docs/class-diagram.md`
- **데이터 흐름 / 시퀀스**: `docs/data-flows.md`
- **이벤트 카탈로그**: `docs/event-bus-catalog.md`
- **V0.1 작업 체크리스트**: `docs/v0.1-tasks.md`
- **알고리즘 명세**: `docs/algorithms.md`
- **GitHub 워크플로우**: `docs/github-workflow.md`
- **Unity MCP 가이드**: `docs/unity-mcp.md`
- **용어집**: `docs/glossary.md`

## Workflow

이 프로젝트는 다음 도구들을 병행 사용한다:

- **채팅 인터페이스 (Claude)** — 설계 / 디자인 결정 / 알고리즘 명세
- **Claude Code (with Unity MCP)** — 코드 작성, 리팩터링, git / PR / 이슈 관리
- **Unity AI Assistant (2026 베타)** — Unity 에디터 안 작업 (씬·Inspector·Profiler·콘솔 + 콘텐츠 생성). 채팅 창 하나에서 Ask / Agent 두 모드. 2026 베타부터 Generators 도 Agent 안으로 흡수 — 단일 진입점.
- **GitHub Issues / Projects** — 작업 관리

도구별 역할 분담 매트릭스는 `docs/unity-mcp.md` 참조. 작업 관리 규칙은 `docs/github-workflow.md` 참조.

### 한 줄 룰 (도구 선택)

- **에디터 화면 안 일** (씬 / Inspector / 콘솔 / Profiler / 콘텐츠 생성) → **Unity AI Assistant**
- **다중 파일 / git / 자동화 / 셸 / 문서** → **Claude Code** (터미널)
- **씬-코드 연결 (디버그 / 에디터 확장 / SO 일괄 처리)** → Claude Code + Unity MCP

Stage 13 UI 진입 후 씬·프리팹·UGUI 작업은 Unity AI Assistant 비중↑.
Claude Code 는 코드 / 문서 / Editor 스크립트 (DebugWindow 같은) 영역.

### Claude Code 자동 포매팅 (CSharpier)

`.cs` 파일을 Edit/Write 한 직후 `.claude/hooks/csharpier-format.ps1` 이
자동 호출되어 CSharpier 포맷 적용. 수동 호출은 `dotnet csharpier format <path>`.
설정 (`.editorconfig`, `.config/dotnet-tools.json`, `.claude/settings.json`) 은
이슈 #142 셋업.

### 새 작업 시작 시 (Issue 기반)

1. GitHub Issue 확인 (Title, Type, Area, Milestone, Priority, Size, DoD)
2. `docs/v0.1-tasks.md`에서 매칭되는 Task 찾기
3. 의존 문서 읽기:
   - 알고리즘 구현 → `docs/algorithms.md` 해당 섹션 (필수)
   - 시스템 구현 → `docs/data-flows.md` 해당 시퀀스
   - 이벤트 관련 → `docs/event-bus-catalog.md`
4. 문서에 `QUESTION:` 또는 `TBD:` 마커가 있는지 확인
5. 마커가 작업에 영향 → **작업 중단, 사용자에게 확인 요청**
6. 명확하면 진행
7. 작업 완료 시 Issue 본문 DoD 체크 + 관련 문서 갱신

## Session Start Checklist

새 작업 시작 시 반드시:

1. 작업 대상 GitHub Issue 또는 `docs/v0.1-tasks.md` Task 확인
2. 해당 작업의 "완료 조건" / DoD 명시적으로 확인
3. 의존 문서 읽기 (위 Workflow 참조)
4. 문서 내 미해결 마커 확인
5. 명확하면 진행

## Decision Logging

작업 중 새 결정이 필요하면:

- 즉흥적으로 결정하지 말 것 (Karpathy 원칙 1번)
- 단순한 선택지면 사용자에게 묻기
- 큰 결정이면 채팅 세션 복귀 권장
- 결정된 사항은 즉시 해당 문서에 추가 (`design-decisions.md` 등)
- Change Log 갱신

## GitHub Issue 작성 시

`docs/github-workflow.md`의 메타데이터 규칙을 반드시 따른다:
- Title: `[영역] 동사형 작업명`
- **Type (GitHub Issue Type 필드, 라벨 X)** — `Feature` / `Task` / `Bug` 중 1개. `gh issue create` 후 `gh api -X PATCH "repos/{owner}/{repo}/issues/{n}" -f type="Feature"` 로 설정.
- Area 라벨 (`area:domain` / `area:simulation` 등) — 1개 이상
- Milestone (`V0.1` / `V1.0` / `V1.x`)
- Projects 보드 추가 → Priority (P0/P1/P2), Size (XS~XL)
- 본문 템플릿 채우기 (DoD 포함)

메타데이터 누락된 이슈는 보드에서 누락되므로 항상 확인.

> `type:feature` / `type:task` / `type:bug` 라벨은 **2026-01-12 폐지** (Issue Type 필드로 마이그레이션). 사용 금지.

## Unity MCP 사용 시

`docs/unity-mcp.md`의 원칙을 따른다:
- 소스 파일 작성은 Claude Code 주도 (Unity MCP는 보조)
- 작은 단위로 위임
- Unity 에디터 상태 변경 작업은 사용자에게 의도 확인 후 진행
- Asset Database 충돌 방지 (Unity 닫고 작업 또는 Refresh)

---

# Behavioral Guidelines

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:

- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
