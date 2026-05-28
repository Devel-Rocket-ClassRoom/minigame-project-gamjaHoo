# N.4 NegotiationScene — Unity AI 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.  
> **`.cs` 파일 절대 손대지 말 것.** 컴파일 에러 → 즉시 멈추고 보고.

---

## 컨텍스트

- 프로젝트: FM-Lite (Unity 6, 축구 매니저)
- 목적: 이적 협상 진행 화면 — CounterOffer 목록 + 수락/거절/재역제안 패널
- 씬 파일: `Assets/Scenes/NegotiationScene.unity` (새로 생성)
- 컨트롤러: `Assets/_Project/Scripts/UI/NegotiationController.cs` (이미 작성됨 — 절대 수정 금지)
- 오퍼 아이템: `Assets/_Project/Scripts/UI/NegotiationOfferItem.cs` (이미 작성됨 — 절대 수정 금지)
- UI 에셋: `Assets/Imported/Modern UI Pack/` (MUIP)

---

## 절대 규칙

1. `.cs` 파일 절대 손대지 말 것.
2. MUIP 원본 prefab 수정 금지.
3. 완료 후 `Ctrl+S` 저장.
4. **새 씬이므로 Main Camera 반드시 포함** (Tag: MainCamera, Background #1A1A2E).
5. EventSystem 중복 추가 금지 (MUIP Canvas.prefab 에 포함).
6. 커스텀 프리팹: `Assets/Imported/FMLite UI/Prefabs/` (폴더 없으면 먼저 생성).

---

## Step 1: 씬 생성

`File → New Scene` → Empty → `Save As` → `Assets/Scenes/NegotiationScene.unity`

---

## Step 2: Main Camera + Canvas 배치

1. `GameObject → Camera` → 이름 **Main Camera**, Tag: `MainCamera`, Background #1A1A2E
2. `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab` 을 Hierarchy 에 드래그
   - Canvas: Screen Space — Overlay, 1080×1920, Scale With Screen Size, Match 0.5

---

## Step 3: Hierarchy 구조

```
Canvas
├── BgPanel             (Image, 전체화면, #1A1A2E)
├── TopBar              (상단 고정, Height 140)
│   ├── TitleText       (TMP_Text, "협상 현황")
│   └── BackButton      (MUIP 버튼)
├── OfferScrollView     (ScrollRect, 중간 영역)
│   ├── Viewport
│   │   └── OfferContent (VerticalLayoutGroup + ContentSizeFitter)
├── EmptyLabel          (TMP_Text, 목록 빈 경우 표시)
└── ResponsePanel       (전체화면 오버레이, 초기: 비활성)
    └── ResponseBg      (라운드 패널, 중앙)
        ├── ResponseTitleText   (TMP_Text)
        ├── ResponseDetailText  (TMP_Text)
        ├── ReCounterAmountInput (TMP_InputField)
        └── ButtonRow
            ├── AcceptButton    (버튼)
            ├── RejectButton    (버튼)
            └── ReCounterButton (버튼)
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
  - TMP_Text: 텍스트 `협상 현황`, fontSize 40, Bold, Color #FFFFFF, 중앙 정렬
  - RectTransform: Anchor middle-center, Width 700, Height 80
  - Font: `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`
- **BackButton**
  - `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Minimal.prefab` 드래그
  - RectTransform: Anchor middle-right, Pos X -100, Width 160, Height 80
  - ButtonManager: Button Text → `뒤로`
  - **Click Event**: `+` → NegotiationController 오브젝트 드래그 → `OnBackClicked` 선택

### OfferScrollView

- RectTransform: stretch-stretch, Top 140, Bottom 0
- ScrollRect: Vertical ✓, Horizontal ✗, Movement Type: Clamped
- **Viewport**: RectTransform stretch-stretch offset 0, Image (Alpha 0), Mask (Show Mask Graphic ✗)
- **OfferContent**: VerticalLayoutGroup + ContentSizeFitter
  - VerticalLayoutGroup: Child Alignment Upper Left, Spacing 8, Child Force Expand Width ✓ Height ✗, Child Controls Size Width ✓ Height ✗, Padding Left/Right 20, Top 10
  - ContentSizeFitter: Vertical Fit = Preferred Size
  - RectTransform: top-stretch, Height 0 (ContentSizeFitter 자동)
- ScrollRect → Content: OfferContent, Viewport: Viewport

### EmptyLabel

- RectTransform: Anchor middle-center, Width 700, Height 80
- TMP_Text: 텍스트 `진행 중인 협상이 없습니다`, fontSize 32, Color #CCCCCC, 중앙 정렬
- Font: NotoSansKR-VF SDF.asset
- 초기 비활성화

### ResponsePanel

- RectTransform: stretch-stretch, offset 0
- **초기 비활성화** (Inspector 체크 해제)
- Image: Color #000000, Alpha 180 (딤 배경)

#### ResponseBg

- RectTransform: Anchor middle-center, Width 900, Height 900
- Image: Source Image `Assets/Imported/Modern UI Pack/Textures/Border/Rounded/256px/Rounded Filled 256px.png`, Image Type: Sliced, Pixels Per Unit: 10, Color #2A2A3E
- VerticalLayoutGroup: Spacing 20, Padding 40

안에 다음 오브젝트 (모두 Font NotoSansKR-VF SDF.asset):

- **ResponseTitleText**: TMP_Text, fontSize 34, Bold, Color #FFFFFF, 중앙, 멀티라인
- **ResponseDetailText**: TMP_Text, fontSize 28, Color #CCCCCC, 중앙, 멀티라인
- **ReCounterAmountInput**: TMP_InputField (MUIP 또는 기본)
  - Content Type: Integer Number
  - Placeholder: "재역제안 금액 입력", fontSize 26
  - Text: fontSize 28, Color #FFFFFF
  - Height 80
- **ButtonRow**: HorizontalLayoutGroup, Spacing 16, Child Force Expand Width ✗
  - **AcceptButton**: `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic/Standard.prefab`, Width 220, Height 90
    - ButtonManager: Button Text → `수락`
    - ⚠️ Click Event 연결 불필요 — 코드 Awake() 자동 연결
  - **RejectButton**: `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab`, Width 220, Height 90
    - ButtonManager: Button Text → `거절`
    - ⚠️ Click Event 연결 불필요
  - **ReCounterButton**: `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab`, Width 220, Height 90
    - ButtonManager: Button Text → `재역제안`
    - ⚠️ Click Event 연결 불필요

---

## Step 5: NegotiationOfferItem 프리팹 생성

Hierarchy 에 **NegotiationOfferItem** 빈 오브젝트 생성:

```
NegotiationOfferItem             (NegotiationOfferItem 컴포넌트, Button 컴포넌트)
├── BgImage                      (Image, Color #2A2A3E, RectTransform stretch-stretch)
└── ContentGroup                 (HorizontalLayoutGroup, Spacing 12, Padding Left/Right 16)
    ├── PlayerNameText           (TMP_Text, fontSize 28, Color #FFFFFF, Width 340)
    ├── AmountText               (TMP_Text, fontSize 28, Color #4A90D9, Width 200)
    ├── StatusText               (TMP_Text, fontSize 24, Color #CCCCCC, Width 220)
    └── RoundText                (TMP_Text, fontSize 24, Color #CCCCCC, Width 80)
```

RectTransform: Width 1040, Height 80

모든 TMP_Text Font: NotoSansKR-VF SDF.asset

**NegotiationOfferItem 컴포넌트 와이어링**:
| 필드 | 연결 대상 |
|------|-----------|
| `playerNameText` | `ContentGroup/PlayerNameText` |
| `amountText` | `ContentGroup/AmountText` |
| `statusText` | `ContentGroup/StatusText` |
| `roundText` | `ContentGroup/RoundText` |
| `selectButton` | NegotiationOfferItem 루트 오브젝트 (Button 컴포넌트 포함) |

**Button 컴포넌트**: NegotiationOfferItem 루트에 `Button` 컴포넌트 추가, Target Graphic → BgImage

Project 창 `Assets/Imported/FMLite UI/Prefabs/` 로 드래그 → `NegotiationOfferItem.prefab` 생성  
Hierarchy 의 임시 오브젝트 삭제.

---

## Step 6: NegotiationController 컴포넌트 추가 + 와이어링

Canvas 아래 빈 오브젝트 이름 `NegotiationController` → **NegotiationController** 컴포넌트 추가.

| 필드 | 드래그 대상 | 타입 |
|------|-------------|------|
| `offerListParent` | `Canvas/OfferScrollView/Viewport/OfferContent` | Transform |
| `offerItemPrefab` | `Assets/Imported/FMLite UI/Prefabs/NegotiationOfferItem.prefab` | GameObject |
| `responsePanel` | `Canvas/ResponsePanel` | GameObject |
| `responseTitleText` | `Canvas/ResponsePanel/ResponseBg/ResponseTitleText` | TMP_Text |
| `responseDetailText` | `Canvas/ResponsePanel/ResponseBg/ResponseDetailText` | TMP_Text |
| `reCounterAmountInput` | `Canvas/ResponsePanel/ResponseBg/ReCounterAmountInput` | TMP_InputField |
| `acceptButton` | `Canvas/ResponsePanel/ResponseBg/ButtonRow/AcceptButton` | Button |
| `rejectButton` | `Canvas/ResponsePanel/ResponseBg/ButtonRow/RejectButton` | Button |
| `reCounterButton` | `Canvas/ResponsePanel/ResponseBg/ButtonRow/ReCounterButton` | Button |
| `emptyLabel` | `Canvas/EmptyLabel` | TMP_Text |

⚠️ **`Button` 타입 필드에 MUIP 버튼 인스턴스 드래그** 시 Unity 가 Button 컴포넌트 자동 할당.  
acceptButton / rejectButton / reCounterButton 은 코드 Awake() 에서 onClick 자동 연결 — Inspector 에서 별도 Click Event 연결 불필요.

---

## Step 7: 씬 등록

`File → Build Settings` → `Add Open Scenes` → `Ctrl+S`

---

## 확인 체크리스트

- [ ] Main Camera (Tag: MainCamera, Background #1A1A2E)
- [ ] EventSystem 1개만 존재
- [ ] ResponsePanel 초기 비활성
- [ ] EmptyLabel 초기 비활성
- [ ] OfferContent 에 VerticalLayoutGroup + ContentSizeFitter 있음
- [ ] ScrollRect.Content = OfferContent, ScrollRect.Viewport = Viewport 연결
- [ ] NegotiationOfferItem.prefab 저장 완료
- [ ] NegotiationController 10개 필드 모두 None 아님
- [ ] TopBar BackButton Click Event → OnBackClicked 연결 확인
- [ ] Build Settings 등록 + Ctrl+S 저장

---

## 문제 발생 시

즉시 멈춤. `.cs` / MUIP 원본 prefab 수정 절대 금지. 에러 메시지 + Step 번호 보고.
