# TacticScene 전면 재설계 — Unity AI 작업 지시서

> Unity AI Assistant 에 그대로 전달.
> **`.cs` 파일 절대 금지.** 컴파일 에러 → 즉시 멈추고 보고.
> 본 지시서는 **매우 자세하게** 작성됨. 모든 좌표 / 크기 / 컴포넌트 설정값을 그대로 따를 것.

---

## 0. 컨텍스트 (Unity AI 가 알아야 할 것)

### 프로젝트
- FM-Lite (Unity 6, 축구 매니저). UI 에셋 `Assets/Imported/Modern UI Pack/` (MUIP).
- 새 prefab 저장 위치: `Assets/Imported/FMLite UI/Prefabs/` (유료 에셋 — private repo. 메인 repo 커밋 금지).

### 배경 (이전 시도 실패)
- 기존 TacticScene 의 `SlotsScrollView` 안에 11 slot row 를 ScrollView 로 담는 구조였음.
- **문제 1**: RoleDropdown 펼침이 아래 row 들에 가려짐 → Sub-Canvas + sortingOrder 패턴으로 시도했으나 ScrollView 의 `RectMask2D` 가 펼침을 viewport 안에 가둠 (Unity 알려진 동작 — Sub-Canvas 가 mask clip 을 우회하지 못함).
- **문제 2**: SlotsScrollView 제거하고 일반 컨테이너로 교체 시도도 부분 적용에 그침 → 전체 UI 가 중구난방.
- **결정**: 처음부터 다시. **SlotsScrollView 폐기**, 11 row 를 한 화면에 직접 배치.

### 본 작업 범위
1. `TacticSlotRow.prefab` 의 RoleDropdown 에 Sub-Canvas + GraphicRaycaster 추가 (펼침이 다른 UI 위에 그려지게).
2. 기존 `TacticScene.unity` 폐기, **새 TacticScene** 처음부터 생성.
3. 새 씬에서 **SlotsScrollView 미사용** — 11 row 를 Canvas 직속 자식의 SlotsContainer (VerticalLayoutGroup) 에 배치.
4. Header (Title / Formation / Mentality) + SlotsContainer + Footer (Save / Back) 깔끔한 3 영역 구성.

### 이미 적용된 코드 변경 (Unity AI 가 알아야 할 것)
- `TacticController.BuildSlotRows()` 가 row instantiate 후 `row.GetComponentInChildren<Canvas>` 로 Sub-Canvas 를 찾아 `sortingOrder = count - i` 차등 부여. **본 지시서 Step 1 에서 RoleDropdown 에 Canvas + GraphicRaycaster 를 반드시 추가해야 작동.**

---

## 1. 절대 규칙

1. **`.cs` 파일 절대 금지.** Missing Script 가 보여도 .cs 손대지 말고 멈춰 보고.
2. **MUIP 원본 prefab 수정 금지** — `Assets/Imported/Modern UI Pack/` 안 prefab 자체는 수정 X. 씬에 인스턴스만 드래그.
3. **신규 prefab 은 `Assets/Imported/FMLite UI/Prefabs/` 밑에 저장**.
4. **EventSystem 중복 금지** — MUIP Canvas.prefab 에 포함됨. 새 씬에 별도 추가 X.
5. Step 별로 진행 + 각 Step 끝나면 `Ctrl+S`.

---

## 2. 사전 작업

### 2.1 기존 TacticScene 백업 (안전 장치)
1. Project 창에서 `Assets/Scenes/TacticScene.unity` 우클릭 → `Duplicate`.
2. 새로 생긴 `TacticScene 1.unity` 이름을 `TacticScene_backup.unity` 로 변경.
3. 만일 본 작업 중 문제 발생 시 백업에서 복원.

### 2.2 폰트 확인
- 모든 TMP_Text 의 폰트는 `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset` 사용 (한글 표시).
- 기본 폰트가 다르게 적용되면 매 TMP_Text 마다 인스펙터의 `Font Asset` 필드를 위 폰트로 변경.

