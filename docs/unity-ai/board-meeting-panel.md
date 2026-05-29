# DashboardScene — BoardMeetingPanel 추가

> Unity AI Assistant 에 그대로 전달할 작업 지시서.
> 이 문서의 모든 단계는 **에디터 안 작업**. **`.cs` 파일은 절대 손대지 말 것.**

---

## 컨텍스트 (Unity AI 가 알아야 할 것)

- 프로젝트: FM-Lite (Unity 6, 축구 매니저 게임).
- **UI 에셋**: `Assets/Imported/Modern UI Pack/` (MUIP). 버튼 모두 이 에셋 프리팹 사용.
- 작업 대상: **기존 DashboardScene** 에 보드 약속 모달 패널을 추가하는 것.
  - 새 씬 생성 아님. 기존 씬에 Panel 하나 추가.
- 컨트롤러는 이미 작성되어 있다:
  - `Assets/_Project/Scripts/UI/BoardMeetingController.cs` — SerializeField 2 개
  - `Assets/_Project/Scripts/UI/DashboardController.cs` — `boardMeetingPanel` SerializeField 1 개 추가됨
- **SerializeField 타입 주의사항**:
  - `descriptionText` → `TMP_Text` (Unity 기본)
  - `balance` → `GameBalanceSO` (ScriptableObject)
  - `AcceptButton`, `RejectButton` → **`ButtonManager`** (MUIP 컴포넌트). Unity 기본 Button 아님.
- **버튼 OnClick 이벤트**: `OnAcceptClicked`, `OnRejectClicked` 는 **Inspector 에서 수동 연결 필요** (코드 자동 연결 아님).

---

## 절대 규칙

1. **`.cs` 파일 절대 금지.** 컴파일 에러가 나도 .cs 손대지 말고 즉시 멈춰서 보고.
2. **`Assets/Imported/Modern UI Pack/` 원본 prefab 수정 금지.** 씬에 인스턴스를 드래그해 배치만.
3. 모든 작업 완료 후 `Ctrl+S` 저장.
4. ⚠️ **EventSystem**: DashboardScene 은 기존 씬이므로 EventSystem 이 이미 있음. 중복 추가 금지.

---

## Step 1 — DashboardScene 열기

1. Project 창에서 `Assets/Scenes/DashboardScene.unity` 더블클릭.
2. Hierarchy 에서 기존 Canvas 오브젝트 확인 (이름이 `Canvas` 인 것).

---

## Step 2 — BoardMeetingPanel 루트 생성

1. Hierarchy 에서 `Canvas` 우클릭 → `Create Empty` → 이름: `BoardMeetingPanel`.
2. `BoardMeetingPanel` 선택 → Inspector RectTransform:
   - Anchor: **stretch-stretch** (Alt 누른 채 클릭 → offset 전부 0 자동 설정).
   - Left = `0`, Right = `0`, Top = `0`, Bottom = `0`.
3. ⚠️ **GameObject 비활성**: Inspector 좌상단 오브젝트 이름 왼쪽 체크박스 **해제** (Active = false).
   - 코드에서 `Awake()` 시 자동으로 비활성화하지만, 초기 상태도 false 로 설정.

---

## Step 3 — DimBackground (클릭 차단 어두운 배경)

1. `BoardMeetingPanel` 자식 우클릭 → `UI → Image` → 이름: `DimBackground`.
2. RectTransform: Anchor = **stretch-stretch** (Alt 클릭 → offset 전부 0).
3. `Image` 컴포넌트:
   - **Color**: `#000000`, **Alpha** = `180`
   - **Raycast Target**: **체크** (클릭이 배경으로 통과되지 않도록).

---

## Step 4 — PanelBox (라운드 모서리 흰색 카드 패널)

1. `BoardMeetingPanel` 자식 우클릭 → `Create Empty` → 이름: `PanelBox`.
2. RectTransform:
   - Anchor: **middle-center** (Alt 누른 채 클릭).
   - Pos X = `0`, Pos Y = `0`
   - Width = `560`, Height = `300`
3. `Add Component` → `Image`:
   - **Source Image**: Project 창에서 다음 스프라이트 드래그:
     `Assets/Imported/Modern UI Pack/Textures/Border/Rounded/256px/Rounded Filled 256px.png`
   - **Image Type**: `Sliced`
   - **Pixels Per Unit Multiplier**: `10`
   - **Color**: `#2A2A3E`, Alpha `255`
   > ℹ️ Rounded Filled + Sliced + PPU 10 = 라운드 모서리 패널.
