# Unity MCP & AI Assistant 활용 가이드

이 프로젝트는 다음 AI 도구를 병행 사용한다:

1. **Claude Code (with Unity MCP)** — 터미널/VSCode 에서 코드 작성, 필요 시 Unity 에디터 조작 (MCP 경유)
2. **Unity AI Assistant** — Unity 에디터 안 채팅 (Ask / Plan / Agent)
3. **Unity Generators** — 자연어 → 스프라이트 / 텍스처 / 머티리얼 / 사운드 / 간단 3D 생성

각 도구의 역할과 사용 원칙을 정의한다.

---

## 0. Unity AI 베타 2026 — 무엇이 어디 있나 (Unity 6.3+)

> Unity 6.3 이상에서 동작하는 Unity 자체 AI 묶음. Claude Code 와는 별개.
> 베타라 모델·과금·기능 자주 변동. **2026 베타부터 Assistant 가 단일 진입점.**

### UI 차원 — 단일 채팅 창 (Assistant)

Unity 에디터 우측 패널의 **AI Assistant 창 하나에서 모두 처리**.
2025 베타의 별도 Generators 패널 / Plan 모드는 2026 베타에서 **Assistant Agent 모드로 흡수**.

- **Ask 모드** — 읽기·설명·분석. 코드 / 씬 / 에셋 / Profiler / 콘솔 로그 / 스크린샷 첨부 가능 (Vision 모델).
- **Agent 모드** — 실제 쓰기 작업 + 계획 + 자산 생성까지 **하나의 채팅으로 오케스트레이션**.
  - 스크립트 작성·수정 / 씬 구성 / 컴포넌트 추가
  - 스프라이트 / 텍스처 / 머티리얼 / 큐브맵 / 사운드 / **프로덕션급 3D** 생성
  - UI Toolkit 레이아웃 자동 생성 / Figma → UGUI

프롬프트 직전 **Git 자동 체크포인트** → 결과 마음에 안 들면 그 시점 롤백.
Allow / Ask Permission / Deny 3분류로 자동 실행 범위 제어.

### 패키지 차원 — 여전히 분리

UI 는 통합이지만 Package Manager 에선 별도 패키지 (의존 자동 설치):

| 패키지 | 역할 |
| --- | --- |
| `com.unity.ai.assistant` | 채팅 UI + Ask·Agent 모드 + 오케스트레이션 |
| `com.unity.ai.generators` | 실제 생성 백엔드 (Assistant 가 호출). 별도 윈도우는 2026 비활성 |
| `com.unity.ai.gateway` | 외부 코딩 에이전트 (Claude Code · Codex 등) 연결 |
| `com.unity.ai.mcp` (또는 MCP Server 내장) | Unity 를 MCP 서버로 노출 — 외부 IDE/CLI 가 씬·콘솔 조작 |

### Asset Knowledge
프로젝트 에셋 (텍스처·머티리얼·GameObject) 을 **로컬** 임베딩해 시맨틱 검색.
클라우드로 안 나감 → 프롬프트 안에서 "이 같은 에셋 / GameObject" 라고 명시 가능.

### AI Gateway (외부 에이전트 → Unity 안)
- Assistant 창에서 에이전트 드롭다운으로 외부 도구 선택 (Claude Code · Codex · Gemini · Cursor)
- 로컬 머신에서 본인 자격증명으로 실행
- Gateway 자체는 Unity 크레딧 미소모 — 외부 에이전트 API 키 / 구독은 별도
- Claude Code 는 2.1.45 이상 필요

### Unity MCP Server (Unity → 외부 에이전트)
- Unity 가 MCP 서버 → 외부 클라이언트 (Claude Code 등) 에 씬·콘솔·GameObject·에셋을 도구로 노출
- 외부 클라이언트가 부를 수 있는 도구 예:
  - `Unity_ReadConsole` — 콘솔 로그 읽기
  - `Unity_ManageScene` — 씬 열기·저장·계층 조회
  - `Unity_ManageGameObject` — GameObject 생성·이동·컴포넌트 추가
- Unity 가 켜져 있을 동안만 연결 가능 (Bridge 가 떠 있음)