---

## Step 1 — TacticSlotRow.prefab 수정 (RoleDropdown 에 Sub-Canvas 추가)

> **이 Step 이 가장 중요.** TacticController 의 sortingOrder 차등 코드가 작동하려면 RoleDropdown 안에 Canvas 컴포넌트가 있어야 함.

### 1.1 Prefab 열기
1. Project 창에서 `Assets/Imported/FMLite UI/Prefabs/TacticSlotRow.prefab` 더블 클릭 → Prefab 편집 모드.

### 1.2 RoleDropdown 자식 선택
- Prefab Hierarchy 에서 `TacticSlotRow/RoleDropdown` 선택.
- (이름이 다르면 `Custom Dropdown` 컴포넌트가 있는 자식 — Inspector 확인)

### 1.3 Canvas 추가
1. `Add Component` → 검색창 `Canvas` → 첫 번째 `Canvas` 선택.
2. 추가된 `Canvas` 컴포넌트 설정:
   - **`Override Sorting`** ✅ 체크
   - **`Sorting Layer`** = `Default` (기본 그대로)
   - **`Order in Layer`** = `10`
   - `Render Mode` 등 나머지 자동 (수정 X)

### 1.4 Graphic Raycaster 추가
1. 같은 `RoleDropdown` 선택 상태에서 `Add Component` → 검색창 `Graphic Raycaster` → 추가.
2. 옵션 기본값 그대로.

### 1.5 Prefab 저장
- Prefab 편집 창 좌상단 `<` (뒤로 가기) → 변경 사항 자동 저장.
- 또는 `Ctrl+S`.

### 1.6 검증
- Project 창에서 prefab 다시 열어 RoleDropdown 인스펙터에 **`Canvas` + `Graphic Raycaster` + `Custom Dropdown`** 3 개 컴포넌트가 모두 있는지 확인.

---

## Step 2 — 기존 TacticScene 삭제

1. Project 창에서 `Assets/Scenes/TacticScene.unity` 우클릭 → `Delete`. (백업 `TacticScene_backup.unity` 는 그대로 둠.)
2. Unity 가 "Delete selected file?" 묻는 다이얼로그 → `Delete`.

---

## Step 3 — 새 TacticScene 생성

1. `File → New Scene`. 템플릿 = `Empty (Built-in)` 선택 → `Create`.
2. `File → Save As` → 파일명 `TacticScene` → 경로 `Assets/Scenes/` → `Save`.
3. Hierarchy 에 `TacticScene` 루트 + (보통) `Main Camera` 만 있는 상태.

---

## Step 4 — Main Camera 확인

- Hierarchy 에 `Main Camera` 가 있어야 함. 없으면 `GameObject → Camera` 로 추가.

---

## Step 5 — Canvas 생성 (MUIP Canvas.prefab 사용)

1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab` 찾기.
2. Hierarchy 의 빈 공간에 **드래그** → 자동 `Canvas` + `EventSystem` 생성됨.
3. Canvas 인스턴스 이름이 `Canvas` 가 아니면 변경 (예: `Canvas (1)` → `Canvas`).
4. ⚠️ 기존 EventSystem 이 이미 있으면 중복 제거 (1 개만).
5. Canvas 인스펙터의 `Canvas Scaler` 컴포넌트 확인:
   - `UI Scale Mode` = `Scale With Screen Size`
   - `Reference Resolution` = `1080 × 1920` (MUIP 기본값. 가로 모드에서도 작동)
   - `Match` = `0.5`
   - `Screen Match Mode` = `Match Width Or Height` (기본)

---

## Step 6 — TacticRoot (컨트롤러 호스트)

1. Hierarchy 의 빈 공간 우클릭 → `Create Empty` → 이름 `TacticRoot`.
2. `TacticRoot` 선택 → Inspector → `Add Component` → 검색 `TacticController` → 추가.
3. Inspector 에 `Tactic Controller` 컴포넌트가 보이며 모든 필드가 `None`. **Step 14 에서 채울 것.**

---

## Step 7 — Background (선택적, 그러나 권장)

1. Canvas 자식 우클릭 → `UI → Image` → 이름 `Background`.
2. RectTransform:
   - Anchor preset = `stretch-stretch` (Alt 누른 채 우하단 stretch 아이콘 클릭하면 offset 자동 0).
   - Left / Right / Top / Bottom = `0`.
3. `Image` 컴포넌트:
   - `Color` = `#1A1A2E` (씬 배경 어두운 톤).
   - Alpha = `255`.
