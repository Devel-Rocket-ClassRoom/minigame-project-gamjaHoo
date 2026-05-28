# MentoringScene — Unity AI 작업 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.
> 이 문서의 모든 단계는 **에디터 안 작업**. **`.cs` 파일은 절대 손대지 말 것.**

---

## 컨텍스트 (Unity AI 가 알아야 할 것)

- 프로젝트: FM-Lite (Unity 6, 축구 매니저 게임).
- **UI 에셋**: `Assets/Imported/Modern UI Pack/` (MUIP). 버튼·드롭다운·토글 모두 이 에셋 프리팹 사용.
- 컨트롤러는 이미 작성되어 있다:
  - `Assets/_Project/Scripts/UI/MentoringController.cs` — SerializeField 9 개
  - `Assets/_Project/Scripts/UI/MentoringGroupItem.cs` — SerializeField 3 개
- **SerializeField 타입 주의사항**:
  - `mentorDropdown` → `CustomDropdown` (MUIP 컴포넌트). Unity 기본 TMP_Dropdown 아님.
  - `confirmCreateButton`, `cancelCreateButton`, `createGroupButton`, `backButton` → `ButtonManager` (MUIP 컴포넌트).
  - `dissolveButton` in MentoringGroupItem → `Button` (Unity 기본).
  - **버튼 OnClick 이벤트**: `createGroupButton`, `backButton`, `confirmCreateButton`, `cancelCreateButton` 는 **MentoringController.Start() 에서 코드로 자동 연결됨** — Inspector 에서 별도 OnClick 연결 불필요.
- 작업 목록:
  1. `MentoringGroupItem.prefab` 생성
  2. `MenteeToggleItem.prefab` 생성 (MUIP Toggle 기반)
  3. `MentoringScene.unity` 생성
  4. DashboardScene 에 "멘토링" 진입 버튼 추가

---

## 절대 규칙

1. **`.cs` 파일 절대 금지.** 컴파일 에러가 나도 .cs 손대지 말고 즉시 멈춰서 보고.
2. **`Assets/Imported/Modern UI Pack/` 원본 prefab 수정 금지.** 씬에 인스턴스를 드래그해 배치만.
3. **신규 prefab 은 반드시 `Assets/Imported/FMLite UI/Prefabs/` 폴더에 저장.**
4. 모든 작업 완료 후 `Ctrl+S` 저장.
5. ⚠️ **EventSystem 필수**: Canvas 생성 시 반드시 EventSystem 이 Hierarchy 에 있어야 함. **없으면 버튼·드롭다운·토글 등 UI 인터랙션 전혀 동작 안 함.** 이 문서는 MUIP Canvas.prefab 사용으로 EventSystem 을 자동 포함시킨다 (Step 3.3 참조).

---

## Step 1 — MentoringGroupItem.prefab 생성

그룹 목록의 한 행. 멘토 이름 + 멘티 이름들 + 해체 버튼으로 구성.

### 1.1 임시 GameObject 생성

1. Hierarchy 우클릭 → `Create Empty` → 이름 `MentoringGroupItem`.
2. Inspector 에서 RectTransform:
   - Width = `900`, Height = `80`

### 1.2 컴포넌트 추가

`MentoringGroupItem` 선택 → Inspector:

1. `Add Component` → `Image`:
   - Color: `#2A2A2A`, Alpha `255`
2. `Add Component` → `Horizontal Layout Group`:
   - Padding: Left `16`, Top `0`, Right `16`, Bottom `0`
   - Spacing: `16`
   - Child Alignment: `Middle Left`
   - Child Force Expand Width **체크**, Height **체크 해제**
   - Child Controls Size Width **체크 해제**, Height **체크 해제**
3. `Add Component` → 검색창 `Mentoring Group Item` → 추가.
   - Inspector 에 `Mentoring Group Item` 컴포넌트가 나타나며 필드 3 개가 `None` 으로 표시됨. **Step 1.5 에서 채울 것.**

### 1.3 MentorLabel 추가 (첫째 자식)

1. `MentoringGroupItem` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `MentorLabel`.
   - "Import TMP Essentials" 다이얼로그가 뜨면 `Import TMP Essentials` 클릭.
2. RectTransform: Width = `280`, Height = `60`.
3. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `멘토 이름`
   - Font Size: `28`
   - Alignment: 가로 `Left`, 세로 `Middle`
   - Color: White (`#FFFFFF`)