### 2026 베타 변경점 요약 (2025 → 2026)
- Assistant 모드 3개 (Ask / Plan / Agent) → **2개 (Ask / Agent)**. Plan 은 Agent 가 직접 계획 + 실행.
- Generators 별도 메뉴 → **Assistant 채팅 안에서 호출**. UI 통합.
- 신규: Vision 모델 (스크린샷 분석) / 프로덕션급 3D / UI Toolkit 레이아웃 생성 / 향상된 오케스트레이션.
- 2025 베타는 2026-01-12 종료 — 포인트 할당 중단.

---

## 0.1 환경 셋업 (이슈 #142, 2026-05-20)

### 자동 셋업 (Claude Code 가 한 것)
- `.claude/settings.json` — Unity 권장 권한 (Library / Temp / Logs / obj 읽기 deny / .unity·.meta 편집 deny / 자주 쓰는 git·dotnet 명령 allow / PostToolUse CSharpier hook)
- `.editorconfig` — LF / final newline / C# indent 4
- `.config/dotnet-tools.json` — CSharpier 로컬 도구 매니페스트
- `.claude/hooks/csharpier-format.ps1` — Edit/Write 직후 .cs 파일 자동 포맷
- `CLAUDE.md` — Unity AI 도구 분담 한 줄 룰 + CSharpier 가이드

### 수동 셋업 (사용자가 해야 할 것)

**A. Unity AI Assistant 패키지 설치**
1. Unity 에디터 → `Window > Package Manager`
2. 좌측 상단 드롭다운 `Packages: Unity Registry` 선택
3. 검색 `AI Assistant` → Install
4. (선택) `AI Generators` 도 같이 Install
5. 에디터 재시작 시 의존 패키지 자동 설치

**B. Unity MCP Server 활성화 + Claude Code 연결**
1. `Edit > Project Settings > AI > Unity MCP`
2. `Unity Bridge` 상태가 `Running` (녹색) 확인 — `Stopped` 면 `Start` 클릭
3. `Integrations` 섹션 펼치고 `Claude Code` 행의 `Configure` 클릭
4. 터미널에서 새 Claude Code 세션 시작
5. 첫 호출 시 같은 설정 페이지 `Pending Connections` 에 Claude Code 항목 표시 → `Accept`
6. 이후 `Connected Clients` 에 표시되며 자동 재연결

**C. 연결 확인**
- Claude Code 세션에서 `/mcp` → 등록된 서버 목록에 `unity` + 상태 `connected`
- 시험 호출: "유니티 콘솔의 최근 에러 한 줄 알려줘" — Unity 가 켜져 있어야 응답

**D. CSharpier IDE 통합 (선택)**
- VS Code: `csharpier.csharpier-vscode` 플러그인 설치
- Rider: 마켓플레이스 CSharpier 플러그인
- IDE 저장 자동 포맷이 Claude Code hook 과 동일한 결과 보장

### 베타 주의사항
- Unity 가 빌드·임포트·컴파일 중일 때 MCP 응답 지연 — 기다렸다 재시도
- AI Assistant / Generators 는 Unity 크레딧 소모 — 베타 플랜 한도 확인
- 공식 MCP 한도 초과 시 커뮤니티 MCP (예: Coplay `unity-mcp`) 대안 — 출처 검토 후 도입

---

## 1. 도구별 역할 분담

### Claude Code (VSCode)

**주 용도:** 코드 작성, 리팩터링, 명세 기반 구현

**적합한 작업:**
- 도메인 클래스 작성
- 시스템 / 알고리즘 구현
- 세이브/로드 / EventBus 같은 인프라
- 코드 리팩터링
- 디버그 도구 작성
- 문서 업데이트

**Unity MCP 활용:**
- Unity 에디터에서 씬 정보 / 게임오브젝트 조회
- SO 인스턴스 자동 생성
- 컴파일 에러 확인
- 기본 프리팹 구성 자동화

### Unity AI Assistant (Unity 에디터)

**주 용도:** Unity 에디터 작업, 시각적 작업