4. `Image` 의 `Raycast Target` **체크 해제** (배경은 클릭 막지 않도록).

---

## Step 8 — 상단 영역 — TitleText + BackButton (top-left) + SaveButton (top-right)

> 사용자 피드백: 11 row 가 화면 아래쪽까지 가득 차므로 **Footer 폐기**. Save/Back 을 화면 상단 양 모서리로 이동.

### 8.1 TitleText (상단 중앙)
1. Canvas 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `TitleText`.
2. (TMP Essentials Import 다이얼로그 뜨면 `Import TMP Essentials` 클릭)
3. RectTransform:
   - Anchor preset = `top-center` (Alt 누른 채 클릭하면 pivot 도 자동 위쪽).
   - **Pos X = `0` / Pos Y = `-55`**
   - **Width = `600` / Height = `50`**
4. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text = `Tactic` (런타임에 컨트롤러가 `tactic_title` 로 덮어씀 — placeholder)
   - Font Asset = `NotoSansKR-VF SDF`
   - Font Size = `42`
   - Alignment = `Center / Middle`
   - Color = White (`#FFFFFF`)

### 8.2 BackButton (상단 좌측 모서리)
1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` 을 Hierarchy 의 `Canvas` 위로 드래그.
2. 인스턴스 이름 → `BackButton`.
3. RectTransform:
   - Anchor preset = `top-left` (Alt 클릭).
   - **Pos X = `120` / Pos Y = `-55`** (Anchor 가 top-left 라 0,0 이 좌상단 → +X 가 오른쪽, -Y 가 아래)
   - **Width = `180` / Height = `60`**
4. `ButtonManager.Button Text` 그대로 (런타임에 컨트롤러가 "뒤로" 로 덮어씀).

### 8.3 SaveButton (상단 우측 모서리)
1. 같은 prefab 을 `Canvas` 위로 한 번 더 드래그.
2. 인스턴스 이름 → `SaveButton`.
3. RectTransform:
   - Anchor preset = `top-right` (Alt 클릭).
   - **Pos X = `-120` / Pos Y = `-55`** (Anchor 가 top-right → -X 가 왼쪽)
   - **Width = `180` / Height = `60`**
4. `ButtonManager.Button Text` 그대로.

### 8.4 검증
- 두 버튼 모두 `ButtonManager` 컴포넌트 (Inspector 확인). 아니면 prefab 잘못 — 삭제 후 재드래그.

---

## Step 9 — HeaderRow (Formation + Mentality 한 줄)

> 사용자 피드백 ("중구난방") 해결 — Formation 과 Mentality 를 한 줄 안에 좌/우 분할.

### 9.1 HeaderRow 빈 컨테이너 생성
1. Canvas 자식 우클릭 → `Create Empty` → 이름 `HeaderRow`.
2. RectTransform:
   - Anchor preset = `top-center`.
   - **Pos X = `0` / Pos Y = `-145`**
   - **Width = `1000` / Height = `80`**

### 9.2 FormationGroup (좌측: Label + Dropdown)
1. `HeaderRow` 자식 우클릭 → `Create Empty` → 이름 `FormationGroup`.
2. RectTransform:
   - Anchor preset = `middle-left` (Alt 클릭).
   - **Pos X = `0` / Pos Y = `0`**
   - **Width = `460` / Height = `60`**
3. `FormationGroup` 에 `Add Component` → `Horizontal Layout Group`:
   - Padding: Left `0`, Top `0`, Right `0`, Bottom `0`
   - Spacing: `15`
   - Child Alignment: `Middle Left`
   - Child Force Expand: Width / Height **둘 다 해제**
   - Child Controls Size: Width / Height **둘 다 해제**

#### 9.2.1 FormationLabel
1. `FormationGroup` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `FormationLabel`.
2. RectTransform: Width = `120`, Height = `50`.
3. TMP:
   - Text = `포메이션`
   - Font Asset = `NotoSansKR-VF SDF`
   - Font Size = `28`
   - Alignment = `Right / Middle`
   - Color = White

#### 9.2.2 FormationDropdown 인스턴스
1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab` 을 Hierarchy 의 `FormationGroup` 위로 **드래그**.
2. 인스턴스 선택 후 이름 변경 → `FormationDropdown`.
3. RectTransform: **Width = `320`, Height = `50`** (Horizontal Layout Group 안에 있어 자동 stretch 되지만 명시).
4. `Custom Dropdown` 컴포넌트:
   - **`Init At Start`** 체크 **해제** (false) — 컨트롤러가 Start 에서 채움.
   - **`Enable Icon`** 체크 **해제** (false) — 아이콘 없음.