4. `Add Component` → `Layout Element`:
   - Min Width: `280`
   - Flexible Width: `0`

### 1.4 MenteesLabel 추가 (둘째 자식)

1. `MentoringGroupItem` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `MenteesLabel`.
2. RectTransform: Width = `440`, Height = `60`.
3. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `멘티 목록`
   - Font Size: `24`
   - Alignment: 가로 `Left`, 세로 `Middle`
   - Color: Light Gray (`#BBBBBB`)
4. `Add Component` → `Layout Element`:
   - Flexible Width: `1`

### 1.5 DissolveButton 추가 (셋째 자식, MUIP Button)

1. Project 창에서 다음 경로의 prefab 찾기:
   `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab`
   ⚠️ `Basic/Standard.prefab` 아님. 반드시 **`Basic - Outline`** 폴더 안의 `Standard.prefab`.
2. 해당 prefab 을 Hierarchy 의 `MentoringGroupItem` 위로 **드래그** (자식으로 들어감).
3. 생성된 인스턴스:
   - 이름 변경: `DissolveButton`
   - Inspector 의 **`Button Manager`** 컴포넌트:
     - `Button Text` 필드: `해체`
     - 나머지 설정 기본값 그대로.
4. `Add Component` → `Layout Element`:
   - Min Width: `120`
   - Flexible Width: `0`

> ℹ️ MUIP 버튼 루트 GameObject 에는 `ButtonManager` 와 `Button` 컴포넌트가 함께 있다.
> `MentoringGroupItem.dissolveButton` 필드가 `Button` 타입이므로, 아래 와이어링 시 루트를 드래그하면 Unity 가 `Button` 컴포넌트를 자동 바인딩한다.

### 1.6 MentoringGroupItem 와이어링 ⭐

`MentoringGroupItem` 루트 선택 → Inspector 의 `Mentoring Group Item` 컴포넌트 3 개 필드:

| 컴포넌트 필드 | 드래그할 자식 오브젝트 |
|---|---|
| `Mentor Label` | `MentorLabel` (TMP_Text 오브젝트) |
| `Mentees Label` | `MenteesLabel` (TMP_Text 오브젝트) |
| `Dissolve Button` | `DissolveButton` (MUIP 버튼 루트) |

⚠️ 3개 필드 모두 `None` 이 없어야 함. 특히 `Dissolve Button` 슬롯 타입이 `Button` 이므로
`DissolveButton` 루트를 드래그하면 자동으로 `Button` 컴포넌트가 바인딩됨.

> `DissolveButton` 의 OnClick 이벤트는 코드에서 동적 연결됨 → Inspector 에서 별도 연결 불필요.

### 1.7 Prefab 저장

1. `Assets/Imported/FMLite UI/Prefabs/` 폴더로 Hierarchy 의 `MentoringGroupItem` 드래그.
2. 다이얼로그 → `Original Prefab` 선택.
3. Hierarchy 의 `MentoringGroupItem` 삭제.

---

## Step 2 — MenteeToggleItem.prefab 생성 (MUIP Toggle 기반)

그룹 만들기 패널에서 멘티를 선택하는 토글 행. MUIP Toggle - Standard 기반.

### 2.1 MUIP Toggle 인스턴스 드래그

1. Project 창에서 다음 prefab 찾기:
   `Assets/Imported/Modern UI Pack/Prefabs/Toggle/Toggle - Standard (Regular).prefab`
2. Hierarchy 에 드래그.

### 2.2 Prefab Unpack

1. Hierarchy 에서 방금 생성된 인스턴스 우클릭.
2. `Prefab → Unpack Completely` 선택.
   - 이제 이 오브젝트는 MUIP 원본 prefab 과 연결이 끊어진 독립 GameObject.

### 2.3 이름 및 크기 설정

1. 이름 변경: `MenteeToggleItem`.
2. RectTransform: Width = `600`, Height = `56`.

### 2.4 기존 텍스트 라벨 → NameLabel 로 변환

1. Hierarchy 에서 `MenteeToggleItem` 를 펼치기 (자식 목록 확인).
2. TMP_Text 컴포넌트가 붙은 자식을 찾는다 (이름이 `Label` 또는 `Toggle Label`).
3. 그 오브젝트 이름을 **`NameLabel`** 로 변경.
4. `NameLabel` 선택 → Inspector 의 `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `선수 이름`
   - Font Size: `26`
   - Alignment: 가로 `Left`, 세로 `Middle`
   - Color: White (`#FFFFFF`)

