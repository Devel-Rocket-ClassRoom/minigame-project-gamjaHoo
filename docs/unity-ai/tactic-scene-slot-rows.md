# TacticScene 11-슬롯 Row 추가 — Unity AI 작업 지시서

> 이전 [`tactic-scene-slice.md`](tactic-scene-slice.md) 의 후속. **`.cs` 절대 금지**, **MUIP 원본 prefab 수정 금지**.

## 컨텍스트 (Unity AI 가 알아야 할 것)
- TacticScene 슬라이스(Formation/Mentality/Save/Back) 는 이미 만들었고 검증 끝.
- 이번 작업: 11 슬롯 Role/Duty 편집 row 추가.
- 코드는 이미 작성됨:
  - `TacticController.cs` 에 `Slot Row Container` (Transform) + `Slot Row Prefab` (TacticSlotRowController) SerializeField 추가됨. 현재 둘 다 `None` 상태 — 이번에 채움.
  - `TacticSlotRowController.cs` 신규 — 한 행을 컨트롤. SerializeField: `PositionLabel`, `RoleDropdown`, `DutySelector`.
- 작업: **새 prefab `TacticSlotRow.prefab` 생성 → 씬에 SlotsScrollView 추가 → TacticController 와이어링.**

## 절대 규칙
1. **`.cs` 파일 절대 금지.** 컴파일 에러가 나도 .cs 손대지 말고 멈춰서 보고.
2. **MUIP 원본 prefab 수정 금지** — `Assets/Imported/Modern UI Pack/Prefabs/...` 의 prefab 자체는 손대지 말고 씬/새 prefab 안에 **인스턴스만** 배치.
3. **신규 prefab 은 반드시 `Assets/Imported/FMLite UI/Prefabs/` 밑에 저장** (유료 에셋 저작권 관리 — 메인 repo 가 아닌 private Imported repo. `Assets/_Project/` 밑에 두면 안 됨).
4. 작업 후 `Ctrl+S` 로 저장.

---

## Step 1 — TacticSlotRow prefab 생성

### 1.1 폴더 준비
Project 창에서 `Assets/Imported/` 안에 다음 폴더가 없으면 만든다:
- `Assets/Imported/FMLite UI/` (없으면 우클릭 → Create → Folder)
- `Assets/Imported/FMLite UI/Prefabs/`

### 1.2 임시 GameObject 생성 (prefab 만들 베이스)
1. TacticScene 의 Hierarchy 에서 Canvas 자식 우클릭 → `Create Empty` → 이름 `TacticSlotRow`.
2. RectTransform: Width = `800`, Height = `70`. (Anchor 는 일단 무관 — prefab 화 후 인스턴스화 시 부모가 결정.)

### 1.3 행 자체에 Layout + Controller 추가
TacticSlotRow 선택 → Inspector:

1. **`Add Component` → `Horizontal Layout Group`**:
   - Padding: Left `30`, Top `0`, Right `30`, Bottom `0`
   - Spacing: `20`
   - Child Alignment: `Middle Left`
   - Child Force Expand: Width / Height **둘 다 체크 해제**
   - Child Controls Size: Width / Height **둘 다 체크 해제**
2. **`Add Component` → `Tactic Slot Row Controller`** (스크립트 검색해 추가).

### 1.4 PositionLabel 추가 (첫 자식)
1. TacticSlotRow 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `PositionLabel`.
2. RectTransform: Width = `80`, Height = `50`.
3. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `??` (placeholder — 런타임에 컨트롤러가 "GK"/"CB" 등으로 덮어씀)
   - Font Size: `32`
   - Alignment: Center / Middle
   - Color: White

### 1.5 RoleDropdown 추가 (둘째 자식)
1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab` 을 Hierarchy 의 `TacticSlotRow` 위로 **드래그** (자식이 됨).
2. 인스턴스 선택:
   - 이름 변경: `RoleDropdown`
   - `Custom Dropdown` 컴포넌트:
     - **`Init At Start`** 체크 해제 (false)
     - **`Enable Icon`** 체크 해제 (false)

### 1.6 DutySelector 추가 (셋째 자식)
1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Horizontal Selector/Horizontal Selector.prefab` 을 `TacticSlotRow` 위로 드래그.
2. 인스턴스 선택:
   - 이름 변경: `DutySelector`
   - `Horizontal Selector` 컴포넌트:
     - **`Enable Icon`** 체크 해제 (false)
     - `Enable Indicators` 그대로 두기 (true)

### 1.7 TacticSlotRowController 와이어링 ⭐
TacticSlotRow 루트 선택 → Inspector 의 `Tactic Slot Row Controller` 컴포넌트의 3 슬롯에 자식을 드래그:

| 컨트롤러 필드 | 드래그할 자식 |
|---|---|
| `Position Label` | `PositionLabel` |
| `Role Dropdown` | `RoleDropdown` |
| `Duty Selector` | `DutySelector` |

3 슬롯 모두 `None` 이 아닌 GameObject 가 표시되어야 함.

### 1.8 Prefab 으로 저장
1. Project 창에서 `Assets/Imported/FMLite UI/Prefabs/` 폴더를 연다.
2. Hierarchy 의 `TacticSlotRow` 를 그 폴더 안으로 **드래그**.
   - Prefab 생성 다이얼로그가 뜨면 `Original Prefab` 선택.