5. **`Add Component` → `Canvas`**:
   - **`Override Sorting`** ✅ 체크
   - **`Sorting Order`** = `20` (slot row 들의 max sortingOrder=11 보다 높게 → 펼침이 모든 slot 위에 그려짐)
6. **`Add Component` → `Graphic Raycaster`**: 기본값.

### 9.3 MentalityGroup (우측: Label + Selector)
1. `HeaderRow` 자식 우클릭 → `Create Empty` → 이름 `MentalityGroup`.
2. RectTransform:
   - Anchor preset = `middle-right` (Alt 클릭).
   - **Pos X = `0` / Pos Y = `0`** (Anchor 가 right 라 자동 우측 끝)
   - **Width = `500` / Height = `70`**
3. `MentalityGroup` 에 `Add Component` → `Horizontal Layout Group`:
   - Padding `0` / Spacing `15`
   - Child Alignment: `Middle Right`
   - Force Expand / Controls Size 모두 해제

#### 9.3.1 MentalityLabel
1. `MentalityGroup` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `MentalityLabel`.
2. Width = `120`, Height = `50`.
3. TMP:
   - Text = `전술 성향`
   - Font Asset = `NotoSansKR-VF SDF`
   - Font Size = `28`
   - Alignment = `Right / Middle`
   - Color = White

#### 9.3.2 MentalitySelector 인스턴스
1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Horizontal Selector/Horizontal Selector.prefab` 을 Hierarchy 의 `MentalityGroup` 위로 드래그.
2. 인스턴스 이름 → `MentalitySelector`.
3. RectTransform: Width = `360`, Height = `60`.
4. `Horizontal Selector` 컴포넌트:
   - **`Enable Icon`** 체크 **해제**
   - **`Enable Indicators`** 체크 유지 (7 개 점 표시)
   - 나머지 기본값.

---

## Step 10 — SlotsContainer (11 row 직접 배치, ScrollView 폐기)

> 사용자 피드백 ("dropdown 영역 침범" + "양 옆 공간 활용") 해결 — RectMask2D 없는 일반 컨테이너, 가로 폭 1400 으로 확장.
> Footer 폐기 + Save/Back 모서리 이동 (Step 8) 으로 세로 공간도 확보.

### 10.1 SlotsContainer 생성
1. Canvas 자식 우클릭 → `Create Empty` → 이름 `SlotsContainer`.
2. RectTransform:
   - Anchor preset = `top-center` (Alt 클릭).
   - **Pos X = `0` / Pos Y = `-260`** (HeaderRow 아래)
   - **Width = `1400` / Height = `780`** (양 옆 공간 활용 + 11 row 안전 수용)

### 10.2 Vertical Layout Group
1. `SlotsContainer` 선택 → `Add Component` → `Vertical Layout Group`:
   - **Padding**: Top `10`, Bottom `10`, Left `100`, Right `100` (1400 - 200 = 1200 가 row 가로 폭)
   - **Spacing**: `4`
   - **Child Alignment**: `Upper Center`
   - **Child Force Expand**: Width / Height **둘 다 해제**
   - **Child Controls Size**: Width / Height **둘 다 해제**

> ℹ️ row prefab 은 가로 800px 인데 본 컨테이너가 1200px → row 가 가운데 정렬. row prefab 의 Width 를 1200 으로 늘리려면 prefab 수정 별도 작업.

### 10.3 (선택) 배경 시각화 — 영역 인식 보조
1. `SlotsContainer` 자식 우클릭 → `UI → Image` → 이름 `BgImage`.
2. RectTransform: stretch-stretch (offset 0).
3. Image:
   - Color = `#2A2A3E` (패널 배경 톤)
   - Alpha = `180`
   - Raycast Target **해제**