4. `Add Component` → `Vertical Layout Group`:
   - Padding: Top = `28`, Bottom = `28`, Left = `36`, Right = `36`
   - Spacing: `18`
   - Child Alignment: `Upper Center`
   - Child Force Expand Width **체크**, Height **체크 해제**
   - Child Controls Size Width **체크**, Height **체크 해제**

---

## Step 5 — TitleText (PanelBox 자식 ①)

1. `PanelBox` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름: `TitleText`.
   - "Import TMP Essentials" 다이얼로그가 뜨면 `Import TMP Essentials` 클릭.
2. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `이사회 요구사항`
   - Font Size: `32`
   - Alignment: 가로 `Center`, 세로 `Middle`
   - Color: White (`#FFFFFF`)
3. `Add Component` → `Layout Element`:
   - Preferred Height: `44`

---

## Step 6 — DescriptionText (PanelBox 자식 ②)

1. `PanelBox` 자식 우클릭 → `UI → Text - TextMeshPro` → 이름: `DescriptionText`.
2. `TextMeshPro - Text (UI)` 컴포넌트:
   - Text: `이사회에서 GK 포지션 선수 영입을 요구합니다.\n수락하면 여름 이적 시장 종료까지 이행해야 합니다.`
   - Font Size: `24`
   - Alignment: 가로 `Center`, 세로 `Middle`
   - Color: Light Gray (`#CCCCCC`)
   - **Overflow**: `Overflow` (텍스트 잘림 방지)
   - **Word Wrapping**: **체크**
3. `Add Component` → `Layout Element`:
   - Preferred Height: `80`
   - Flexible Height: `1`

---

## Step 7 — ButtonRow (PanelBox 자식 ③)

1. `PanelBox` 자식 우클릭 → `Create Empty` → 이름: `ButtonRow`.
2. `Add Component` → `Horizontal Layout Group`:
   - Spacing: `24`
   - Child Alignment: `Middle Center`
   - Child Force Expand Width **체크 해제**, Height **체크 해제**
   - Child Controls Size Width **체크 해제**, Height **체크 해제**
3. `Add Component` → `Layout Element`:
   - Preferred Height: `60`

---

### Step 7.1 — RejectButton (ButtonRow 자식, MUIP Button)

1. Project 창에서 다음 prefab 찾기:
   `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab`
   ⚠️ `Basic/Standard.prefab` 아님. 반드시 **`Basic - Outline`** 폴더 안의 `Standard.prefab`.
2. 해당 prefab 을 Hierarchy 의 `ButtonRow` 위로 **드래그** (자식으로 들어감).
3. 생성된 인스턴스:
   - 이름: `RejectButton`
   - RectTransform: Width = `200`, Height = `56`
   - **`Button Manager` 컴포넌트** → `Button Text` 필드: `거절`

---

### Step 7.2 — AcceptButton (ButtonRow 자식, MUIP Button)

1. `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` → `ButtonRow` 위로 드래그.
2. 생성된 인스턴스:
   - 이름: `AcceptButton`
   - RectTransform: Width = `200`, Height = `56`
   - **`Button Manager` 컴포넌트** → `Button Text` 필드: `수락`

---

## Step 8 — BoardMeetingController 컴포넌트 부착 및 와이어링 ⭐

### 8.1 컴포넌트 추가

1. Hierarchy 에서 `BoardMeetingPanel` 선택.
2. Inspector → `Add Component` → 검색창에 `Board Meeting Controller` → 추가.
   - `Board Meeting Controller` 컴포넌트가 나타나며 필드 2 개가 `None`. **Step 8.2 에서 채울 것.**

### 8.2 Inspector 와이어링 — SerializeField 전체 채우기 ⭐

`BoardMeetingPanel` 선택 → `Board Meeting Controller` 컴포넌트:

| 컨트롤러 필드 | 드래그할 대상 | 바인딩되는 타입 |
|---|---|---|
| `Description Text` | `Canvas/BoardMeetingPanel/PanelBox/DescriptionText` | TMP_Text 자동 바인딩 |
| `Balance` | Project: `Assets/_Project/Data/Resources/Balance/GameBalance.asset` | GameBalanceSO |

⚠️ 2 개 필드 모두 `None` 이 없어야 함.

---

## Step 9 — 버튼 OnClick 이벤트 연결 ⭐

> **이 패널의 버튼 2 개는 Inspector 에서 수동으로 연결해야 한다** (코드 자동 연결 아님).

### 9.1 RejectButton OnClick 연결

1. `ButtonRow/RejectButton` 선택.
2. Inspector 의 **`Button Manager` 컴포넌트** → `Click Event` 항목:
   - `+` 버튼 클릭 → 새 항목 생성.
   - 빈 오브젝트 슬롯에 Hierarchy 의 **`BoardMeetingPanel`** 드래그.
   - 함수 드롭다운: `BoardMeetingController` → `OnRejectClicked ()`.