### 2.5 Toggle 컴포넌트 확인

`MenteeToggleItem` 루트 선택 → Inspector 의 `Toggle` 컴포넌트 확인:
- `Is On`: **체크 해제** (false)
- `Graphic`: `Background` 아래 `Checkmark` 이미지가 연결되어 있는지 확인.
  - 연결 안 됐으면: Hierarchy 에서 `Background/Checkmark` 오브젝트를 `Graphic` 슬롯으로 드래그.
- `On Value Changed` 이벤트: **비워둠** (코드에서 동적 연결).

> ℹ️ `MentoringController.cs` 는 런타임에 `GetComponent<Toggle>()` 로 이 컴포넌트를 찾는다.
> `CustomToggle` 컴포넌트도 있지만 그대로 두면 됨 — 건드리지 말 것.

### 2.6 Prefab 저장

1. `Assets/Imported/FMLite UI/Prefabs/` 폴더로 Hierarchy 의 `MenteeToggleItem` 드래그.
2. 다이얼로그 → `Original Prefab` 선택.
3. Hierarchy 의 `MenteeToggleItem` 삭제.

---

## Step 3 — MentoringScene.unity 생성

### 3.1 새 씬 만들기

1. `File → New Scene` → 템플릿 `Empty (Built-in)`.
2. `File → Save As` → 경로: `Assets/Scenes/MentoringScene.unity`.

### 3.2 기본 오브젝트 확인

- Hierarchy 에 `Main Camera` 있는지 확인 (없으면 우클릭 → `Camera`).
- ⚠️ EventSystem 은 Step 3.3 의 MUIP Canvas.prefab 드래그로 자동 추가됨. 여기서 별도 추가 불필요.

### 3.3 Canvas 생성 (MUIP Canvas.prefab 사용)

1. Project 창에서 다음 prefab 찾기:
   `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab`
2. 해당 prefab 을 Hierarchy 에 **드래그**.
   - 이 prefab 에는 **Canvas + EventSystem 이 이미 포함**되어 있다.
   - Canvas 설정도 이미 올바르게 구성됨: Screen Space - Overlay, 1920×1080, Scale With Screen Size, Match 0.5.
3. 생성된 루트 오브젝트 이름 확인: `Canvas`. 다르면 이름 변경.
4. Hierarchy 에 `Canvas` 와 그 자식 `Event System` 이 보이면 정상.

> ⚠️ 이미 Hierarchy 에 EventSystem 이 있으면 중복 삭제. EventSystem 은 씬에 1개만 있어야 함.

### 3.4 Background (선택)

1. Canvas 자식 우클릭 → `UI → Image` → 이름 `Background`.
2. RectTransform: Anchor preset = **stretch-stretch** (Alt 누른 채 클릭 → offset 전부 0 자동 설정).
3. `Image` 컴포넌트 → Color: `#1A1A2E`, Alpha `255`.

---

### 3.5 Header 구역

#### 3.5a Header 컨테이너

1. Canvas 자식 우클릭 → `Create Empty` → 이름 `Header`.
2. RectTransform:
   - Anchor preset = **top-stretch** (Alt 누른 채 클릭).
   - Pos Y = `-60`, Height = `120`, Left = `0`, Right = `0`.

#### 3.5b BackButton (Header 자식, MUIP Button)

1. Project 창: `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` → `Header` 위로 드래그.
2. 인스턴스:
   - 이름: `BackButton`
   - RectTransform: Anchor = **middle-left** (Alt 누른 채 클릭), Pos X = `80`, Pos Y = `0`, Width = `160`, Height = `60`
   - **`Button Manager` 컴포넌트**: `Button Text` = `← 뒤로`

> ℹ️ BackButton 의 onClick 은 MentoringController.Start() 에서 자동 연결됨. Inspector OnClick 연결 불필요.

#### 3.5c TitleText (Header 자식)

1. `Header` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `TitleText`.
2. RectTransform: Anchor = **middle-center**, Pos X = `0`, Pos Y = `0`, Width = `700`, Height = `70`.
3. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `멘토링 관리`
   - Font Size: `48`
   - Alignment: 가로 `Center`, 세로 `Middle`
   - Color: White (`#FFFFFF`)