4. **⚠️ `BgImage` 가 Layout 영향 받지 않도록**:
   - `Add Component` → `Layout Element` → `Ignore Layout` ✅ 체크.

> ℹ️ BG 가 거슬리면 Step 10.3 전체 건너뛰기.

---

## Step 11 — (Footer 폐기) Save/Back 은 Step 8 에서 처리됨

> 이전 디자인의 Footer 는 폐기. Save = 우상단 / Back = 좌상단 모서리에 배치 (Step 8.2, 8.3 참조).
> Footer 없으므로 SlotsContainer 가 화면 하단까지 활용 가능.

---

## Step 12 — TacticController 와이어링 ⭐ (가장 중요)

> 코드에서 자동 처리되는 부분:
> - 모든 버튼의 `Click Event` (Save / Back) → 자동 등록.
> - 모든 dropdown / selector 의 `Init`, `SetupDropdown / SetupSelector` → 자동 호출.
> 이 Step 에서는 **SerializeField 슬롯에 참조만 드래그**.

1. Hierarchy 에서 `TacticRoot` 선택 → Inspector 의 `Tactic Controller` 컴포넌트.
2. 아래 표대로 모든 슬롯 채우기:

| TacticController 필드 | 드래그할 대상 |
|---|---|
| `Title Text` | `Canvas/TitleText` |
| `Formation Dropdown` | `Canvas/HeaderRow/FormationGroup/FormationDropdown` |
| `Mentality Selector` | `Canvas/HeaderRow/MentalityGroup/MentalitySelector` |
| `Save Button` | `Canvas/Footer/SaveButton` |
| `Back Button` | `Canvas/Footer/BackButton` |
| `Slot Row Container` | `Canvas/SlotsContainer` |
| `Slot Row Prefab` | Project 창의 `Assets/Imported/FMLite UI/Prefabs/TacticSlotRow.prefab` |

3. **모든 슬롯이 `None` 아닌 값으로 채워졌는지 검증** (8 개 슬롯).

> ⚠️ 각 GameObject 의 **루트** 를 드래그. Unity 가 컴포넌트 (CustomDropdown / HorizontalSelector / ButtonManager / TMP_Text / Transform) 를 자동 바인딩.

---

## Step 13 — Build Settings 등록

1. `File → Build Profiles` (또는 `File → Build Settings`) 열기.
2. `Scenes In Build` 리스트에 다음 확인:
   - `Scenes/TacticScene` ✅ 등록 (현재 씬이 열려있는 상태에서 `Add Open Scenes` 클릭하면 자동 추가)
   - `Scenes/DashboardScene` ✅ (이미 등록되어 있어야 함)
3. 둘 다 체크박스 ON.
4. 창 닫기.

---

## Step 14 — 씬 저장

`Ctrl+S`.

---

## 검증 체크리스트

### TacticSlotRow.prefab (Step 1)
- [ ] `Assets/Imported/FMLite UI/Prefabs/TacticSlotRow.prefab` 의 `RoleDropdown` 자식에 `Canvas` + `Graphic Raycaster` 컴포넌트 추가됨.
- [ ] Canvas: `Override Sorting = true`, `Sorting Order = 10`.

