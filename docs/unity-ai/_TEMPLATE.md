# [씬/작업명] Unity AI 지시서 템플릿

> Unity AI Assistant 에 그대로 전달할 작업 지시서.
> **`.cs` 파일은 절대 손대지 말 것.** 코드는 Claude Code 가 담당.

---

## 컨텍스트 (Unity AI 가 알아야 할 것)

- 프로젝트: FM-Lite (Unity 6, 축구 매니저 게임)
- **UI 에셋**: `Assets/Imported/Modern UI Pack/` (MUIP)
- 작업 대상: [새 씬 생성 / 기존 씬 수정 여부 명시]
- 컨트롤러: 이미 작성됨 (`Assets/_Project/Scripts/UI/XxxController.cs`)
- **SerializeField 타입 주의사항**:
  - `TMP_Text` → TextMeshPro Text (UI)
  - `Button` → Unity 기본 Button
  - `ButtonManager` → MUIP 컴포넌트 (Unity 기본 Button 아님)
  - `CustomDropdown` → MUIP 컴포넌트
  - `Transform` → 빈 GameObject 의 Transform
  - `ScrollRect` → Unity 기본 ScrollRect
  - `GameBalanceSO` → ScriptableObject (`Assets/_Project/Data/Resources/Balance/GameBalance.asset`)

---

## 절대 규칙

1. **`.cs` 파일 절대 금지.** 컴파일 에러가 나도 .cs 손대지 말고 즉시 멈춰서 보고.
2. **`Assets/Imported/Modern UI Pack/` 원본 prefab 수정 금지.** 씬에 인스턴스를 드래그해 배치만.
3. 모든 작업 완료 후 **`Ctrl+S`** 저장.
4. ⚠️ **EventSystem 중복 금지**: MUIP Canvas.prefab 에 포함되어 있음. 기존 씬에서는 이미 있으니 추가 금지.
5. ⚠️ **Main Camera**: 새 씬이면 반드시 포함 (`GameObject → Camera`).
6. **커스텀 프리팹 저장 위치**: `Assets/Imported/FMLite UI/Prefabs/` (없으면 폴더 먼저 생성).

---

## MUIP 핵심 컴포넌트 참조

### 버튼 프리팹 경로