3. Hierarchy 의 `TacticSlotRow` 는 이제 prefab instance (파란 아이콘). **삭제** (씬에 둘 필요 없음 — 런타임에 컨트롤러가 인스턴스화 함).

---

## Step 2 — TacticScene 에 SlotsScrollView 추가

### 2.1 ScrollView 생성
1. TacticScene 의 Canvas 자식 우클릭 → `UI → Scroll View` → 이름 `SlotsScrollView`.
2. RectTransform: Anchor preset = **top-center**, Pos Y = `-380`, Width = `900`, Height = `560`.
3. Scroll Rect 컴포넌트:
   - `Horizontal` **체크 해제** (수평 스크롤 X)
   - `Vertical` **체크 유지** (수직 스크롤만)
4. Hierarchy 에서 `SlotsScrollView/Scrollbar Horizontal` 우클릭 → `Delete` (불필요).

### 2.2 Content 설정 (rows 가 들어갈 곳)
1. Hierarchy 에서 `SlotsScrollView → Viewport → Content` 를 선택.
2. **`Add Component` → `Vertical Layout Group`**:
   - Padding: Top `10`, Bottom `10`, Left `0`, Right `0`
   - Spacing: `5`
   - Child Alignment: `Upper Center`
   - Child Force Expand: Width / Height **둘 다 체크 해제**
   - Child Controls Size: Width / Height **둘 다 체크 해제**
3. **`Add Component` → `Content Size Fitter`**:
   - Horizontal Fit: `Unconstrained`
   - Vertical Fit: **`Preferred Size`**

### 2.3 TacticController 의 SlotRow 슬롯 와이어링 ⭐
1. Hierarchy 에서 `TacticRoot` 선택.
2. Inspector 의 `Tactic Controller` 컴포넌트:

| 필드 | 드래그할 대상 |
|---|---|
| `Slot Row Container` | Hierarchy 의 `Canvas/SlotsScrollView/Viewport/Content` (Transform 슬롯) |
| `Slot Row Prefab` | Project 창의 `Assets/Imported/FMLite UI/Prefabs/TacticSlotRow.prefab` |

---

## Step 3 — 저장
- `Ctrl+S` 로 씬 저장. (`TacticSlotRow.prefab` 은 자동 저장됨.)

---

## 검증 체크리스트
- [ ] `Assets/Imported/FMLite UI/Prefabs/TacticSlotRow.prefab` 파일 존재.
- [ ] Prefab 내부: 자식 3 개 (`PositionLabel`, `RoleDropdown`, `DutySelector`) + `Tactic Slot Row Controller` 컴포넌트 + `Horizontal Layout Group` 컴포넌트.
- [ ] `RoleDropdown` 의 CustomDropdown: `Init At Start = false`, `Enable Icon = false`.
- [ ] `DutySelector` 의 HorizontalSelector: `Enable Icon = false`.
- [ ] `TacticSlotRowController` 의 3 슬롯 모두 채워짐 (None 없음).
- [ ] TacticScene 에 `SlotsScrollView` 존재 (Canvas 자식).
- [ ] SlotsScrollView/Viewport/Content 에 `Vertical Layout Group` + `Content Size Fitter` (Vertical Fit = Preferred Size).
- [ ] `TacticRoot` 의 `Tactic Controller`:
  - `Slot Row Container` = `Canvas/SlotsScrollView/Viewport/Content`
  - `Slot Row Prefab` = `Assets/Imported/FMLite UI/Prefabs/TacticSlotRow.prefab`
- [ ] Console 컴파일 에러 0.

## 문제 발생 시 처리
1. **즉시 멈춤**. `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 에러 메시지 + 어느 Step 에서 막혔는지 + 가능하면 스크린샷 보고.

---

## (참고) Play 모드 동작 검증 — Unity AI 작업 끝난 후 사용자 수동 테스트

1. MainMenu → 새 게임 → Dashboard → Tactic 버튼.
2. TacticScene 진입 시:
   - 기존 Formation / Mentality / Save / Back 그대로 동작.
   - 그 아래 `SlotsScrollView` 에 11 개 row 표시:
     - 4-4-2 기본 포메이션 → Position 라벨 순서: `GK / CB / CB / LB / RB / DM(또는CM) / DM(또는CM) / LM / RM / ST / ST` (FormationSO 의 slotPositions 배열에 따름)
     - 각 row 의 Role 드롭다운 클릭 → **해당 포지션 호환 Role 들** 만 표시 (예: GK row 는 Goalkeeper / Sweeper Keeper / Defender Goalkeeper 등 GK 전용, ST row 는 Poacher / Target Man / Advanced Forward 등 ST 전용).
     - 각 row 의 Duty 셀렉터 → 3 단계 좌우 화살표 (`공격 / 지원 / 수비`).
3. Formation 드롭다운 변경 (4-4-2 → 4-3-3) → 11 row 가 새 포메이션 포지션에 맞춰 **자동 재구성** (Position 라벨 + Role 드롭다운 후보 갱신, 기존 Role 이 새 포지션 호환 안 되면 첫 호환 role 로 자동 변경).
4. 임의 row 에서 Role / Duty 변경 → Save → Dashboard → 다시 TacticScene → **변경값 유지** 확인.
5. 다시 다른 값으로 변경 → Back → Dashboard → 다시 TacticScene → **이전 Save 한 값 유지** (Back 은 변경 폐기).