### TacticScene 구조
- [ ] `Assets/Scenes/TacticScene.unity` 파일 존재. (백업 `TacticScene_backup.unity` 도 존재 — 안전 장치)
- [ ] Hierarchy 구조:
  ```
  TacticScene
  ├── Main Camera
  ├── EventSystem            (MUIP Canvas 자동 포함)
  ├── TacticRoot              (← TacticController 호스트)
  └── Canvas                  (MUIP, Overlay, 1080×1920, Match 0.5)
      ├── Background           (Image, #1A1A2E, stretch)
      ├── TitleText            (TMP, "Tactic", top-center Y=-55)
      ├── BackButton           (ButtonManager, top-left X=120 Y=-55)
      ├── SaveButton           (ButtonManager, top-right X=-120 Y=-55)
      ├── HeaderRow
      │   ├── FormationGroup
      │   │   ├── FormationLabel       (TMP, "포메이션")
      │   │   └── FormationDropdown    (CustomDropdown + Canvas Order=20 + GraphicRaycaster)
      │   └── MentalityGroup
      │       ├── MentalityLabel       (TMP, "전술 성향")
      │       └── MentalitySelector    (HorizontalSelector)
      └── SlotsContainer        (VerticalLayoutGroup, Width=1400, Height=780)
          └── (런타임에 TacticSlotRow × 11 자동 생성)
  ```

### TacticController 와이어링
- [ ] `TacticRoot.TacticController` 의 8 슬롯 모두 채워짐 (`Title Text` / `Formation Dropdown` / `Mentality Selector` / `Save Button` / `Back Button` / `Slot Row Container` / `Slot Row Prefab`).

### 컴포넌트 설정
- [ ] FormationDropdown: `Init At Start = false`, `Enable Icon = false`, Canvas `Override Sorting = true` `Order = 20`.
- [ ] MentalitySelector: `Enable Icon = false`.
- [ ] SaveButton / BackButton: 컴포넌트 = `ButtonManager` (아닌 경우 prefab 잘못 — Basic - Outline/Standard.prefab 인지 확인).

### Build Settings
- [ ] `Scenes/TacticScene` Scenes In Build 등록.

### Console
- [ ] 컴파일 에러 0 / Missing Reference 경고 0.

---

## Play 모드 동작 검증 — Unity AI 작업 끝난 후 사용자 수동 테스트

### 진입
1. MainMenu → 새 게임 → Dashboard → [전술] 버튼 클릭 → TacticScene 진입.

### 헤더
2. TitleText = `전술` (또는 영어 `Tactic`, 언어 설정 따라).
3. FormationDropdown 클릭 → 6 개 포메이션 (4-4-2 / 4-3-3 / 3-5-2 / 4-2-3-1 / 4-4-1-1 / 5-3-2) 표시.
4. MentalitySelector 좌우 화살표 → 7 단계 ("매우 수비적" ~ "매우 공격적") 순환.

### 11 슬롯 row
5. SlotsContainer 안에 11 row 표시 (스크롤 없음, 한 화면에 다 들어옴).
6. 4-4-2 기본 포메이션 → row 의 Position 라벨: `GK / CB / CB / LB / RB / DM(또는CM) / DM(또는CM) / LM / RM / ST / ST` (FormationSO.slotPositions 따름).

### RoleDropdown 펼침 (핵심 검증)
7. **첫 row (GK)** 의 RoleDropdown 클릭 펼침 → 호환 Role 목록 (`Goalkeeper / Sweeper Keeper` 등) 이 아래 row 들 위에 명확히 표시. **영역 밖 새어남 없음** (ScrollView 가 없으므로).
8. **중간 row (예: DM)** RoleDropdown 펼침 → 다음 row 들 (LM/RM/ST...) 위에 그려짐.
9. **마지막 row (ST)** RoleDropdown 펼침 → Footer (Save/Back) 위까지 펼쳐질 수 있음. 영역 침범 시각적으로 거슬리면 → Step 10.1 의 SlotsContainer Pos Y / Height 조정 또는 Footer Y 조정 (별도 작업).