---

### 3.6 GroupScrollView 생성

1. Canvas 자식 우클릭 → `UI → Scroll View` → 이름 `GroupScrollView`.
2. RectTransform:
   - Anchor preset = **top-center**.
   - Pos X = `0`, Pos Y = `-240`, Width = `960`, Height = `640`.
3. **`Scroll Rect` 컴포넌트**:
   - `Horizontal` **체크 해제**
   - `Vertical` **체크**
4. Hierarchy 에서 `GroupScrollView/Scrollbar Horizontal` 우클릭 → `Delete`.

#### 3.6a GroupScrollView Content 설정

`GroupScrollView/Viewport/Content` 선택:

1. `Add Component` → `Vertical Layout Group`:
   - Padding: Top `8`, Bottom `8`, Left `0`, Right `0`
   - Spacing: `6`
   - Child Alignment: `Upper Center`
   - Child Force Expand Width **체크**, Height **체크 해제**
   - Child Controls Size Width **체크 해제**, Height **체크 해제**
2. `Add Component` → `Content Size Fitter`:
   - Horizontal Fit: `Unconstrained`
   - Vertical Fit: `Preferred Size`

---

### 3.7 CreateGroupButton (MUIP Button)

1. Project 창: `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` → Canvas 위로 드래그.
2. 인스턴스:
   - 이름: `CreateGroupButton`
   - RectTransform: Anchor = **bottom-center**, Pos Y = `80`, Width = `300`, Height = `65`
   - **`Button Manager` 컴포넌트**: `Button Text` = `+ 그룹 만들기`

> ℹ️ CreateGroupButton 의 onClick 은 MentoringController.Start() 에서 자동 연결됨.

---

### 3.8 CreatePanel (전체화면 오버레이, 초기 비활성)

#### 3.8a CreatePanel 루트

1. Canvas 자식 우클릭 → `Create Empty` → 이름 `CreatePanel`.
2. RectTransform: Anchor = **stretch-stretch** (Alt 클릭 → offset 전부 0). 전체 화면 덮음.
3. ⚠️ **GameObject 비활성**: Inspector 좌상단 체크박스 **해제** (Active = false).

#### 3.8b DimBackground (어두운 배경 레이어)

1. `CreatePanel` 자식 우클릭 → `UI → Image` → 이름 `DimBackground`.
2. RectTransform: Anchor = **stretch-stretch** (Alt 클릭 → offset 전부 0).
3. `Image` 컴포넌트:
   - Color: `#000000`, Alpha = `180`
   - `Raycast Target`: **체크** (클릭이 뒤로 통과되지 않도록).

#### 3.8c PanelBox (폼 영역, 라운드 모서리 패널)

1. `CreatePanel` 자식 우클릭 → `Create Empty` → 이름 `PanelBox`.
2. RectTransform: Anchor = **middle-center**, Pos X = `0`, Pos Y = `0`, Width = `700`, Height = `820`.
3. `Add Component` → `Image`:
   - **Source Image**: Project 창에서 다음 스프라이트를 드래그:
     `Assets/Imported/Modern UI Pack/Textures/Border/Rounded/256px/Rounded Filled 256px.png`
   - **Image Type**: `Sliced`
   - **Pixels Per Unit Multiplier**: `10`
   - **Color**: `#2A2A3E`, Alpha `255`
   > ℹ️ Rounded Filled 스프라이트 + Sliced = 라운드 모서리 패널. Pixels Per Unit 10 으로 모서리 크기 조절.
4. `Add Component` → `Vertical Layout Group`:
   - Padding: Top `30`, Bottom `30`, Left `40`, Right `40`
   - Spacing: `16`
   - Child Alignment: `Upper Center`
   - Child Force Expand Width **체크**, Height **체크 해제**
   - Child Controls Size Width **체크**, Height **체크 해제**

---

#### 3.8d PanelTitle (PanelBox 자식 ①)

1. `PanelBox` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `PanelTitle`.
2. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `새 멘토링 그룹`
   - Font Size: `36`
   - Alignment: 가로 `Center`, 세로 `Middle`
   - Color: White
3. `Add Component` → `Layout Element`: Preferred Height = `50`.

#### 3.8e MentorSectionTitle (PanelBox 자식 ②)

1. `PanelBox` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `MentorSectionTitle`.
2. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `멘토 선택`
   - Font Size: `28`
   - Alignment: 가로 `Left`, 세로 `Middle`
   - Color: `#AAAAAA`