**적합한 작업:**
- UI 레이아웃 구성
- 프리팹 셋업 / 컴포넌트 연결
- 씬 구성 (게임오브젝트 배치)
- 머터리얼 / 라이팅
- ScriptableObject 인스턴스 데이터 입력
- 인스펙터 기반 디버깅

---

## 2. 작업 분담 가이드

| 작업 종류 | 권장 도구 | 이유 |
| --- | --- | --- |
| 새 클래스 작성 | Claude Code | 명세 기반 코드 |
| 알고리즘 구현 | Claude Code | algorithms.md 참조 필요 |
| 리팩터링 | Claude Code | 코드 일관성 |
| UI 화면 만들기 | Unity AI Assistant | 인스펙터 기반 작업 |
| 프리팹 셋업 | Unity AI Assistant | 시각적 작업 |
| SO 에셋 만들기 | 둘 다 (Claude Code로 생성 후 데이터 입력) | |
| 디버그 윈도우 | Claude Code | 코드 위주 |
| 씬 구성 | Unity AI Assistant | 게임오브젝트 배치 |
| 머터리얼 / 셰이더 | Unity AI Assistant | Unity 종속 |

### 협업 예시

```
1. [Claude Code] PlayerGenerator.cs 작성
2. [Claude Code] GameBalanceSO.cs 정의
3. [Unity AI Assistant] GameBalanceSO 인스턴스 생성 + 수치 입력
4. [Unity AI Assistant] 디버그 씬 구성, 테스트
5. [Claude Code] 발견된 버그 수정
```

---

## 3. Claude Code + Unity MCP 사용 원칙

### 원칙 1: 코드는 Claude Code 주도

Unity MCP가 코드를 직접 작성 가능하더라도, **소스 파일 작성은 Claude Code에서 한다.** 이유:

- Git 추적 일관성
- 코딩 컨벤션 (coding-conventions.md) 적용
- 문서와 동기화 (명세 → 코드)

Unity MCP는 보조 도구로만:
- 씬 정보 조회 (디버깅용)
- SO 인스턴스 생성 (코드 자체는 Claude Code에서 작성한 SO 클래스 기반)
- 컴파일 상태 확인

### 원칙 2: 작은 단위로 위임

Karpathy 원칙 그대로 적용:

```
❌ "Unity에 게임 전체 만들어줘"
✅ "PlayerGenerator.cs 작성한 다음, Resources/Data/Balance/ 에 GameBalanceSO 인스턴스 하나 생성해줘"
```

### 원칙 3: Unity MCP 호출 전 확인

Unity 에디터 상태를 변경하는 작업 (씬 저장, 컴포넌트 추가 등)은:

1. 사용자에게 의도 확인
2. 변경 전 백업 (씬 저장 등)
3. 변경 후 결과 보고

### 원칙 4: 에디터 의존 작업 분리

Unity 에디터에서만 가능한 작업 (Asset Database 갱신, 인스펙터 설정 등)은 Unity AI Assistant로 위임 권장. Claude Code에서 무리하게 자동화하지 말 것.

---

## 4. Unity AI Assistant 사용 원칙

### 원칙 1: 코드 자동 생성 자제

Unity AI Assistant가 스크립트 생성을 제안해도, 새 스크립트 생성은 Claude Code에서. 이유:

- 코딩 컨벤션 일관성
- 폴더 위치 / 네임스페이스 일관성
- 명세 기반 작성 보장

**예외:** UI 컨트롤러처럼 매우 간단한 MonoBehaviour는 Unity AI Assistant로 OK. 단, coding-conventions.md 준수 확인.

### 원칙 2: 인스펙터 작업에 활용

- 프리팹 컴포넌트 연결
- ScriptableObject 데이터 입력
- 씬 게임오브젝트 배치
- 머터리얼 설정

이런 작업은 Unity AI Assistant가 효율적.

### 원칙 3: 디자인 결정은 사람이

Unity AI Assistant가 "이런 레이아웃 어떠세요?" 제안해도, **UI / 게임 디자인 결정은 사람이 한다.** AI는 작업 실행만.

---

## 5. 두 도구 간 컨텍스트 동기화

### 문제

Claude Code와 Unity AI Assistant는 서로 메모리를 공유하지 않는다.