### Formation 변경
10. FormationDropdown 4-4-2 → 4-3-3 변경 → 11 row 가 새 포지션으로 재구성 (Position 라벨 + Role 후보 갱신).

### Mentality
11. MentalitySelector "Balanced" → "Attacking" 변경 → (저장 후 매치에 반영, UI 즉시 변화 X).

### Save / Back
12. 임의 row 에서 Role / Duty 변경 → Save 클릭 → DashboardScene 복귀 + 변경값 저장.
13. 다시 TacticScene 진입 → 저장값 유지 확인.
14. 다른 값으로 변경 → Back 클릭 → DashboardScene 복귀 (변경 폐기).
15. 다시 TacticScene 진입 → 이전 Save 값 그대로 (Back 변경분 X).

---

## 문제 발생 시 처리

1. **즉시 멈춤.** `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 다음을 보고:
   - 에러 메시지 전문 (Console 복사)
   - 어느 Step / Sub-step 에서 막혔는지
   - 가능하면 Inspector / Hierarchy / Game view 스크린샷
3. 보고는 사용자가 Claude Code 에 전달 → 다음 단계 결정.

### 자주 발생하는 문제

| 증상 | 해결 |
|------|------|
| RoleDropdown 펼침이 안 보임 (또는 펼친 자리에 빈 공간) | Step 1 의 Canvas + GraphicRaycaster 가 RoleDropdown 자식에 정말 추가됐는지 확인. `Order in Layer = 10` 확인. |
| FormationDropdown 펼침이 slot row 들에 가려짐 | Step 9.2.2 의 FormationDropdown Canvas `Order = 20` 확인. slot row sortingOrder 는 max 11. |
| 11 row 가 안 보임 (SlotsContainer 비어있음) | Step 12 의 `Slot Row Container` (= SlotsContainer) + `Slot Row Prefab` (= TacticSlotRow.prefab) 둘 다 와이어링됐는지 확인. |
| 11 row 가 SlotsContainer 영역 밖으로 넘침 | Step 10.1 의 SlotsContainer Height `730` 확인. 또는 row 의 prefab 자체 Height 조정 (60 → 50). |
| 텍스트가 깨짐 / `??` 박스 | NotoSansKR-VF SDF 폰트가 적용 안 됐을 가능성. 모든 TMP_Text 의 Font Asset 확인. |
| Save / Back 클릭 시 씬 전환 X | Button 인스턴스가 `ButtonManager` 가 아닌 `ButtonManagerBasic` 일 수 있음. prefab 경로 재확인 (Basic - Outline 폴더). |
| Console "EventSystem 중복" 경고 | Hierarchy 에 EventSystem 1 개만 남기고 나머지 삭제. |

---

## 변경 요약 (이전 시도 대비)

| 항목 | 이전 시도 | 본 작업 |
|------|----------|---------|
| 11 row 배치 | SlotsScrollView (ScrollView + RectMask2D) 안 | Canvas 직속 자식 SlotsContainer (VerticalLayoutGroup) |
| RoleDropdown 펼침 영역 | viewport 안에 mask clip | 영역 제한 없음 |
| TacticSlotRow.prefab | Sub-Canvas 없음 | RoleDropdown 자식에 Canvas Order=10 + GraphicRaycaster |
| FormationDropdown 펼침 | slot row 들과 같은 sort layer | Canvas Order=20 → slot row max(11) 보다 높게 |
| Header 구성 | 분산 (FormationRow + MentalityRow 따로) | HeaderRow 한 줄에 좌/우 분할 |
| 코드 변경 | TacticController 에 sortingOrder 차등 X | TacticController.BuildSlotRows() 가 row.GetComponentInChildren<Canvas> → sortingOrder = count - i (이 PR 에 포함됨) |