3. `Add Component` → `Layout Element`: Preferred Height = `40`.

---

#### 3.8f MentorDropdown (PanelBox 자식 ③, MUIP Dropdown)

1. Project 창에서 다음 prefab 찾기:
   `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab`
2. 이 prefab 을 Hierarchy 의 `PanelBox` 위로 **드래그** (자식으로 들어감).
3. 생성된 인스턴스:
   - 이름: `MentorDropdown`
   - Inspector 의 **`Custom Dropdown` 컴포넌트**:
     - **`Init At Start`** 체크박스 → **체크 해제 (false)**. ⚠️ 중요 — 컨트롤러가 Start 에서 직접 채우므로 prefab 의 자동 초기화 끔.
     - **`Enable Icon`** → **체크 해제 (false)**. 이름만 표시.
     - 나머지 설정 기본값 그대로.
4. `Add Component` → `Layout Element`: Preferred Height = `65`.
5. **드롭다운 리스트 겹침 방지** (⚠️ 필수):
   - `Add Component` → `Canvas`:
     - `Override Sorting` **체크**
     - `Sorting Order` = `10`
   - `Add Component` → `Graphic Raycaster` (기본값 그대로).
   > ℹ️ Sub-Canvas + Sorting Order 10 으로 설정하면 드롭다운 리스트가 PanelBox 의 다른 UI 요소들(MenteeSectionTitle, MenteeScrollView 등) 위에 렌더링됨.

> ℹ️ `MentoringController.mentorDropdown` 필드 타입은 `CustomDropdown`. 아래 Step 4.2 와이어링 시 이 인스턴스 루트를 드래그하면 자동 바인딩.

---

#### 3.8g MenteeSectionTitle (PanelBox 자식 ④)

1. `PanelBox` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `MenteeSectionTitle`.
2. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `멘티 선택 (1~3명)`
   - Font Size: `28`
   - Alignment: 가로 `Left`, 세로 `Middle`
   - Color: `#AAAAAA`
3. `Add Component` → `Layout Element`: Preferred Height = `40`.

---

#### 3.8h MenteeScrollView (PanelBox 자식 ⑤)

1. `PanelBox` 자식 우클릭 → `UI → Scroll View` → 이름 `MenteeScrollView`.
2. **`Scroll Rect` 컴포넌트**: Horizontal **체크 해제**, Vertical **체크**.
3. `MenteeScrollView/Scrollbar Horizontal` → **Delete**.
4. `Add Component` → `Layout Element`:
   - Preferred Height: `300`
   - Min Height: `150`

`MenteeScrollView/Viewport/Content` 선택:

1. `Add Component` → `Vertical Layout Group`:
   - Spacing: `4`
   - Child Force Expand Width **체크**, Height **체크 해제**
   - Child Controls Size Width **체크 해제**, Height **체크 해제**
2. `Add Component` → `Content Size Fitter`:
   - Horizontal Fit: `Unconstrained`
   - Vertical Fit: `Preferred Size`

---

#### 3.8i ButtonRow (PanelBox 자식 ⑥)

1. `PanelBox` 자식 우클릭 → `Create Empty` → 이름 `ButtonRow`.
2. `Add Component` → `Horizontal Layout Group`:
   - Spacing: `20`
   - Child Alignment: `Middle Center`
   - Child Force Expand Width **체크 해제**, Height **체크 해제**
3. `Add Component` → `Layout Element`: Preferred Height = `65`.

**CancelCreateButton** (`ButtonRow` 자식, MUIP Button):

1. `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` → `ButtonRow` 위로 드래그.
2. 인스턴스:
   - 이름: `CancelCreateButton`
   - RectTransform: Width = `220`, Height = `60`
   - **`Button Manager` 컴포넌트**: `Button Text` = `취소`

**ConfirmCreateButton** (`ButtonRow` 자식, MUIP Button):

1. `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` → `ButtonRow` 위로 드래그.
2. 인스턴스:
   - 이름: `ConfirmCreateButton`
   - RectTransform: Width = `220`, Height = `60`
   - **`Button Manager` 컴포넌트**: `Button Text` = `그룹 생성`

> ℹ️ CancelCreateButton 과 ConfirmCreateButton 의 onClick 은 MentoringController.Start() 에서 자동 연결됨.

