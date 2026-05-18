# CLAUDE.md

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
- **Claude Code (with Unity MCP)** — 코드 작성, 리팩터링
- **Unity AI Assistant** — Unity 에디터 작업 (씬, 프리팹, SO 데이터)
- **GitHub Issues / Projects** — 작업 관리

도구별 역할 분담은 `docs/unity-mcp.md` 참조. 작업 관리 규칙은 `docs/github-workflow.md` 참조.

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
- Type 라벨 (`type:feature` / `type:bug` / `type:task`) — 1개
- Area 라벨 (`area:domain` / `area:simulation` 등) — 1개 이상
- Milestone (`V0.1` / `V1.0` / `V1.x`)
- Projects 보드 추가 → Priority (P0/P1/P2), Size (XS~XL)
- 본문 템플릿 채우기 (DoD 포함)

메타데이터 누락된 이슈는 보드에서 누락되므로 항상 확인.

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
