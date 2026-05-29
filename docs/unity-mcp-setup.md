# Unity MCP 셋업 — V1.0 Stage 0

> **목적:** Claude Code (터미널) 가 Unity Editor 를 **직접 조작** (씬 보기 / Hierarchy 조회 / GameObject 생성·수정 / 컴포넌트 와이어링 / prefab 편집 / 메뉴 실행 등) 할 수 있게 MCP (Model Context Protocol) 서버를 셋업한다. 셋업이 성공하면 지금까지 `docs/unity-ai/*.md` 지시서를 사용자가 Unity AI Assistant 에 복붙하던 워크플로우 일부를 Claude Code 가 직접 수행 가능.
>
> **중요:** 이 셋업은 V1.0 의 **첫 작업 (Stage 0)**. 사용자가 과거 시도 실패한 영역. 본 문서는 *안 될 경우* 대비 fallback 4단계까지 모두 명세함.
>
> **환경 (2026-05-29 확인):**
> - Unity: `6000.3.15f` (Unity 6.3 시리즈, URP)
> - Claude Code: 터미널 네이티브 (PowerShell on Windows 11)
> - 운영체제: Windows 11 Pro
> - 프로젝트 경로: `c:\Users\jjiho\source\repos\minigame-project-gamjaHoo`
>
> **선행 문서:** `docs/unity-mcp.md` (역할 분담 매트릭스) / `docs/unity-ai/_TEMPLATE.md` (현재 Unity AI 지시서 패턴) / `CLAUDE.md` (Unity 환경 주의사항)

---

## 0. 의사결정 트리 — 어느 옵션부터?

본 문서는 4 옵션을 순차 시도 권장. 각 옵션 실패 시 다음으로 이동.

```
[옵션 A] CoplayDev/unity-mcp (커뮤니티 표준)        ← 1순위
   ↓ 실패 시
[옵션 B] Unity 공식 MCP (com.unity.ai.assistant)   ← 2순위
   ↓ 실패 시
[옵션 C] IvanMurzak/Unity-MCP                       ← 3순위
   ↓ 실패 시
[옵션 D] 기존 unity-ai 지시서 패턴 유지              ← Fallback (현 상태 유지)
```

### 옵션 비교표

