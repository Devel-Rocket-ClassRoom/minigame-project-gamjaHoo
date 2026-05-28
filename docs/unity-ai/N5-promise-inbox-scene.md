# N.5 PromiseInboxScene — Unity AI 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.  
> **`.cs` 파일 절대 손대지 말 것.** 컴파일 에러 → 즉시 멈추고 보고.

---

## 컨텍스트

- 프로젝트: FM-Lite (Unity 6, 축구 매니저)
- 목적: 활성 약속(Promise) 진행 현황 목록 화면
- 씬 파일: `Assets/Scenes/PromiseInboxScene.unity` (새로 생성)
- 컨트롤러: `Assets/_Project/Scripts/UI/PromiseInboxController.cs` (이미 작성됨 — 절대 수정 금지)
- UI 에셋: `Assets/Imported/Modern UI Pack/` (MUIP)

---

## 절대 규칙

1. `.cs` 파일 절대 손대지 말 것.
2. MUIP 원본 prefab 수정 금지.
3. 완료 후 `Ctrl+S` 저장.
4. **새 씬이므로 Main Camera 반드시 포함** (Tag: MainCamera, Background #1A1A2E).
5. EventSystem 중복 추가 금지 (MUIP Canvas.prefab 포함).
6. 커스텀 프리팹: `Assets/Imported/FMLite UI/Prefabs/` (폴더 없으면 먼저 생성).

---

## Step 1: 씬 생성

`File → New Scene` → Empty → `Save As` → `Assets/Scenes/PromiseInboxScene.unity`

---

## Step 2: Main Camera + Canvas 배치

1. `GameObject → Camera` → 이름 **Main Camera**, Tag: `MainCamera`, Background #1A1A2E
2. `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab` 을 Hierarchy 에 드래그
   - Screen Space — Overlay, 1080×1920, Scale With Screen Size, Match 0.5

---

## Step 3: Hierarchy 구조

```
Canvas
├── BgPanel              (Image, 전체화면, #1A1A2E)
├── TopBar               (상단 고정, Height 140)
│   ├── TitleText        (TMP_Text, "약속 현황")
│   └── BackButton       (MUIP 버튼)
├── PromiseScrollView    (ScrollRect, 나머지 영역)
│   ├── Viewport
│   │   └── PromiseContent (VerticalLayoutGroup + ContentSizeFitter)
└── EmptyLabel           (TMP_Text, 목록 빈 경우)
```

---

## Step 4: 각 오브젝트 상세 설정

### BgPanel

- RectTransform: stretch-stretch, offset 0
- Image: Color #1A1A2E, Alpha 255

### TopBar

- RectTransform: top-stretch, Height 140
- Image: Color #2A2A3E, Alpha 255
- **TitleText**
  - TMP_Text: 텍스트 `약속 현황`, fontSize 40, Bold, Color #FFFFFF, 중앙 정렬
  - RectTransform: Anchor middle-center, Width 700, Height 80
  - Font: `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`
- **BackButton**
  - `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Minimal.prefab` 드래그
  - RectTransform: Anchor middle-right, Pos X -100, Width 160, Height 80
  - ButtonManager: Button Text → `뒤로`
  - **Click Event**: `+` → PromiseInboxController 오브젝트 드래그 → `OnBackClicked` 선택
  - ⚠️ `Button.onClick` 아님. **ButtonManager 의 Click Event** 사용.

### PromiseScrollView

- RectTransform: stretch-stretch, Top 140, Bottom 0
- ScrollRect 컴포넌트:
  - Vertical: ✓, Horizontal: ✗
  - Movement Type: Clamped
- **Viewport**:
  - RectTransform: stretch-stretch, offset 0
  - Image 컴포넌트 (Alpha 0, Raycast Target ✓)
  - Mask 컴포넌트: Show Mask Graphic ✗
- **PromiseContent**:
  - VerticalLayoutGroup:
    - Child Alignment: Upper Left
    - Spacing: 10
    - Child Force Expand Width: ✓, Height: ✗
    - Child Controls Size Width: ✓, Height: ✗
    - Padding: Left 30, Right 30, Top 20, Bottom 20
  - ContentSizeFitter: Vertical Fit = **Preferred Size**
  - RectTransform: Anchor top-stretch, Height 0 (자동 조정)
- ScrollRect → Content 필드: PromiseContent, Viewport 필드: Viewport

### EmptyLabel

- RectTransform: Anchor middle-center, Width 700, Height 80
- TMP_Text: 텍스트 `진행 중인 약속이 없습니다`, fontSize 32, Color #CCCCCC, 중앙 정렬
- Font: NotoSansKR-VF SDF.asset
- **초기 비활성화** (컨트롤러가 런타임에 제어)

---

## Step 5: PromiseItem 프리팹 생성

Hierarchy 에 빈 오브젝트 생성 → 이름: **PromiseItem**

```
PromiseItem                  (RectTransform, Width 1020, Height 100)
├── BgImage                  (Image, Color #2A2A3E, RectTransform stretch-stretch)
│   └── Source Image:        Rounded Filled 256px.png, Sliced, Pixels Per Unit 10
└── InfoText                 (TMP_Text)
```

**InfoText 설정**:
- RectTransform: stretch-stretch, Padding Left/Right 20, Top/Bottom 8
- TMP_Text: fontSize 26, Color #FFFFFF, 멀티라인, Overflow: Overflow
- Font: `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`
- Alignment: Left-Middle

Source Image 경로:
`Assets/Imported/Modern UI Pack/Textures/Border/Rounded/256px/Rounded Filled 256px.png`

Project 창 `Assets/Imported/FMLite UI/Prefabs/` 로 드래그 → `PromiseItem.prefab` 생성  
Hierarchy 의 임시 PromiseItem 삭제.

---

## Step 6: PromiseInboxController 컴포넌트 추가 + 와이어링

Canvas 아래 빈 오브젝트 이름 `PromiseInboxController` → **PromiseInboxController** 컴포넌트 추가.

| 필드 | 드래그 대상 | 타입 |
|------|-------------|------|
| `listParent` | `Canvas/PromiseScrollView/Viewport/PromiseContent` | Transform |
| `itemPrefab` | `Assets/Imported/FMLite UI/Prefabs/PromiseItem.prefab` | GameObject |
| `emptyLabel` | `Canvas/EmptyLabel` | TMP_Text |

---

## Step 7: 씬 등록

`File → Build Settings` → `Add Open Scenes` → `Ctrl+S`

---

## 확인 체크리스트

- [ ] Main Camera (Tag: MainCamera, Background #1A1A2E)
- [ ] EventSystem 1개만 존재
- [ ] EmptyLabel 초기 비활성
- [ ] PromiseContent 에 VerticalLayoutGroup + ContentSizeFitter(Preferred Size) 있음
- [ ] ScrollRect.Content = PromiseContent, ScrollRect.Viewport = Viewport 연결
- [ ] PromiseItem.prefab 저장 완료 (TMP_Text 자식 포함)
- [ ] PromiseInboxController 3개 필드 모두 None 아님
- [ ] BackButton Click Event → PromiseInboxController.OnBackClicked() 연결
- [ ] Build Settings 등록 + Ctrl+S 저장

---

## 문제 발생 시

즉시 멈춤. `.cs` / MUIP 원본 prefab 수정 절대 금지. 에러 메시지 + Step 번호 보고.
