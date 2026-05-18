# Project Context

## Vision

**"FM의 육성 부분만 떼어내서 시간 압축한 게임"**

기존 FM은 깊이 있지만 한 시즌에 수십 시간 걸려 직장인이 즐기기 부담스럽다. FM-Lite는 한 시즌을 2~3시간에 완주할 수 있는 컴팩트 매니저 게임. 경기 시각화는 포기하고 육성/이적/유스 인스펙션에 집중한다.

## Selling Points

- 한 시즌 2~3시간 완주
- 3중 가챠 시스템: 초기 스쿼드 / 유스 인스펙션 / 트레잇
- 정보 비대칭 기반 의사결정 (제한된 정보 속에서 선택)
- 리롤 시스템화 (세이브 로드 편법을 정식 메커닉으로)
- 시드 기반 리플레이 가치

## Target User

- FM 시리즈를 좋아하지만 시간 부족한 직장인
- 유망주 키우기 콘텐츠 (동수칸/종식이 등) 시청자
- 로그라이크 의사결정 게임 선호 유저

## Tech Stack

| Area | Choice |
| --- | --- |
| Engine | Unity (C#) |
| UI Asset | Modern UI Pack |
| Serialization | Newtonsoft.Json |
| Static Data | ScriptableObject |
| Animation | DOTween (V1.x onwards) |
| Editor Tools | Built-in + Custom Editor (when needed) |
| Dependencies | Minimized (no Odin) |

### Imported Assets (외부 유료 에셋)

라이선스 보호를 위해 public repo에는 커밋 금지. 별도 private git repo로 관리되는 `Assets/Imported/` 아래에 임포트한다 (gitignored). Asset Store에서 임포트할 때 대상 폴더를 `Assets/Imported/<PackageName>/` 으로 지정.

**Modern UI Pack 사용법:**
- UI Manager: `Tools → Modern UI Pack → Show UI Manager`

## Architecture

**Layered Architecture**

```
Presentation Layer    — UI, Unity Scenes
        ↕
Application Layer     — GameManager, Systems
        ↕
Domain Layer          — Game rules, data classes
        ↕
Data Layer            — Save/Load, ScriptableObject
```

### Dependency Strategy

- ScriptableObject-based + GameManager singleton
- Custom EventBus (static, ~50 lines)
- UI callbacks via UnityEvent

### Folder Structure

```
Assets/
├─ _Project/
│  ├─ Scripts/
│  │  ├─ Core/           (GameManager, GameTime, EventBus)
│  │  ├─ Domain/         (Player, Club, League, Match, etc.)
│  │  ├─ Application/    (Systems)
│  │  ├─ Persistence/    (Save/Load)
│  │  ├─ UI/             (UI Controllers)
│  │  ├─ Utils/
│  │  └─ Editor/         (Custom Inspectors)
│  ├─ Data/              (ScriptableObject instances)
│  ├─ Prefabs/
│  ├─ Scenes/
│  ├─ Art/
│  ├─ Audio/
│  └─ Resources/
├─ Plugins/              (Newtonsoft.Json, DOTween)
└─ ThirdParty/           (UI assets)
```

### Namespace Strategy

Flat: `FMLite.Core`, `FMLite.Domain`, `FMLite.Application`, `FMLite.UI`

## Development Timeline

### V0.1 — Prototype (5/15 ~ 5/22)

**Goal:** Play one full season end-to-end (UI may be ugly, functionality first).

- Folder structure, debug mode, logging, Korean font
- Core data classes, SO base, save/load, dummy data
- Time progression, event queue, season cycle
- Match results (score only)
- Starting squad gacha + reroll
- Youth inspection + reroll tokens
- Simple transfer (offer → negotiate → conclude)
- Functional UI: main / squad / youth / transfer / schedule

### V1.0 — Playability (5/23 ~ 5/29)

**Goal:** A season runs entertainingly.

- Match text events (FC Online auto style)
- Detailed stats (possession, shots, pass rate)
- Injuries, cards
- Tactic presets (5-6)
- Additional active leagues (target 3)
- Youth system balancing
- Morale/discontent system
- UI polish
- V0.1 refactoring

### V1.x — Polish (5/30 ~ 6/5)

**Goal:** Portfolio-ready build.

- Player/club images (initials or simple parts)
- DOTween UI animations
- Convenience features (shortcuts, autosave, season summary)
- Balancing finalization
- README, screenshots, video, architecture diagram

## Out of Scope (for V0.1)

- Champions League / Europa League
- International (national team)
- Press conferences / media
- Coach personnel management
- Locker room groups / cliques
- Carabao Cup (optional)
