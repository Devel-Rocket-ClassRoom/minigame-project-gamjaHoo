# LineupScene — Unity AI 작업 지시서

> 이전 [`tactic-scene-slot-rows.md`](tactic-scene-slot-rows.md) 의 후속. **`.cs` 절대 금지**, **MUIP 원본 prefab 수정 금지**.

## 컨텍스트 (Unity AI 가 알아야 할 것)

- TacticScene (Formation/Mentality/11-슬롯 Role/Duty) 은 완성·검증 끝.
- 이번 작업: **LineupScene 신규 생성** — 11명 선수 수동 배정 + 자동 배정 버튼 + Set Pieces 4종.
- 코드는 이미 작성됨:
  - `Assets/_Project/Scripts/UI/LineupController.cs` — LineupScene 메인 컨트롤러.
    SerializeField: `TitleText` / `SlotRowContainer` (Transform) / `SlotRowPrefab` (LineupSlotRowController) / `AutoLineupButton` (ButtonManager) / `PenaltyDropdown` / `FreeKickDropdown` / `CornerDropdown` / `ThrowInDropdown` (모두 CustomDropdown) / `SaveButton` / `BackButton` (모두 ButtonManager).
  - `Assets/_Project/Scripts/UI/LineupSlotRowController.cs` — 슬롯 row 컨트롤러. SerializeField: `PositionLabel` (TMP_Text) / `RoleLabel` (TMP_Text) / `PlayerDropdown` (CustomDropdown).
- 작업:
  1. **새 prefab `LineupSlotRow.prefab`** 생성.
  2. **새 씬 `LineupScene.unity`** 생성.
  3. **DashboardScene 에 Lineup 버튼** 추가.

## 절대 규칙

1. **`.cs` 파일 절대 금지.** 컴파일 에러가 나도 .cs 손대지 말고 멈춰서 보고.
2. **MUIP 원본 prefab 수정 금지** — `Assets/Imported/Modern UI Pack/Prefabs/...` 안 파일은 손대지 말 것.
3. **신규 prefab 은 반드시 `Assets/Imported/FMLite UI/Prefabs/` 밑에 저장** (유료 에셋 저작권 관리).
4. 작업 후 `Ctrl+S` 로 저장.

---

## Step 1 — LineupSlotRow prefab 생성

### 1.1 폴더 확인

`Assets/Imported/FMLite UI/Prefabs/` 폴더가 이미 있는지 확인. 없으면 생성 (TacticSlotRow.prefab 이 이미 여기 있으면 있는 것).

### 1.2 임시 GameObject 생성

1. Hierarchy 우클릭 → `Create Empty` → 이름 `LineupSlotRow`.
2. RectTransform: Width = `800`, Height = `70`.

### 1.3 레이아웃 + 컨트롤러 추가

`LineupSlotRow` 선택 → Inspector:

1. **`Add Component` → `Horizontal Layout Group`**:
   - Padding: Left `30`, Top `0`, Right `30`, Bottom `0`
   - Spacing: `20`
   - Child Alignment: `Middle Left`
   - Child Force Expand: Width / Height **둘 다 체크 해제**
   - Child Controls Size: Width / Height **둘 다 체크 해제**
2. **`Add Component` → `Lineup Slot Row Controller`** (스크립트 검색 후 추가).

### 1.4 PositionLabel 추가 (첫 자식)

1. `LineupSlotRow` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `PositionLabel`.
2. RectTransform: Width = `80`, Height = `50`.
3. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `??`
   - Font Size: `32`
   - Alignment: Center / Middle
   - Color: White

### 1.5 RoleLabel 추가 (둘째 자식)

1. `LineupSlotRow` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `RoleLabel`.
2. RectTransform: Width = `200`, Height = `50`.
3. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `--`
   - Font Size: `28`
   - Alignment: Center / Middle
   - Color: Light Gray (`#CCCCCC`)

