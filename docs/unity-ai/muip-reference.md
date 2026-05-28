# Modern UI Pack (MUIP) — 프로젝트 전용 참조

> FM-Lite 프로젝트에서 실제 사용하는 컴포넌트 위주 요약.  
> 전체 공식 문서: https://docs.michsky.com/docs/modern-ui-pack/

---

## ⚠️ 핵심 규칙 (Unity AI 전달 필수)

1. **`.cs` 파일 절대 손대지 말 것** — 컴파일 에러가 나도 멈추고 보고.
2. **MUIP 원본 prefab 수정 금지** — 씬에 드래그해 인스턴스로만 사용.
3. **`ButtonManager.Click Event`** 사용 — Unity 기본 `Button.onClick` (Inspector 와이어링) 아님.  
   _단, C# 코드에서 `AddListener` 로 연결하는 경우 Button 컴포넌트 직접 할당만 하면 됨._
4. **EventSystem 중복 금지** — MUIP Canvas.prefab 에 포함됨. 기존 씬엔 추가 금지.
5. **Main Camera 빠짐 없이** — 새 씬엔 반드시 포함.

---

## 프리팹 경로 (자주 사용)

| 용도 | 경로 |
|------|------|
| Canvas (새 씬용, EventSystem 포함) | `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab` |
| 기본 외곽선 버튼 | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` |
| 기본 채색 버튼 | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic/Standard.prefab` |
| 작은 버튼 (Minimal) | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Minimal.prefab` |
| 드롭다운 | `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab` |
| 커스텀 프리팹 저장 위치 | `Assets/Imported/FMLite UI/Prefabs/` |

---

## 1. ButtonManager

### 타입 구분
- **`ButtonManager`** — MUIP 버튼 컴포넌트. `[SerializeField] ButtonManager myBtn;` 에 할당.
- **`Button`** (Unity 기본) — MUIP 버튼 프리팹 인스턴스에도 `Button` 컴포넌트가 존재. `[SerializeField] Button myBtn;` 에 드래그하면 자동 할당됨.

### Inspector 와이어링
1. 프리팹을 Hierarchy 에 드래그
2. **`Button Manager`** 컴포넌트 → `Button Text` 필드에 레이블 입력
3. **`Click Event`** 섹션 → `+` → 컨트롤러 GameObject 드래그 → 함수 선택

### 스크립팅 API (v5.5+)

```csharp
[SerializeField] ButtonManager myButton;
myButton.SetText("클릭");
myButton.Interactable(false);   // 비활성화
myButton.onClick.AddListener(OnClick);
```

### 주의

- Inspector Click Event = `ButtonManager.onClickEvent`
- 코드에서 `myButton.onClick.AddListener(...)` 는 `Button.onClick` (다른 이벤트)
- 두 개 혼용 가능하지만 프로젝트에서는 코드 쪽은 `Button` 타입으로 선언, Inspector 연결은 `ButtonManager.Click Event` 사용

---

## 2. CustomDropdown

### Inspector 설정 (런타임 코드로 채울 때 필수)

| 항목 | 값 |
|------|----|
| **Init At Start** | **체크 해제** (코드에서 채울 때) |
| **Enable Icon** | **체크 해제** (아이콘 없을 때) |

### 스크립팅 API

```csharp
[SerializeField] CustomDropdown myDropdown;
myDropdown.CreateNewItem("항목1", false);
myDropdown.CreateNewItem("항목2", false);
myDropdown.SetupDropdown();   // 반드시 호출

myDropdown.ChangeDropdownInfo(0);   // 선택 변경
myDropdown.onValueChanged.AddListener((int i) => { });
```

### 주의
- `Init At Start` = false 이면 코드에서 `SetupDropdown()` 호출 전까지 드롭다운이 비어있음
- 저장 기능 사용 시 인스턴스마다 고유 **Dropdown Tag** 필요

---

## 3. ModalWindowManager

```csharp
[SerializeField] ModalWindowManager myModal;
myModal.titleText = "확인";
myModal.descriptionText = "정말로 하시겠습니까?";
myModal.UpdateUI();
myModal.Open();
myModal.onConfirm.AddListener(OnConfirmed);
myModal.onCancel.AddListener(OnCancelled);
```

- 코드로 내용 세팅 시 **`useCustomContent = true`** 체크 필수 (체크 안 하면 UI Manager가 덮어씀)

---

## 4. 자주 쓰는 표준 패턴

### Canvas (새 씬)

```
MUIP Canvas.prefab 드래그
→ Screen Space — Overlay
→ Reference Resolution: 1080 × 1920
→ Scale With Screen Size, Match 0.5
(EventSystem 포함됨 — 별도 추가 금지)
```

### ScrollRect 구조

```
ScrollView (ScrollRect 컴포넌트)
├── Viewport (Mask + Image)
│   └── Content (VerticalLayoutGroup + ContentSizeFitter: Vertical = Preferred Size)
└── Scrollbar Vertical (선택)
```

ScrollRect Inspector:
- `Content` 필드 → Content 오브젝트
- `Viewport` 필드 → Viewport 오브젝트

### VerticalLayoutGroup (스크롤 목록 Content 용)

```
Child Alignment: Upper Left
Spacing: 8
Child Force Expand Width: ✓  Height: ✗
Child Controls Size Width: ✓  Height: ✗
ContentSizeFitter: Vertical Fit = Preferred Size
```

### 라운드 모서리 패널

1. Create Empty → Image 컴포넌트 추가
2. Source Image: `Assets/Imported/Modern UI Pack/Textures/Border/Rounded/256px/Rounded Filled 256px.png`
3. Image Type: Sliced, Pixels Per Unit Multiplier: 10
4. Color: `#2A2A3E` (어두운 카드) 또는 원하는 색

---

## 5. 폰트 / 색상 표준

### 폰트
모든 TMP_Text: `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`

### 색상

| 용도 | Hex |
|------|-----|
| 씬 배경 | #1A1A2E |
| 패널 배경 | #2A2A3E |
| 기본 텍스트 | #FFFFFF |
| 보조 텍스트 | #CCCCCC |
| 강조 (버튼) | #4A90D9 |
| 경고 | #E87040 |
| 성공 | #4CAF50 |
| 딤 배경 | #000000 Alpha 180 |

---

## 6. RectTransform 앵커

| 용도 | 방법 |
|------|------|
| 전체 화면 | Alt 누른 채 stretch-stretch → offset 0 |
| 중앙 고정 (모달) | Alt 누른 채 middle-center → W/H 직접 입력 |
| 상단 고정 | Alt 누른 채 top-stretch → Height 입력 |
| 하단 고정 | Alt 누른 채 bottom-stretch → Height 입력 |

---

## 7. 자주 발생하는 문제

| 증상 | 원인 / 해결 |
|------|-------------|
| SetText / SetIcon 효과 없음 | `useCustomContent = true` 체크 확인 |
| Dropdown 선택 시 onValueChanged 안 됨 | `SetupDropdown()` 호출 여부 확인 |
| 씬 첫 실행 시 NullRef | Enter Play Mode Options → "Reload Scene" 활성화 |
| EventSystem 중복 경고 | MUIP Canvas.prefab 이미 EventSystem 포함 — 중복 삭제 |
| Button 클릭 이벤트 안 됨 | Click Event 는 `ButtonManager` 컴포넌트 안에 있음 (`Button.onClick` 아님) |