| 종류 | 경로 |
|------|------|
| 기본 외곽선 버튼 | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` |
| 기본 채색 버튼 | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic/Standard.prefab` |
| 작은 버튼 | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Minimal.prefab` |

### ButtonManager 와이어링 방법

1. MUIP 버튼 프리팹을 Hierarchy 에 드래그 → 인스턴스 생성
2. 인스턴스의 **`Button Manager`** 컴포넌트 확인
3. `Button Text` 필드 → 버튼 레이블 텍스트 입력
4. `Click Event` 섹션 → `+` 클릭 → 컨트롤러 오브젝트 드래그 → 함수 선택

⚠️ `Button.onClick` (Unity 기본) 아님. 반드시 **`ButtonManager.Click Event`** 사용.

### 드롭다운 (CustomDropdown)

- 프리팹: `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab`
- 컴포넌트: **`CustomDropdown`** (Unity 기본 `Dropdown` 아님)
- **`Init At Start`**: 런타임에 코드로 채우는 경우 **체크 해제**
- **`Enable Icon`**: 아이콘 없으면 **체크 해제**
- 항목 추가: 코드에서 `customDropdown.CreateNewItemFast("항목명", false)` 호출

### Canvas 프리팹 (새 씬용)

`Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab`
→ EventSystem + Canvas 포함. 새 씬에서 이것만 드래그하면 기본 설정 완료.
→ Reference Resolution: 1080×1920, Scale Mode: Scale With Screen Size

### 라운드 모서리 패널 만드는 법

1. `Create Empty` → `Image` 컴포넌트 추가
2. `Source Image`: `Assets/Imported/Modern UI Pack/Textures/Border/Rounded/256px/Rounded Filled 256px.png`
3. `Image Type`: `Sliced`, `Pixels Per Unit Multiplier`: `10`
4. Color: 원하는 색 (#2A2A3E 어두운 카드, #FFFFFF 흰 카드)

---

## Canvas / RectTransform 설정 표준

| 항목 | 값 |
|------|-----|
| Canvas Render Mode | Screen Space — Overlay |
| Reference Resolution | 1080 × 1920 |
| UI Scale Mode | Scale With Screen Size |
| Match | 0.5 (Width-Height 중간) |

### 자주 쓰는 RectTransform 앵커 패턴

| 용도 | 앵커 설정 방법 |
|------|----------------|
| 전체 화면 꽉 채우기 | Alt 누른 채 stretch-stretch 클릭 → offset 전부 0 |
| 중앙 고정 (모달 등) | Alt 누른 채 middle-center 클릭 → Width/Height 직접 입력 |
| 상단 고정 | Alt 누른 채 top-stretch 클릭 → Height 직접 입력 |
| 하단 고정 | Alt 누른 채 bottom-stretch 클릭 → Height 직접 입력 |

---

## 레이아웃 컴포넌트 표준

### VerticalLayoutGroup (스크롤 목록 컨테이너)

```
Child Alignment: Upper Left
Spacing: 8
Child Force Expand Width: ✓   Height: ✗
Child Controls Size Width: ✓   Height: ✗
```
+ ContentSizeFitter: Vertical Fit = **Preferred Size**

### HorizontalLayoutGroup (버튼 행 등)

```
Child Alignment: Middle Center
Spacing: 16
Child Force Expand Width: ✗   Height: ✗
Child Controls Size Width: ✗   Height: ✗
```

### ScrollRect + ScrollView 표준 구조

```
ScrollView (ScrollRect 컴포넌트)
├── Viewport (Mask + Image)
│   └── Content (VerticalLayoutGroup + ContentSizeFitter)
│       └── [아이템 프리팹들 Instantiate 위치]
└── Scrollbar Vertical (선택)
```
- ScrollRect.Content → Content 오브젝트 드래그
- ScrollRect.Viewport → Viewport 오브젝트 드래그
- Viewport 에 `Mask` 컴포넌트 + `Image` 컴포넌트 (투명 스프라이트)

---

## 폰트 표준

- 모든 TMP_Text: 폰트 에셋 `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`
- 폰트 지정 방법: TMP_Text 컴포넌트 → `Font Asset` 슬롯 → 해당 asset 드래그
- "Import TMP Essentials" 다이얼로그 뜨면 클릭.

---

## 색상 팔레트 표준

| 용도 | Hex |
|------|-----|
| 패널 배경 (어두운 카드) | #2A2A3E |
| 딤 배경 | #000000 Alpha 180 |
| 기본 텍스트 | #FFFFFF |
| 보조 텍스트 | #CCCCCC |
| 강조 색 (버튼) | #4A90D9 |
| 경고 색 | #E87040 |
| 성공 색 | #4CAF50 |
| 배경 (씬 전체) | #1A1A2E |

---

## 씬 필수 오브젝트 체크리스트 (새 씬)

- [ ] **Canvas** (MUIP Canvas.prefab 드래그 또는 수동: Canvas + EventSystem + GraphicRaycaster)
- [ ] **Main Camera** (`GameObject → Camera` — Tag: `MainCamera`)
- [ ] **EventSystem** (MUIP Canvas.prefab 에 포함 / 직접 추가 시: GameObject → UI → EventSystem)
- [ ] 씬 배경색 지정 (Main Camera → Background Color)

---

## 컴포넌트 와이어링 표 형식 (각 지시서에서 사용)

| 컨트롤러 필드명 | 드래그 대상 | 타입 |
|-----------------|-------------|------|
| `xxxText` | `Canvas/.../XxxText` 오브젝트 | TMP_Text |
| `xxxButton` | `Canvas/.../XxxButton` 오브젝트 | ButtonManager |
| `listParent` | `Canvas/.../ListContent` 오브젝트 | Transform |
| `itemPrefab` | `Assets/Imported/FMLite UI/Prefabs/XxxItem.prefab` | GameObject |
| `balance` | `Assets/_Project/Data/Resources/Balance/GameBalance.asset` | GameBalanceSO |

---

## 아이템 프리팹 생성 표준

1. Hierarchy 에서 임시 오브젝트 생성 → 구성 완료
2. Project 창 `Assets/Imported/FMLite UI/Prefabs/` 폴더로 드래그 → .prefab 생성
3. Hierarchy 의 임시 오브젝트 삭제
4. 컨트롤러 `itemPrefab` 슬롯에 방금 만든 .prefab 드래그

---

## EditorBuildSettings 등록 방법

`File → Build Settings → Scenes In Build` 패널:
1. Project 창에서 씬 파일 드래그 → 목록에 추가
2. 또는 해당 씬을 열고 `Add Open Scenes` 버튼 클릭
3. 저장 (`Ctrl+S` 또는 `File → Save`)

---

## Localization 재시드

새 키를 코드에서 사용하면 반드시:
`FM-Lite → Seed → Generate V0.5 Localization` 실행

---

## 문제 발생 시

1. **즉시 멈춤.** `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 에러 메시지 + 어느 Step 에서 막혔는지 보고.
3. 사용자가 Claude Code 에 전달해 다음 단계 결정.