### 1.6 PlayerDropdown 추가 (셋째 자식)

1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab` 을 `LineupSlotRow` 위로 드래그.
2. 인스턴스 선택:
   - 이름 변경: `PlayerDropdown`
   - `Custom Dropdown` 컴포넌트:
     - **`Init At Start`** 체크 해제 (false)
     - **`Enable Icon`** 체크 해제 (false)

### 1.7 LineupSlotRowController 와이어링 ⭐

`LineupSlotRow` 루트 선택 → Inspector 의 `Lineup Slot Row Controller` 컴포넌트 3 슬롯 채우기:

| 컨트롤러 필드 | 드래그할 자식 |
|---|---|
| `Position Label` | `PositionLabel` |
| `Role Label` | `RoleLabel` |
| `Player Dropdown` | `PlayerDropdown` |

### 1.8 Prefab 으로 저장

1. `Assets/Imported/FMLite UI/Prefabs/` 폴더로 Hierarchy 의 `LineupSlotRow` 드래그.
2. Prefab 생성 다이얼로그 → `Original Prefab`.
3. Hierarchy 의 `LineupSlotRow` 는 **삭제** (런타임에 컨트롤러가 인스턴스화).

---

## Step 2 — LineupScene 생성

### 2.1 새 씬 만들기

1. `File → New Scene` → Empty (Built-in) 또는 Basic (Built-in).
2. `File → Save As` → `Assets/Scenes/LineupScene.unity`.

### 2.2 기본 오브젝트 확인

- Hierarchy 에 `Main Camera` 존재 확인 (없으면 추가).
- Hierarchy 에 `EventSystem` 존재 확인 (없으면 추가).

### 2.3 Canvas 생성

1. Hierarchy 우클릭 → `UI → Canvas`. 이름 `Canvas`.
2. Inspector:
   - Canvas: `Render Mode` = `Screen Space - Overlay`
   - Canvas Scaler: `Scale With Screen Size`, Reference Resolution `1920 × 1080`, Match `0.5`

### 2.4 LineupRoot (컨트롤러 호스트)

1. Hierarchy 우클릭 → `Create Empty` → 이름 `LineupRoot`.
2. `Add Component` → 검색창에 `LineupController` → 추가.
   - Inspector 에 `Lineup Controller` 컴포넌트가 보이며 슬롯들이 `None`. **Step 2.10 에서 채울 것.**

### 2.5 (선택) Background

- Canvas 자식 `UI → Image` → 이름 `Background`.
- Anchor: stretch-stretch (Alt 클릭으로 offset 자동 0).
- Color: `#1A1A1A`, Alpha 255.

## 레이아웃 구조 (1920×1080 기준)

```
┌─────────────────────────────────────────────────────┐
│                   [라인업] TitleText                  │
│  [자동 배정 btn]              [세트피스 SectionTitle] │
│                                                       │
│  ┌──────────────────┐   ┌──────────────────────────┐ │
│  │                  │   │ 페널티   [Dropdown]       │ │
│  │  SlotsScrollView │   │ 프리킥   [Dropdown]       │ │
│  │  (11 slot rows)  │   │ 코너킥   [Dropdown]       │ │
│  │                  │   │ 스로인   [Dropdown]       │ │
│  └──────────────────┘   └──────────────────────────┘ │
│                   [저장]  [뒤로]                      │
└─────────────────────────────────────────────────────┘
```

- **왼쪽 컬럼** (PosX = **-420**): 자동 배정 버튼 + SlotsScrollView
- **오른쪽 컬럼** (PosX = **+380**): 세트피스 섹션 타이틀 + SetPiecesPanel

---

### 2.6 TitleText

1. Canvas 자식 → `UI → Text - TextMeshPro` → 이름 `TitleText`.
2. RectTransform: Anchor = top-center, **Pos X = `0`**, Pos Y = `-65`, Width = `700`, Height = `60`.
3. Text = `Lineup`, Font Size = `48`, Alignment = Center/Middle.