---

## Step 4 — MentoringController 컴포넌트 추가 및 와이어링 ⭐

### 4.1 MentoringRoot 생성

1. Hierarchy 루트 우클릭 → `Create Empty` → 이름 `MentoringRoot`.
2. `MentoringRoot` 선택 → `Add Component` → 검색창에 `Mentoring Controller` → 추가.
   - Inspector 에 `Mentoring Controller` 컴포넌트 나타남. 9 개 필드가 `None`. **Step 4.2 에서 채울 것.**

### 4.2 Inspector 와이어링 — SerializeField 전체 채우기 ⭐

`MentoringRoot` 선택 → `Mentoring Controller` 컴포넌트:

| 컨트롤러 필드 | 드래그할 대상 | 바인딩되는 컴포넌트 타입 |
|---|---|---|
| `Group List Parent` | `Canvas/GroupScrollView/Viewport/Content` | Transform |
| `Group Item Prefab` | Project: `Assets/Imported/FMLite UI/Prefabs/MentoringGroupItem.prefab` | GameObject |
| `Create Panel` | `Canvas/CreatePanel` | GameObject |
| `Mentor Dropdown` | `Canvas/CreatePanel/PanelBox/MentorDropdown` | **CustomDropdown** 자동 바인딩 |
| `Mentee Toggle Parent` | `Canvas/CreatePanel/PanelBox/MenteeScrollView/Viewport/Content` | Transform |
| `Mentee Toggle Prefab` | Project: `Assets/Imported/FMLite UI/Prefabs/MenteeToggleItem.prefab` | GameObject |
| `Confirm Create Button` | `Canvas/CreatePanel/PanelBox/ButtonRow/ConfirmCreateButton` | **ButtonManager** 자동 바인딩 |
| `Cancel Create Button` | `Canvas/CreatePanel/PanelBox/ButtonRow/CancelCreateButton` | **ButtonManager** 자동 바인딩 |
| `Create Group Button` | `Canvas/CreateGroupButton` | **ButtonManager** 자동 바인딩 |
| `Back Button` | `Canvas/Header/BackButton` | **ButtonManager** 자동 바인딩 |

⚠️ 각 항목의 **루트 GameObject 자체**를 드래그. Unity 가 컴포넌트 타입을 자동 인식해 바인딩.
⚠️ 10 개 필드 모두 `None` 이 없어야 함.

### 4.3 버튼 OnClick 연결 — 불필요

> **이 씬의 버튼 4 개 (`BackButton`, `CreateGroupButton`, `ConfirmCreateButton`, `CancelCreateButton`) 의 클릭 이벤트는 `MentoringController.Start()` 에서 코드로 자동 등록된다.**
> Inspector 에서 별도로 `Button Manager → Click Event` 또는 `Button → On Click ()` 를 연결할 필요 없음.

### 4.4 Build Settings 등록

1. 현재 MentoringScene 이 열려 있는 상태에서 `File → Build Settings`.
2. `Add Open Scenes` 클릭.
3. `Scenes In Build` 목록에 `Scenes/MentoringScene` 확인.
4. 창 닫기.

### 4.5 씬 저장

`Ctrl+S`.

---

## Step 5 — DashboardScene 에 "멘토링" 버튼 추가

1. `Assets/_Project/Scenes/DashboardScene.unity` 열기.
2. 기존 버튼들이 있는 부모 컨테이너 찾기 (`TacticButton`, `YouthButton` 등 사이드 메뉴 버튼들이 있는 곳).
3. `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` → 해당 컨테이너 아래 드래그.
4. 인스턴스:
   - 이름: `MentoringButton`
   - **`Button Manager` 컴포넌트**: `Button Text` = `멘토링`
5. `MentoringButton` 선택 → Inspector 의 **`Button Manager` 컴포넌트** → `Click Event` 항목:
   - `+` 버튼 클릭 → `DashboardController` 가 붙어있는 루트 오브젝트 드래그 → 함수: `DashboardController.OnMentoringClicked`.
6. `Ctrl+S` 로 DashboardScene 저장.

> ℹ️ `DashboardController.OnMentoringClicked` 메서드는 이미 코드에 작성됨 (MentoringScene 로 로드).
> 없다고 에러가 나면 `.cs` 수정 없이 멈추고 보고.

---

## 검증 체크리스트

### Prefabs