### 해결

**1. 단일 진실의 원천(SoT) 유지**

- 코드 = Git 저장소
- 디자인 결정 = `docs/design-decisions.md`
- 알고리즘 = `docs/algorithms.md`
- 작업 진행도 = GitHub Issues + `docs/v0.1-tasks.md`

두 도구 모두 위 SoT를 참조.

**2. 도구 간 핸드오프**

```
[Claude Code] 작업 완료
  ↓
GitHub Issue 업데이트 + 문서 갱신 + 커밋
  ↓
[Unity AI Assistant] 작업 시작 시
  - 최신 커밋 pull
  - 관련 문서 확인
  - 작업 진행
```

**3. 결정사항은 즉시 문서로**

Unity AI Assistant 사용 중 새 결정이 발생하면:
- `design-decisions.md`에 추가
- 다음 Claude Code 세션에서 인식

---

## 6. 실용 예시

### 예시 1: PlayerGenerator 구현 + 테스트

```
[Claude Code]
1. algorithms.md #1 명세 확인
2. PlayerGenerator.cs 작성
3. GameBalanceSO 필드 추가
4. 단위 테스트 작성 (선택)
5. 커밋 + 이슈 close

[Unity AI Assistant]
6. GameBalanceSO 인스턴스 생성
7. 인스펙터에서 수치 입력
8. 테스트 씬에서 PlayerGenerator 호출
9. 결과 확인
```

### 예시 2: 유스 풀 UI 화면

```
[Claude Code]
1. YouthPoolView.cs (컨트롤러) 작성
2. EventBus 구독 / 데이터 바인딩 로직
3. 커밋

[Unity AI Assistant]
4. UI 프리팹 생성 (Canvas, Panel, ScrollView 등)
5. YouthPoolView 컴포넌트 연결
6. 필드 참조 설정 (인스펙터)
7. 씬에 배치
8. 플레이 모드 확인
```

### 예시 3: 디버그 씬 셋업

```
[Unity AI Assistant]
1. 빈 씬 생성: Debug_Test.unity
2. GameManager 게임오브젝트 배치
3. 디버그 UI 추가 (테스트 버튼들)
4. 씬 저장

[Claude Code]
5. 디버그 윈도우 코드 보강 (필요 시)
```

---

## 7. 주의사항

### Unity MCP 비활성화 시 대응

Unity MCP가 연결되지 않은 상태에서 Claude Code 작업 시:
- Unity 에디터 조작 작업은 사용자에게 위임
- Claude Code는 코드 작성에 집중
- 사용자가 Unity에서 수동 작업 후 결과 보고

### Asset Database 충돌 방지

Unity가 열려있는 상태에서 외부 도구가 파일 시스템을 변경하면 충돌 가능. 해결:
- 큰 변경 시 Unity 닫고 작업
- 또는 변경 후 Unity에서 `Assets → Refresh`

### 두 AI 동시 사용 금지

같은 시점에 Claude Code와 Unity AI Assistant가 같은 파일을 변경하면 충돌. 한 번에 하나만.

---

## 8. 워크플로우 요약

```
설계 / 의사결정
    ↓
채팅 인터페이스 (Claude) → docs/ 업데이트
    ↓
코드 작성
    ↓
Claude Code (with Unity MCP) → Git 커밋
    ↓
Unity 에디터 작업 (씬, 프리팹, SO 데이터)
    ↓
Unity AI Assistant → Asset 변경 → Git 커밋
    ↓
플레이 테스트
    ↓
발견 사항 → docs/ 업데이트 → 새 이슈
```

---

## Change Log

| Date | Change |
| --- | --- |
| 2025-05-15 | 초안 작성 |
| 2026-05-20 | 이슈 #142 — Unity AI 베타 4 컴포넌트 (Assistant / Generators / AI Gateway / Unity MCP Server) 정리 + 환경 셋업 절차 (자동 / 수동) 추가. `.claude/settings.json` / `.editorconfig` / `.config/dotnet-tools.json` / `.claude/hooks/csharpier-format.ps1` 도입. Stage 13 UI 진입 전 셋업. |