### 2.7 AutoLineupButton (왼쪽 컬럼 상단)

1. Canvas 자식 → `Create Empty` → 이름 `AutoLineupRow`.
2. RectTransform: Anchor = top-center, **Pos X = `-420`**, Pos Y = `-160`, Width = `380`, Height = `50`.
3. `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` 를 `AutoLineupRow` 위로 드래그.
4. 인스턴스 이름: `AutoLineupButton`.

### 2.8 SlotsScrollView (왼쪽 컬럼 메인)

1. Canvas 자식 우클릭 → `UI → Scroll View` → 이름 `SlotsScrollView`.
2. RectTransform: Anchor = top-center, **Pos X = `-420`**, Pos Y = `-570`, Width = `840`, Height = `750`.
3. Scroll Rect 컴포넌트:
   - `Horizontal` **체크 해제** (수직만)
4. `SlotsScrollView/Scrollbar Horizontal` 우클릭 → `Delete`.
5. `SlotsScrollView/Viewport/Content` 선택:
   - `Add Component` → `Vertical Layout Group`:
     - Padding Top/Bottom `10`, Left/Right `0`, Spacing `5`
     - Child Alignment: `Upper Center`
     - Child Force Expand: Width/Height **체크 해제**
     - Child Controls Size: Width/Height **체크 해제**
   - `Add Component` → `Content Size Fitter`:
     - Horizontal Fit: `Unconstrained`
     - Vertical Fit: `Preferred Size`

### 2.9 Set Pieces 섹션 (오른쪽 컬럼)

오른쪽 컬럼은 섹션 타이틀 TMP_Text 하나 + SetPiecesPanel 로 구성된다.

#### 2.9a SetPiecesSectionTitle (정적 라벨)

1. Canvas 자식 → `UI → Text - TextMeshPro` → 이름 `SetPiecesSectionTitle`.
2. RectTransform: Anchor = top-center, **Pos X = `+380`**, Pos Y = `-160`, Width = `440`, Height = `40`.
3. Text = `세트피스`, Font Size = `34`, Alignment = Center/Middle, Color = White.

#### 2.9b SetPiecesPanel — [라벨 | 드롭다운] 4행

1. Canvas 자식 → `Create Empty` → 이름 `SetPiecesPanel`.
2. RectTransform: Anchor = top-center, **Pos X = `+380`**, Pos Y = `-400`, Width = `460`, Height = `360`.
3. `Add Component` → `Vertical Layout Group`:
   - Spacing `12`, Child Alignment: `Upper Center`
   - Child Force Expand: Width **체크**, Height **체크 해제**
   - Child Controls Size: Width **체크**, Height **체크 해제**

4. 아래 4개 행을 **순서대로** `SetPiecesPanel` 자식으로 만든다:

   **각 행 생성 순서 (4번 반복):**

   a. `SetPiecesPanel` 자식 우클릭 → `Create Empty` → 이름 (아래 표 참고).
   b. 행 RectTransform: Width = `440`, Height = `70`.
   c. 행에 `Add Component` → `Horizontal Layout Group`:
      - Spacing `12`, Child Alignment: `Middle Left`
      - Child Force Expand: Width **체크**, Height **체크 해제**
      - Child Controls Size: Width **체크 해제**, Height **체크 해제**
   d. 행 자식 → `UI → Text - TextMeshPro` → 이름 (아래 표 참고):
      - Width = `120`, Height = `50`
      - Font Size: `26`, Alignment: Middle Left, Color: White
      - Text: (아래 표의 자리표시자 — 런타임에 덮어씀)
   e. 행 자식 → `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab` 드래그:
      - 이름: (아래 표 참고)
      - `Custom Dropdown` 컴포넌트: **`Init At Start` = false**, **`Enable Icon` = false**

   | 행 이름 | TMP_Text 이름 | Text 자리표시자 | Dropdown 이름 |
   |---|---|---|---|
   | `PenaltyRow` | `PenaltyLabel` | `페널티` | `PenaltyDropdown` |
   | `FreeKickRow` | `FreeKickLabel` | `프리킥` | `FreeKickDropdown` |
   | `CornerRow` | `CornerLabel` | `코너킥` | `CornerDropdown` |
   | `ThrowInRow` | `ThrowInLabel` | `스로인` | `ThrowInDropdown` |