- [ ] `Assets/Imported/FMLite UI/Prefabs/MentoringGroupItem.prefab` 존재.
- [ ] MentoringGroupItem.prefab 내: `MentorLabel` + `MenteesLabel` + `DissolveButton` 자식 3 개.
- [ ] `Mentoring Group Item` 컴포넌트 3 개 필드 모두 채워짐 (None 없음).
- [ ] `DissolveButton`: `Button Manager` + `Button` 컴포넌트 존재 (Inspector 확인).
- [ ] `Assets/Imported/FMLite UI/Prefabs/MenteeToggleItem.prefab` 존재.
- [ ] MenteeToggleItem.prefab 내: `Toggle` 컴포넌트 + `CustomToggle` 컴포넌트 + `NameLabel` (TMP_Text).
- [ ] `Toggle.Graphic` = `Background/Checkmark` 이미지.

### MentoringScene

- [ ] `Assets/Scenes/MentoringScene.unity` 존재.
- [ ] `Build Settings → Scenes In Build` 에 포함.
- [ ] `MentoringRoot` 에 `Mentoring Controller` 컴포넌트 있음.
- [ ] `Mentoring Controller` 10 개 필드 모두 채워짐 (None 없음).
- [ ] `Canvas/CreatePanel` 의 초기 Active = **false** (Inspector 체크박스 해제).
- [ ] `Canvas/CreatePanel/PanelBox/MentorDropdown` 에 **`Custom Dropdown`** 컴포넌트 있음.
  - `Init At Start` = false, `Enable Icon` = false 확인.
- [ ] `MentorDropdown` 에 `Canvas` (Override Sorting = true, Sorting Order = 10) + `Graphic Raycaster` 추가됨.
- [ ] `Canvas/CreatePanel/PanelBox/MenteeScrollView/Viewport/Content` 에 `Vertical Layout Group` + `Content Size Fitter` 있음.
- [ ] `Canvas/GroupScrollView/Viewport/Content` 에 `Vertical Layout Group` + `Content Size Fitter` 있음.
- [ ] Header 의 `BackButton`: `Button Manager` 컴포넌트 있음 (`ButtonManagerBasic` 이면 잘못된 prefab — 재배치 필요).
- [ ] `CreateGroupButton`, `ConfirmCreateButton`, `CancelCreateButton` 모두 `Button Manager` 컴포넌트 있음.

### DashboardScene

- [ ] `MentoringButton` 오브젝트 존재.
- [ ] `MentoringButton` 의 `Button Manager → Click Event` → `DashboardController.OnMentoringClicked` 연결됨.

### Console

- [ ] 컴파일 에러 0.

---

## Hierarchy 최종 구조 (참고)

```
MentoringScene
├── Main Camera
├── EventSystem
├── MentoringRoot          (← Mentoring Controller 컴포넌트)
└── Canvas                 (Overlay, 1920×1080, Match 0.5)
    ├── Background         (Image, #1A1A2E, stretch)
    ├── Header             (top-stretch, H=120)
    │   ├── BackButton     (ButtonManager 인스턴스, middle-left)
    │   └── TitleText      (TMP_Text, middle-center)
    ├── GroupScrollView    (top-center, 960×640)
    │   └── Viewport
    │       └── Content    (VLG + CSF)
    ├── CreateGroupButton  (ButtonManager 인스턴스, bottom-center)
    └── CreatePanel        (stretch, INACTIVE 초기)
        ├── DimBackground  (Image, #000000 α180)
        └── PanelBox       (middle-center, 700×820, Image + VLG)
            ├── PanelTitle          (TMP_Text, H=50)
            ├── MentorSectionTitle  (TMP_Text, H=40)
            ├── MentorDropdown      (CustomDropdown 인스턴스, H=65)
            ├── MenteeSectionTitle  (TMP_Text, H=40)
            ├── MenteeScrollView    (ScrollView, H=300)
            │   └── Viewport
            │       └── Content    (VLG + CSF)
            └── ButtonRow          (HLG, H=65)
                ├── CancelCreateButton  (ButtonManager 인스턴스, W=220)
                └── ConfirmCreateButton (ButtonManager 인스턴스, W=220)
```

---

## 문제 발생 시

1. **즉시 멈춤.** `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 에러 메시지 + 어느 Step 에서 막혔는지 보고.
3. 사용자가 Claude Code 에 전달해 다음 단계 결정.
