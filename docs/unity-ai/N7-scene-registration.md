# N.7 신규 씬 등록 — Unity AI 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.  
> **`.cs` 파일 절대 손대지 말 것.**

---

## 목적

V1.0 에서 새로 추가된 씬을 **EditorBuildSettings** 에 등록해 빌드에 포함시킨다.

---

## 등록할 씬 목록

아래 7개 씬을 `File → Build Settings → Scenes In Build` 패널에 추가한다.

| 씬 이름 | 경로 |
|---------|------|
| LineupScene | `Assets/Scenes/LineupScene.unity` |
| TacticScene | `Assets/Scenes/TacticScene.unity` |
| MatchTextScene | `Assets/Scenes/MatchTextScene.unity` |
| NegotiationScene | `Assets/Scenes/NegotiationScene.unity` |
| MentoringScene | `Assets/Scenes/MentoringScene.unity` |
| SeasonSummaryScene | `Assets/Scenes/SeasonSummaryScene.unity` |
| PromiseInboxScene | `Assets/Scenes/PromiseInboxScene.unity` |

---

## 등록 방법

**방법 A (권장):**

각 씬에 대해:
1. Project 창에서 해당 `.unity` 파일 선택
2. `File → Build Settings` 창이 열려 있으면 `Scenes In Build` 패널로 드래그
3. 또는 해당 씬을 열고 Build Settings 에서 **Add Open Scenes** 클릭

**방법 B (한 번에):**

1. `File → Build Settings` 창 오픈
2. Project 창에서 위 7개 씬 파일을 전부 선택 (Ctrl 클릭으로 다중 선택)
3. `Scenes In Build` 패널로 한 번에 드래그

---

## 완료 후

- `Ctrl+S` 로 저장 (`ProjectSettings/EditorBuildSettings.asset` 갱신)
- Build Settings 창에서 7개 씬이 목록에 표시되면 완료

---

## 주의

- 씬 파일이 아직 생성되지 않은 경우(MatchTextScene, NegotiationScene, PromiseInboxScene 등) → 먼저 해당 씬 작업 지시서(N.3, N.4, N.5)를 완료한 후 이 작업 수행.
- 이미 등록된 씬은 중복 추가 불필요.
- 씬 순서(Build Index)는 중요하지 않음 — 코드에서 씬 이름 문자열로 로드.