### 2.10 Footer (Save / Back 버튼)

1. Canvas 자식 → `Create Empty` → 이름 `Footer`.
2. RectTransform: Anchor = bottom-center, Pos Y = `80`, Width = `500`, Height = `60`.
3. `Add Component` → `Horizontal Layout Group`:
   - Child Alignment: `Middle Center`, Spacing `20`
   - Child Force Expand: Width/Height **체크 해제**
4. `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` 를 `Footer` 위로 **두 번** 드래그:
   - 첫 번째: `SaveButton`
   - 두 번째: `BackButton`

### 2.11 ⭐ LineupController 와이어링 (가장 중요)

Hierarchy 에서 `LineupRoot` 선택 → Inspector 의 `Lineup Controller` 컴포넌트의 모든 필드를 채운다:

| 컨트롤러 필드 | 드래그할 대상 |
|---|---|
| `Title Text` | `Canvas/TitleText` |
| `Slot Row Container` | `Canvas/SlotsScrollView/Viewport/Content` |
| `Slot Row Prefab` | Project 창의 `Assets/Imported/FMLite UI/Prefabs/LineupSlotRow.prefab` |
| `Auto Lineup Button` | `Canvas/AutoLineupRow/AutoLineupButton` |
| `Penalty Label` | `Canvas/SetPiecesPanel/PenaltyRow/PenaltyLabel` |
| `Free Kick Label` | `Canvas/SetPiecesPanel/FreeKickRow/FreeKickLabel` |
| `Corner Label` | `Canvas/SetPiecesPanel/CornerRow/CornerLabel` |
| `Throw In Label` | `Canvas/SetPiecesPanel/ThrowInRow/ThrowInLabel` |
| `Penalty Dropdown` | `Canvas/SetPiecesPanel/PenaltyRow/PenaltyDropdown` |
| `Free Kick Dropdown` | `Canvas/SetPiecesPanel/FreeKickRow/FreeKickDropdown` |
| `Corner Dropdown` | `Canvas/SetPiecesPanel/CornerRow/CornerDropdown` |
| `Throw In Dropdown` | `Canvas/SetPiecesPanel/ThrowInRow/ThrowInDropdown` |
| `Save Button` | `Canvas/Footer/SaveButton` |
| `Back Button` | `Canvas/Footer/BackButton` |

⚠️ 각 GameObject 의 **루트** 를 드래그. Unity 가 컴포넌트를 자동 바인딩.

### 2.12 Build Settings 등록

1. `File → Build Settings` → 현재 LineupScene 열린 상태에서 `Add Open Scenes`.
2. 목록에 `Scenes/LineupScene` 추가 확인.
3. 창 닫기.

### 2.13 씬 저장

`Ctrl+S`.

---

## Step 3 — DashboardScene 에 Lineup 버튼 추가

1. `Assets/Scenes/DashboardScene.unity` 열기.
2. 기존 `TacticButton` 이 있는 곳 (Inspector 로 확인) 근처에 버튼 추가:
   - `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` 를 버튼들이 있는 부모 컨테이너로 드래그.
   - 이름: `LineupButton`.
3. `LineupButton` 의 `ButtonManager` 컴포넌트:
   - `Button Text` = `Lineup` (런타임에 컨트롤러가 덮어쓸 예정이 아니므로 직접 입력. 향후 로컬라이즈 추가 가능).