| 항목 | A) CoplayDev | B) Unity 공식 | C) IvanMurzak | D) 현 unity-ai md |
|---|---|---|---|---|
| 호환 Unity | 2022.3+ | **6.0+ 필수** | 2022.3+ | 무관 |
| 호환 Claude Code | ✅ | ✅ | ✅ | ✅ (수동 복붙) |
| 설치 난이도 | 중 | 저 (Unity 내장) | 중 | 0 |
| 도구 풍부도 | ~50 (asset/scene/script/test) | 공식 카탈로그 | ~70 (asset/scene/script) | N/A |
| Windows 네이티브 함정 | 있음 (#773) | 적음 (릴레이 내장) | 적음 | 0 |
| Unity 6.3 DLL 충돌 | 없음 | **있음 (수동 fix 필요)** | 미확인 | 무관 |
| 커뮤니티 / 문서 | ★★★ | ★★ | ★★ | N/A |
| 권장 (우리 환경) | **1순위** | 2순위 | 3순위 | 모두 실패 시 |

> **권장 1순위 — CoplayDev**: 가장 성숙. Claude Code 공식 가이드. Unity 6.3 에서 DLL 충돌 없음.
> **2순위 — Unity 공식**: 가장 깔끔하나 6.3 에서 `System.Collections.Immutable` 충돌 → DLL 수동 설치 필요.

---

## 1. 옵션 A — CoplayDev/unity-mcp (1순위)

### 1.1 사전 조건

| 도구 | 최소 버전 | 확인 방법 | 미설치 시 |
|---|---|---|---|
| Unity | 6000.3.15f (현재) ✓ | Unity Hub | 이미 OK |
| Python | 3.11 이상 | `python --version` (PowerShell) | https://www.python.org/downloads/ (최신 3.12 권장) |
| uv | 최신 | `uv --version` | `irm https://astral.sh/uv/install.ps1 \| iex` (PowerShell) |
| Claude Code | 최신 | `claude --version` | 이미 설치됨 (이 채팅 진행 중) |
| Git | 임의 | `git --version` | 이미 설치됨 (프로젝트가 git repo) |

> **`uv` 가 핵심**: MCP 서버 실행 런타임. Astral 의 빠른 Python 패키지 매니저. 한 줄 설치 / 시스템 격리.

#### 1.1.1 사전 조건 검증 절차

PowerShell 새 창에서:

```powershell
# 모두 정상 출력되어야 함
python --version    # Python 3.11.x 또는 3.12.x
uv --version        # uv 0.4.x 이상
claude --version    # claude-code 0.x.x
git --version       # git version 2.x.x
```

**`uv` 미설치 시 (가장 흔함)**:

```powershell
# 관리자 PowerShell 권장
irm https://astral.sh/uv/install.ps1 | iex

# 설치 후 새 터미널 열어 확인
uv --version
```

**Python 미설치 시**:
- https://www.python.org/downloads/ 에서 3.12 LTS 다운로드
- 설치 시 **`Add Python to PATH` 반드시 체크**
- 새 터미널 열어 `python --version` 재확인

### 1.2 설치 — Unity 측

1. Unity Editor 에서 프로젝트 (`minigame-project-gamjaHoo`) 오픈.
2. **Window → Package Manager** 클릭.
3. 좌상단 `+` 드롭다운 → **Add package from git URL...**
4. URL 입력:

   ```
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
   ```

   > **버전 안정성을 원한다면** `#main` 대신 특정 태그 (예: `#v9.4.6`) 사용. 단, 본 문서 작성 시점 (2026-05-29) 기준 `main` 이 가장 최신 안정.

5. **Add** 클릭 → Unity 가 패키지 다운로드 + 컴파일 (~30초). 콘솔에 에러 없는지 확인.

#### 1.2.1 Unity 6.3 호환성 — DLL 충돌 점검

CoplayDev unity-mcp 는 Unity 6.3 에서 **공식적으로 DLL 충돌 없음**. (Unity 공식 MCP 만 `System.Collections.Immutable` 충돌 — 옵션 B 참조.)

다만 컴파일 에러가 보이면:
- Console 의 첫 에러 → 보통 누락된 dependency
- Package Manager → MCP for Unity → Dependencies 확인
- 만약 `System.IO.Pipelines` / `System.Threading.Channels` 등 누락 → NuGet for Unity 패키지 추가 검토

### 1.3 설치 — Claude Code 측 (자동 셋업 권장)

#### 1.3.1 권장 — Auto-Setup

1. Unity 메뉴 → **Window → MCP for Unity** → 윈도우 열림.
2. **Configure All Detected Clients** 클릭.
3. Claude Code 가 자동 등록됨 (사용자 PATH 의 `claude` CLI 검출). 상태 `Connected ✓` 로 변하면 성공.

#### 1.3.2 수동 — Auto-Setup 실패 시

자동 등록이 안 되면 (예: Claude Code 가 비표준 경로) 수동 등록:

```powershell
# user scope (모든 프로젝트에서 사용 가능) — 권장
claude mcp add --scope user --transport stdio coplay-mcp `
  --env MCP_TOOL_TIMEOUT=720000 `
  -- uvx --python ">=3.11" coplay-mcp-server@latest
```

> **scope**: `user` = 전역 / `project` = 이 프로젝트만. user 권장 — 다른 Unity 프로젝트도 동일 셋업 활용 가능.
> **MCP_TOOL_TIMEOUT**: 일부 도구 (예: Unity reimport) 가 오래 걸려 default timeout (60s) 초과 → 720000 (12분) 설정.

등록 확인:

```powershell
claude mcp list
# coplay-mcp (또는 unityMCP) 항목이 보여야 함
```

**Windows 네이티브에서 Issue #773 회피** (uvx --from 자동 config 실패 시):

```powershell
# 우회 — uv --directory 방식 (CoplayDev wiki 권장 우회책)
# 1. Python 서버 코드 위치 확인 (Unity 가 설치한 곳)
#    보통: Library/PackageCache/com.coplaydev.unity-mcp@<hash>/Python
#    또는: ~/AppData/Local/CoplayDev/UnityMCP/Python

# 2. 수동 등록 (위 자동 등록이 실패할 때만)
claude mcp add-json "unityMCP" '{"command":"uv","args":["--directory","C:\\Users\\jjiho\\AppData\\Local\\CoplayDev\\UnityMCP\\Python","run","server.py"]}'
```

### 1.4 검증

#### 1.4.1 Claude Code 재시작

MCP 등록 후 **Claude Code 반드시 재시작**. 현재 터미널 세션 종료 → 새 세션 시작.

```powershell
# 새 PowerShell 창에서
cd c:\Users\jjiho\source\repos\minigame-project-gamjaHoo
claude
```

#### 1.4.2 도구 인식 확인

Claude Code 안에서:

```
/mcp
```

→ `unityMCP` (또는 `coplay-mcp`) 가 `connected` 상태로 나오면 OK. `tools` 섹션에 `mcp__unityMCP__*` 시리즈 (manage_scene / manage_asset / manage_script / run_tests 등) 가 나열되어야 함.

#### 1.4.3 첫 호출 — Smoke Test

사용자가 Claude Code 에 다음 프롬프트:

```
"Unity Hierarchy 에 빈 GameObject 를 'MCPTest' 라는 이름으로 만들어줘."
```

Claude Code 가 `mcp__unityMCP__manage_gameobject` (또는 유사 이름) 도구 호출 → Unity Hierarchy 에 `MCPTest` 가 즉시 나타나면 성공.

> **첫 연결 승인**: Unity 측에서 "Pending Connections" 알림이 뜰 수 있음 → MCP for Unity 윈도우의 Pending 탭에서 승인.

#### 1.4.4 우리 프로젝트 검증 — 실제 작업 흐름 테스트

```
"현재 열려있는 씬의 이름과 루트 GameObject 목록을 보여줘."
```

→ 현재 씬 (예: `DashboardScene`) 의 Hierarchy 가 응답에 나오면, 본 작업이 가능한 상태.

다음 단계 (실제 V1.0 작업):
```
"DashboardScene 의 모든 Canvas 루트 자식을 나열해줘. 각 자식의 RectTransform anchor 와 position 도 함께."
```

이 정도가 가능하면 글로벌 네비게이션 (TopBar/SideBar) prefab 작업이 Claude Code 주도로 가능.

### 1.5 일상 사용 패턴

- **Unity Editor 가 실행 중일 때만** MCP 서버 활성 (Editor 가 호스트).
- 코드 변경 후 컴파일 진행 중일 때는 도구 호출 대기 (Unity 가 도메인 리로드 중).
- Play Mode 진입 시 일부 도구 (씬 편집) 차단됨. Edit Mode 권장.
- **세션 시작 시 체크리스트**:
  1. Unity Editor 열려 있는지
  2. Console 에 컴파일 에러 없는지
  3. `/mcp` 로 connection 상태 확인
  4. 작업 시작

---

## 2. 옵션 B — Unity 공식 MCP (2순위)

### 2.1 사전 조건

| 도구 | 요구사항 |
|---|---|
| Unity | 6.0+ (현재 6.3 ✓) |
| `com.unity.ai.assistant` | 2.7+ |
| Claude Code | 최신 |
| `uv` | 불필요 (릴레이 바이너리 내장) |
| Python | 불필요 |

> **장점**: 의존성 최소. Unity 자체가 릴레이를 `~/.unity/relay/` (Windows: `%USERPROFILE%\.unity\relay\`) 에 자동 설치.
> **단점**: Unity 6.3 + `com.unity.ai.assistant` 조합에서 `System.Collections.Immutable` v9.0.0 DLL 충돌 → 수동 fix 필수.

### 2.2 설치

1. Unity Package Manager → Unity Registry → `Unity AI Assistant` 검색 → 2.7+ 설치.
2. 메뉴 **Edit → Project Settings → AI → Unity MCP Server** 열기.
3. **Status: Stopped** 이면 **Start** 클릭. Running 으로 변경 확인.

#### 2.2.1 Unity 6.3 DLL 충돌 수정 — 필수

증상: Console 에 `System.Collections.Immutable, Version=X.X.X.X` 관련 `TypeLoadException`.

해결:
1. https://www.nuget.org/packages/System.Collections.Immutable/9.0.0 다운로드.
2. `.nupkg` 파일을 zip 으로 열기 → `lib/netstandard2.0/System.Collections.Immutable.dll` 추출.
3. `Assets/Plugins/System.Collections.Immutable.dll` 에 배치.
4. Unity 가 자동 import. 인스펙터에서 Asm Def 설정:
   - **Auto Reference: false**
   - **Validate References: false**
   - Plugin Platform Settings → **Any Platform: true**
5. 재컴파일 후 에러 해소 확인.

### 2.3 Claude Code 등록

Unity 가 릴레이 바이너리를 자동 설치. 경로 확인 후 등록:

```powershell
# 릴레이 경로 확인 (Windows)
ls $env:USERPROFILE\.unity\relay\

# 보통: %USERPROFILE%\.unity\relay\unity-mcp-relay.exe
```

```powershell
# Claude Code 등록 (예시 — 실제 경로 확인 후 사용)
claude mcp add --scope user --transport stdio unity-official `
  -- "$env:USERPROFILE\.unity\relay\unity-mcp-relay.exe" --mcp `
     --project-path "c:\Users\jjiho\source\repos\minigame-project-gamjaHoo"
```

> **`--mcp` 플래그 필수**: 릴레이를 MCP 서버 모드로 동작시킴.
> **`--project-path` 선택**: 여러 Unity 인스턴스 실행 중일 때 특정 프로젝트 타겟팅. 우리는 단일 프로젝트라 생략 가능.

### 2.4 검증

옵션 A 의 §1.4 동일 절차. Pending Connections 승인 필수.

---

## 3. 옵션 C — IvanMurzak/Unity-MCP (3순위)

### 3.1 사전 조건

- Unity 2022.3+ ✓
- `uv` 또는 Python (서버 실행)
- Claude Code

### 3.2 설치

```bash
# CLI 자동 셋업 (저장소 README 권장 방법)
npx @ivanmurzak/unity-mcp install
```

또는 수동: 저장소 README 따라 Unity package + Claude Code config 등록.

### 3.3 특이사항

- **`Any C# method may be turned into a tool by a single line`** — 우리 자체 시스템 (`MoraleSystem.Tick`, `GrowthSystem.Apply` 등) 을 MCP 도구로 노출 가능. **장기적으로 매력적**.
- CLI 자동화 강함. CoplayDev 가 실패하고 시간 여유 있다면 시도 가치 있음.

---

## 4. 옵션 D — Fallback (현 unity-ai/*.md 패턴 유지)

A/B/C 모두 실패 시:

- **Claude Code** 는 docs/unity-ai/<scene>.md 지시서를 작성.
- **사용자** 가 Unity Editor 의 AI Assistant 에 복붙.
- **Unity AI Assistant** 가 씬 / prefab 작업 수행.

> **이게 V0.5 에서 했던 방식**. 안정적이나 사용자 개입 필요.
> 단점: 다왕복 / 사용자 수동 작업 / Claude Code 가 결과 확인 불가능.

이 모드에서도 V1.0 작업은 가능. 단지 속도가 늦음. Stage 0 가 실패해도 V1.0 자체는 진행 가능 — Stage A 부터 종전 패턴.

---

## 5. 알려진 함정 — 통합 트러블슈팅

> **순서대로 확인**. 대부분 본 셋업의 첫 시도에서 만남.

### 5.1 `claude mcp add` 명령이 안 인식됨

```
Unknown command: mcp
```

**원인**: 오래된 Claude Code (MCP 지원 전 버전).
**해결**: `npm install -g @anthropic-ai/claude-code@latest` 후 새 터미널.

### 5.2 `uv: command not found` (PowerShell)

**원인**: uv 설치 후 PATH 갱신 안 됨.
**해결**:
1. 새 PowerShell 창 열기 (PATH 환경 변수 새로고침).
2. 그래도 안 되면 `$env:Path += ";$env:USERPROFILE\.local\bin"` 임시 추가.
3. 영구 해결: 시스템 환경 변수에 `%USERPROFILE%\.local\bin` 추가.

### 5.3 Python 인터프리터 못 찾음

```
error: failed to find Python 3.11
```

**원인**: `uv` 가 시스템 Python 을 못 찾음.
**해결**:
```powershell
# uv 가 Python 을 자체 설치하게
uv python install 3.12
uv python list
```

### 5.4 `/mcp` 했는데 unityMCP 가 안 보임

**원인 1**: Claude Code 재시작 안 함.
**해결**: 현재 세션 종료 → 새 터미널.

**원인 2**: scope 가 project 인데 다른 경로에서 실행 중.
**해결**: `claude mcp list` 로 등록 위치 확인. user scope 로 재등록.

**원인 3**: `claude_desktop_config.json` 손상.
**해결**: `%APPDATA%\Claude\claude_desktop_config.json` 백업 후 삭제 → Claude 가 자동 재생성. 또는 수동 편집.

### 5.5 `connected` 인데 도구 호출이 timeout

**원인**: Unity Editor 가 컴파일 중 / Domain Reload 중.
**해결**: Unity Console 의 좌하단 `Reloading...` 표시 사라질 때까지 대기.

### 5.6 `Connection refused` / `Connection reset`

**원인**: Unity Editor 종료된 상태에서 MCP 호출.
**해결**: Unity 열기.

**원인 (드물게)**: 포트 충돌.
**해결**: Unity 메뉴 `Window → MCP for Unity` → Port 변경 (default 8090).

### 5.7 도구 호출은 되는데 Unity 에 변화 없음

**원인**: Pending Connection 미승인.
**해결**: Unity `Window → MCP for Unity` → Pending 탭 → Approve.

### 5.8 Issue #773 — Windows 네이티브 stdio config 실패

**증상**: Auto-Setup 후 Claude 가 도구 호출 시 응답 없음. uvx --from 명령이 silent fail.
**해결**: §1.3.2 의 `uv --directory` 수동 방식으로 재등록.

### 5.9 Unity 6.3 + Unity 공식 MCP (옵션 B) DLL 충돌

**증상**: `System.Collections.Immutable` `TypeLoadException`.
**해결**: §2.2.1 절차.

### 5.10 도메인 리로드 시 MCP 연결 끊김

**원인**: Unity 가 .cs 변경 감지 → 도메인 리로드 → MCP 서버 재시작.
**해결**: 짧게 (~5초) 대기 후 재시도. 그래도 안 되면 `/mcp` 재확인.

### 5.11 다른 MCP 서버 (Linear, Slack 등) 와의 충돌

**원인**: 동일 transport 이름 충돌 등.
**해결**: `claude mcp list` 로 충돌 확인. 다른 이름으로 재등록.

### 5.12 Deferred MCP 도구 — ToolSearch schema 사전 로드 필수

**증상**: 새 세션에서 `mcp__UnityMCP__*` 도구 호출 시 `InputValidationError` 또는 도구를 인식 못 함.
**원인**: 본 환경 (Claude Code 안에서 Claude) 에서 MCP 도구는 **deferred** — 시스템 리마인더에 이름만 노출되고 JSONSchema 는 미로드 상태. 호출 시점에 schema 가 없어 검증 실패.
**해결**: 세션 시작 직후 `ToolSearch` 로 필요한 도구 schema 를 사전 로드.

```
ToolSearch query="select:ListMcpResourcesTool,ReadMcpResourceTool,mcp__UnityMCP__manage_gameobject,mcp__UnityMCP__manage_scene,mcp__UnityMCP__find_gameobjects,mcp__UnityMCP__set_active_instance,mcp__UnityMCP__read_console"
```

이후 해당 도구는 일반 도구처럼 호출 가능. 추가 도구 (manage_components / manage_asset / manage_script 등) 도 동일 패턴.

### 5.13 다중 Unity 인스턴스 시 active instance 지정 필수

**증상**: `Multiple instances connected — set_active_instance required` 에러.
**원인**: 한 PC 에서 두 개 이상의 Unity Editor 가 동시에 실행 중.
**해결**:
1. `ReadMcpResourceTool server="UnityMCP" uri="mcpforunity://instances"` 로 `Name@hash` 확인.
2. `mcp__UnityMCP__set_active_instance instance="<Name@hash>"` 로 세션 디폴트 지정.
3. 또는 개별 도구 호출에 `unity_instance="<hash 접두 6자+>"` 파라미터 전달.

단일 인스턴스 환경에서는 자동 라우팅 — 별도 호출 불필요. (본 프로젝트 2026-05-29 검증 시 단일 인스턴스 = `minigame-project-gamjaHoo@39286a1e0139c24c` 자동 라우팅 성공.)

### 5.14 `mcp__UnityMCP` prefix-only 호출 불가

**증상**: `Error: No such tool available: mcp__UnityMCP`.
**원인**: 서버명 (prefix) 만으로 호출 — MCP 도구는 항상 `<server>__<tool>` 전체 이름.
**해결**: 정확한 도구명 사용 — `mcp__UnityMCP__manage_scene`, `mcp__UnityMCP__manage_gameobject`, `mcp__UnityMCP__read_console` 등. 도구 카탈로그는 `ListMcpResourcesTool` (리소스 19종) 또는 `mcpforunity://tool-groups` 리소스로 확인 가능.

---

## 6. 셋업 성공 후 — 작업 흐름 변화

### 6.1 변경 사항 (Stage 0 이후 V1.0 작업 패턴)

| 영역 | Before (V0.5 patten) | After (Stage 0 완료) |
|---|---|---|
| 씬 prefab 생성 | docs/unity-ai/<scene>.md 작성 → 사용자가 Unity AI 에 복붙 | Claude Code 가 MCP 도구로 직접 GameObject 생성 / 컴포넌트 추가 / 와이어링 |
| Hierarchy 조회 | 사용자 스크린샷 / 캡처 | Claude Code 가 `mcp__unityMCP__read_hierarchy` 직접 호출 |
| 컴포넌트 와이어링 | 사용자가 인스펙터에서 드래그 | Claude Code 가 `mcp__unityMCP__set_field` 등으로 직접 |
| Console 에러 확인 | 사용자가 복사 후 전달 | Claude Code 가 `mcp__unityMCP__read_console` 직접 |
| Play Mode 검증 | 사용자가 Play 후 보고 | (대부분) Edit Mode 작업. Play Mode 진입은 사용자 수동 유지 |

### 6.2 유지되는 패턴

- **`.cs` 파일 작성/수정** — Claude Code 의 Edit/Write 도구 (변함 없음).
- **MUIP 원본 prefab 수정 금지** — 본 셋업 후에도 동일 (커스텀 prefab 만 편집).
- **EditMode 테스트 실행** — 사용자가 Test Runner 에서 직접 (MCP 가 자동화 가능하지만 우선순위 낮음).
- **Play Mode 종합 검증** — 사용자가 직접 (Claude Code 가 자동 못 함).
- **씬 디자인 판단** — 사용자 검토 / 피드백 루프 유지. Claude Code 는 실행만.

### 6.3 docs/unity-ai/*.md 의 운명

Stage 0 성공 시:
- **신규 지시서 작성 빈도 ↓** — Claude Code 가 직접 작업하므로 사용자 복붙 단계 생략.
- **그러나 폐기 X** — 복잡한 씬 디자인 (도식 / 드래그 시스템 / 애니메이션) 은 여전히 Unity AI Assistant 가 유리할 수 있음. 두 도구 병행.
- **회고 / 문서화 용도** — 어떤 씬을 어떻게 만들었는지 기록 (audit 용).

Stage 0 실패 시:
- 기존 패턴 100% 유지 (옵션 D).

---

## 7. 롤백 — 셋업이 망가뜨렸을 때

본 셋업이 기존 환경 (V0.5 작업 가능 상태) 을 망가뜨릴 경우:

### 7.1 Unity 측 롤백

1. Window → Package Manager → MCP for Unity (또는 Unity AI Assistant) → **Remove**.
2. Unity Console 에러 0 확인.
3. 기존 V0.5 작업 (씬 / prefab) 정상 열림 확인.

### 7.2 Claude Code 측 롤백

```powershell
claude mcp remove unityMCP    # 또는 등록한 이름
claude mcp list               # 0개여야 함
```

### 7.3 Git 측

본 셋업은 **`.cs` / 씬 / prefab 을 건드리지 않아야 함**. 만약 건드렸다면:

```powershell
git status                    # 의도하지 않은 변경 확인
git diff                      # 변경 내용 확인
git checkout -- <파일>         # 변경 되돌리기
```

### 7.4 SkippedFile

`Assets/Plugins/System.Collections.Immutable.dll` (옵션 B 의 fix) 추가 시:
- 옵션 B 포기하고 옵션 A 로 전환했다면 → 이 DLL 도 제거 (`.dll` 파일 + `.meta` 삭제).

---

## 8. 검증 체크리스트 (Stage 0 완료 조건)

이 모든 항목 ✓ 시 Stage 0 완료, V1.0 Stage A 진입 가능.

### 8.1 환경
- [x] `python --version` 3.11+
- [x] `uv --version` 정상
- [x] `claude --version` 정상
- [x] Unity Editor 6000.3.15f 정상 실행

### 8.2 옵션 A (CoplayDev) 셋업
- [x] Package Manager 에 `MCP for Unity` 설치
- [x] Console 컴파일 에러 0 (read_console 결과 error/warning 0건)
- [x] `Window → MCP for Unity` 윈도우 열림 / Status `Running`
- [x] HTTP transport 연결 — `mcpforunity://instances` = `minigame-project-gamjaHoo@39286a1e0139c24c` (Unity 6000.3.15f1)

### 8.3 Smoke Test
- [x] ToolSearch + ListMcpResourcesTool → `mcp__UnityMCP__*` 시리즈 + 리소스 19종 노출 확인
- [x] "MCPTest GameObject 생성" → TacticScene Hierarchy 에 즉시 추가됨 (instanceID -4818)
- [x] "현재 씬 Hierarchy 보여줘" → 루트 4개 (Main Camera / Canvas / TacticRoot / MCPTest) 정확
- [x] Pending Connections — 본 환경에서는 자동 승인 (별도 조작 불필요)

### 8.4 실프로젝트 호환
- [x] TacticScene 활성 (현재 작업 씬) 에서 Canvas 자식 7개 트리 + RectTransform position 정확 — DashboardScene 갈음
- [ ] 시험 Edit (TitleText.text 변경 등) — 본 세션 검증 흐름에 미포함 (별도 시도 시 갱신)
- [x] `git status` — 세션 시작 시점과 100% 동일 (MCP 도구가 디스크 변경 0건)
- [x] 시험 GameObject (MCPTest) 제거 → Unity 깨끗

### 8.5 롤백 가능성 확인
- [ ] 본 문서 §7 절차 머릿속에 있음
- [ ] 만약 망가지면 Package Manager Remove + `claude mcp remove` 로 원상복귀 가능

---

## 9. Stage 0 → Stage A 전환

Stage 0 완료 (또는 옵션 D 로 fallback 결정) 시:
1. **본 문서를 V1.0 Open Questions Resolution log 에 기록** (`v1.0-plan.md` Change Log).
2. **사용 옵션 (A/B/C/D 중 어느 것)** 명시.
3. 위 Resolution 을 GitHub Issue (chore type) 로 생성 — `feature/0-unity-mcp-setup` 브랜치 / `area:infra` 라벨 / V1.0 milestone.
4. Stage A (v1.0-tasks.md Stage A — Inbox + Player.physical 등) 진입.

---

## 10. 참고 자료

| 자료 | 용도 |
|---|---|
| https://github.com/CoplayDev/unity-mcp | 옵션 A 본체 |
| https://github.com/CoplayDev/unity-mcp/wiki/2.-Fix-Unity-MCP-and-Claude-Code | Claude Code 통합 fix |
| https://github.com/CoplayDev/unity-mcp/wiki/3.-Common-Setup-Problems | 트러블슈팅 |
| https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.7/manual/integration/unity-mcp-get-started.html | 옵션 B 공식 |
| https://github.com/IvanMurzak/Unity-MCP | 옵션 C |
| https://github.com/CoplayDev/unity-mcp/issues/773 | Windows native stdio 함정 |
| https://docs.astral.sh/uv/ | `uv` 공식 |
| https://docs.anthropic.com/en/docs/claude-code/mcp | Claude Code MCP 공식 |

---

## Change Log

| Date | Change |
|---|---|
| 2026-05-29 | V1.0 Stage 0 명세 초안 작성. 옵션 4단계 (CoplayDev / Unity 공식 / IvanMurzak / Fallback) + Windows 네이티브 함정 11종 + DLL 충돌 fix + 롤백 절차 + Stage A 전환 조건 명시. 사용자가 과거 시도 실패한 영역이라 *안 될 경우 대비* 4단계 fallback 트리 구축. |
| 2026-05-29 | Stage 0 검증 완료 — **옵션 A (CoplayDev) 채택**. UnityMCP 인스턴스 `minigame-project-gamjaHoo@39286a1e0139c24c` (Unity 6000.3.15f1) HTTP transport 정상 연결. Smoke Test (MCPTest 생성 → TacticScene Hierarchy 조회 → Canvas 7 children 정확 응답 → 삭제) 통과. git status 변화 없음 = MCP 도구가 디스크 안 건드림 검증. 본 세션 신규 함정 3건 추가: §5.12 deferred MCP 도구 ToolSearch 사전 로드 / §5.13 다중 인스턴스 active 지정 / §5.14 prefix-only 호출 불가. §8 체크리스트 통과 (8.4 시험 Edit 만 미실시). Stage A 진입 가능. |