### 9.2 AcceptButton OnClick 연결

1. `ButtonRow/AcceptButton` 선택.
2. Inspector 의 **`Button Manager` 컴포넌트** → `Click Event` 항목:
   - `+` 버튼 클릭 → 새 항목 생성.
   - 빈 오브젝트 슬롯에 Hierarchy 의 **`BoardMeetingPanel`** 드래그.
   - 함수 드롭다운: `BoardMeetingController` → `OnAcceptClicked ()`.

---

## Step 10 — DashboardController 와이어링 ⭐

1. Hierarchy 에서 `DashboardController` 컴포넌트가 붙어있는 오브젝트 찾기 (보통 이름이 `GameController` 또는 `DashboardController` 또는 Canvas 루트).
2. Inspector 의 **`Dashboard Controller` 컴포넌트** 에서 `Board Meeting Panel` 슬롯 찾기:
   - Hierarchy 의 `BoardMeetingPanel` 을 해당 슬롯으로 드래그.
   - **`BoardMeetingController`** 타입으로 자동 바인딩됨.

⚠️ `Board Meeting Panel` 슬롯이 `None` 이면 새 시즌 시작 시 보드 약속 팝업이 뜨지 않음.

---

## Step 11 — Localization 재시드

1. Unity 메뉴: `FM-Lite → Seed → Generate V0.5 Localization` 실행.
2. Console 에 에러 없으면 완료.

---

## Step 12 — 씬 저장

`Ctrl+S`.

---

## 검증 체크리스트

### BoardMeetingPanel 구조

- [ ] `Canvas/BoardMeetingPanel` 오브젝트 존재, 초기 Active = **false**.
- [ ] `BoardMeetingPanel/DimBackground`: `Image` 컴포넌트, Color `#000000` Alpha `180`, Raycast Target **체크**.
- [ ] `BoardMeetingPanel/PanelBox`: Width=`560`, Height=`300`, `Image` (Rounded Filled, Sliced, PPU=10, `#2A2A3E`), `Vertical Layout Group`.
- [ ] `PanelBox/TitleText`: TMP_Text, 텍스트=`이사회 요구사항`, FontSize=`32`.
- [ ] `PanelBox/DescriptionText`: TMP_Text, FontSize=`24`, Word Wrapping **체크**.
- [ ] `PanelBox/ButtonRow`: `Horizontal Layout Group`, Spacing=`24`.
- [ ] `ButtonRow/RejectButton`: `Button Manager` 컴포넌트, Button Text=`거절`, Width=`200`.
- [ ] `ButtonRow/AcceptButton`: `Button Manager` 컴포넌트, Button Text=`수락`, Width=`200`.

### 컴포넌트 와이어링

- [ ] `BoardMeetingPanel` 에 `Board Meeting Controller` 컴포넌트 있음.
- [ ] `Board Meeting Controller.Description Text` = `DescriptionText` 오브젝트 (None 아님).
- [ ] `Board Meeting Controller.Balance` = `GameBalance.asset` (None 아님).
- [ ] `RejectButton.Click Event` → `BoardMeetingController.OnRejectClicked()` 연결됨.
- [ ] `AcceptButton.Click Event` → `BoardMeetingController.OnAcceptClicked()` 연결됨.
- [ ] `DashboardController.Board Meeting Panel` = `BoardMeetingPanel` (None 아님).

### Console

- [ ] 컴파일 에러 0.
- [ ] `FM-Lite/Seed/Generate V0.5 Localization` 실행 완료.

---

## Hierarchy 최종 구조 (Canvas 안에 추가되는 부분만)

```
Canvas
└── BoardMeetingPanel          (← Board Meeting Controller 컴포넌트, INACTIVE 초기)
    ├── DimBackground          (Image, #000000 α180, stretch)
    └── PanelBox               (middle-center, 560×300, Image 라운드+VLG)
        ├── TitleText          (TMP_Text, "이사회 요구사항", FontSize=32)
        ├── DescriptionText    (TMP_Text, FontSize=24, Word Wrap)
        └── ButtonRow          (HLG, Spacing=24)
            ├── RejectButton   (ButtonManager, "거절", 200×56)
            └── AcceptButton   (ButtonManager, "수락", 200×56)
```

---

## 문제 발생 시

1. **즉시 멈춤.** `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 에러 메시지 + 어느 Step 에서 막혔는지 보고.
3. 사용자가 Claude Code 에 전달해 다음 단계 결정.