4. `LineupButton` 의 `Button` 컴포넌트 (또는 `ButtonManager.clickEvent`) — DashboardRoot 의 `Dashboard Controller` 컴포넌트의 `On Lineup Clicked ()` 이벤트로 연결:
   - Inspector 의 `Button Manager` 컴포넌트 → `Click Event` 항목 → `+` 버튼 → DashboardRoot 오브젝트 드래그 → 함수: `DashboardController.OnLineupClicked`.
5. `Ctrl+S` 로 DashboardScene 저장.

---

## 검증 체크리스트

### prefab

- [ ] `Assets/Imported/FMLite UI/Prefabs/LineupSlotRow.prefab` 파일 존재.
- [ ] Prefab 내부: 자식 3개 (`PositionLabel`, `RoleLabel`, `PlayerDropdown`) + `Lineup Slot Row Controller` + `Horizontal Layout Group`.
- [ ] `Lineup Slot Row Controller` 3 슬롯 모두 채워짐 (`None` 없음).
- [ ] `PlayerDropdown` CustomDropdown: `Init At Start = false`, `Enable Icon = false`.

### LineupScene

- [ ] `Assets/Scenes/LineupScene.unity` 파일 존재.
- [ ] `Build Settings → Scenes In Build` 목록에 `Scenes/LineupScene` 포함.
- [ ] 왼쪽 컬럼 (PosX=-420): `AutoLineupRow` + `SlotsScrollView` 배치.
- [ ] 오른쪽 컬럼 (PosX=+380): `SetPiecesSectionTitle` + `SetPiecesPanel` 배치.
- [ ] `LineupRoot` 의 `Lineup Controller` 모든 필드 채워짐 (`None` 없음, 라벨 4개 + 드롭다운 4개 포함).
- [ ] Set Pieces 4개 드롭다운: `Init At Start = false`, `Enable Icon = false`.
- [ ] `SetPiecesPanel` 자식 4개가 각각 `[라벨 TMP_Text | 드롭다운]` 행 구조 (HorizontalLayoutGroup).
- [ ] Footer 의 두 버튼 모두 `ButtonManager` 컴포넌트 존재 (`ButtonManagerBasic` 이면 잘못된 prefab).

### DashboardScene

- [ ] `LineupButton` 이 Dashboard 씬에 존재.
- [ ] `LineupButton.clickEvent` → `DashboardController.OnLineupClicked` 연결됨.

### Console

- [ ] 컴파일 에러 0.

---

## (참고) Play 모드 동작 검증 — Unity AI 작업 끝난 후 사용자 수동 테스트

1. MainMenu → 새 게임 → Dashboard.
2. Dashboard 의 `LineupButton` 클릭 → LineupScene 진입.
3. LineupScene 진입 시:
   - TitleText = "라인업".
   - SlotsScrollView 에 11개 row 표시 (포메이션에 따른 포지션 라벨 + Role 라벨 + 선수 드롭다운).
   - 선수 드롭다운: 해당 포지션 호환 선수 목록 (부상/정지 표기 포함).
   - Auto Assign 버튼 텍스트 = "자동 배정".
   - Set Pieces 4개 드롭다운 각각 선수 목록 + 첫 항목 "자동 (stat 최상위)".
4. Auto Assign 버튼 클릭 → 11 슬롯 자동 채워짐.
5. Save 클릭 → Dashboard 복귀 + `club.tactic.slots[i].assignedPlayerId` 반영됨.
6. Back 클릭 → Dashboard 복귀 + 변경 미반영.
7. Dashboard → Tactic → TacticScene 에서 Formation 변경 후 Save → Dashboard → Lineup 진입 → 새 포메이션 포지션 라벨로 row 갱신 확인.

---

## 문제 발생 시 처리

1. **즉시 멈춤.** `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 에러 메시지 + 어느 Step 에서 막혔는지 + 가능하면 스크린샷 보고.
3. 보고는 사용자가 Claude Code 에 전달해 다음 단계 결정.
