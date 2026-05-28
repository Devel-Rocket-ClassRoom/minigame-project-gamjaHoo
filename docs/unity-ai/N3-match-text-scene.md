# N.3 MatchTextScene — Unity AI 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.  
> **`.cs` 파일 절대 손대지 말 것.** 컴파일 에러 → 즉시 멈추고 보고.

---

## 컨텍스트

- 프로젝트: FM-Lite (Unity 6, 축구 매니저)
- 목적: 유저 클럽 경기 분 단위 텍스트 중계 화면
- 씬 파일: `Assets/Scenes/MatchTextScene.unity` (새로 생성)
- 컨트롤러: `Assets/_Project/Scripts/UI/MatchTextController.cs` (이미 작성됨 — 절대 수정 금지)
- UI 에셋: `Assets/Imported/Modern UI Pack/` (MUIP)

---

## 절대 규칙

1. `.cs` 파일 절대 손대지 말 것.
2. `Assets/Imported/Modern UI Pack/` 원본 prefab 수정 금지.
3. 완료 후 `Ctrl+S` 저장.
4. **새 씬이므로 Main Camera 반드시 포함** (`GameObject → Camera`, Tag: MainCamera, Background Color: #1A1A2E).
5. MUIP `Canvas.prefab` 사용 시 EventSystem 이미 포함 — 별도 EventSystem 추가 금지.
6. 커스텀 프리팹 저장: `Assets/Imported/FMLite UI/Prefabs/` (폴더 없으면 먼저 생성).

---

## Step 1: 씬 생성

1. `File → New Scene` → Empty 씬 선택 → `Save As` → `Assets/Scenes/MatchTextScene.unity`

---

## Step 2: Main Camera + Canvas 배치

1. `GameObject → Camera` → 이름: **Main Camera**, Tag: `MainCamera`
   - Background Color: `#1A1A2E`
2. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab` 을 Hierarchy 에 드래그
   - Canvas 설정 확인: Screen Space — Overlay, Reference Resolution 1080×1920, Scale With Screen Size, Match 0.5
   - EventSystem 이 자동 포함됨 — 추가 생성 금지

---

## Step 3: Hierarchy 구조 생성

Canvas 아래에 다음 구조 생성:

```
Canvas
├── BgPanel              (Image, 전체화면, Color #1A1A2E, Alpha 255)
├── TopBar               (RectTransform, 상단 고정, Height 160)
│   ├── HomeNameText     (TMP_Text)
│   ├── ScoreGroup       (RectTransform, 중앙)
│   │   ├── ScoreText    (TMP_Text)
│   │   └── MinuteText   (TMP_Text)
│   └── AwayNameText     (TMP_Text)
├── EventScrollView      (ScrollRect 구조, 중간 영역)
│   ├── Viewport         (Mask + Image)
│   │   └── EventContent (VerticalLayoutGroup + ContentSizeFitter)
│   └── Scrollbar Vertical (선택)
├── ControlBar           (RectTransform, 하단 고정, Height 130)
│   ├── SpeedX1Button    (버튼)
│   ├── SpeedX2Button    (버튼)
│   ├── SpeedX4Button    (버튼)
│   └── SkipButton       (버튼)
└── ResultPanel          (전체화면 오버레이, 초기: 비활성)
    ├── ResultBg         (Image, Color #2A2A3E, 라운드 모서리)
    ├── ResultScoreText  (TMP_Text)
    ├── ScorersLabel     (TMP_Text, 정적 레이블)
    ├── ResultScorersText(TMP_Text)
    ├── RatingsLabel     (TMP_Text, 정적 레이블)
    ├── ResultRatingsText(TMP_Text)
    └── ResultBackButton (버튼)
```

---

## Step 4: 각 오브젝트 상세 설정

### BgPanel

- RectTransform: 앵커 → Alt 누른 채 **stretch-stretch** → offset 전부 0
- Image 컴포넌트: Color `#1A1A2E`, Alpha 255

### TopBar

- RectTransform: 앵커 → Alt 누른 채 **top-stretch** → Height 160, Top 0
- 폰트: 모든 TMP_Text → `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`
- **HomeNameText**: 왼쪽 정렬, fontSize 36, Bold, Color #FFFFFF
  - RectTransform: Anchor left-middle, Pos X 20, Width ~350, Height 80
- **ScoreGroup**: 중앙, Width 200, Height 120
  - RectTransform: Anchor middle-center, Pos X 0
  - **ScoreText**: 중앙 정렬, fontSize 52, Bold, Color #FFFFFF, 초기 텍스트: `0 : 0`
  - **MinuteText**: 중앙 정렬, fontSize 30, Normal, Color #CCCCCC, 초기 텍스트: `0'`
- **AwayNameText**: 오른쪽 정렬, fontSize 36, Bold, Color #FFFFFF
  - RectTransform: Anchor right-middle, Pos X -20, Width ~350, Height 80

### EventScrollView

- RectTransform: 앵커 → stretch-stretch, Top 160, Bottom 130 (TopBar 아래, ControlBar 위)
- ScrollRect 컴포넌트 추가:
  - Movement Type: Clamped
  - Vertical: ✓, Horizontal: ✗
- **Viewport** (ScrollView 자식):
  - RectTransform: stretch-stretch, offset 0
  - Image 컴포넌트 (투명: Alpha 0)
  - Mask 컴포넌트: Show Mask Graphic: ✗
- **EventContent** (Viewport 자식):
  - RectTransform: Anchor top-stretch, Height는 ContentSizeFitter가 자동 조정
  - VerticalLayoutGroup:
    - Child Alignment: Upper Left
    - Spacing: 6
    - Child Force Expand Width: ✓, Height: ✗
    - Child Controls Size Width: ✓, Height: ✗
    - Padding: Left 20, Right 20, Top 10
  - ContentSizeFitter: Vertical Fit = **Preferred Size**
- ScrollRect 컴포넌트 → `Content` 필드에 **EventContent** 드래그, `Viewport` 필드에 **Viewport** 드래그

### ControlBar

- RectTransform: 앵커 → Alt 누른 채 **bottom-stretch** → Height 130, Bottom 0
- Image 컴포넌트: Color `#2A2A3E`, Alpha 200
- HorizontalLayoutGroup:
  - Child Alignment: Middle Center
  - Spacing: 12
  - Padding: Left 20, Right 20
  - Child Force Expand Width: ✗, Height: ✗
  - Child Controls Size Width: ✗, Height: ✗
- **SpeedX1Button**: MUIP `Basic - Outline/Minimal.prefab` 인스턴스, Width 160, Height 90
  - ButtonManager: Button Text → `×1`
- **SpeedX2Button**: 동일 방식, Button Text → `×2`
- **SpeedX4Button**: 동일 방식, Button Text → `×4`
- **SkipButton**: MUIP `Basic - Outline/Minimal.prefab`, Width 200, Height 90
  - ButtonManager: Button Text → `건너뛰기`

⚠️ **ControlBar 버튼들은 Inspector Click Event 연결 불필요** — 코드(Awake)에서 자동 연결됨.  
  단, GameObject 를 컨트롤러 SerializeField 에 드래그 할당은 Step 5에서 수행.

### ResultPanel

- RectTransform: 앵커 → stretch-stretch, offset 0
- **초기 비활성화**: Inspector 에서 체크박스 해제 (GameObj 비활성)
- Image 컴포넌트: Color #000000, Alpha 180 (딤 배경)
- **ResultBg**: 중앙 패널
  - RectTransform: Anchor middle-center, Width 900, Height 1400
  - Image: Source Image → `Assets/Imported/Modern UI Pack/Textures/Border/Rounded/256px/Rounded Filled 256px.png`, Image Type: Sliced, Pixels Per Unit: 10, Color #2A2A3E
- ResultBg 안에 VerticalLayoutGroup (Spacing 24, Padding 40):
  - **ResultScoreText**: TMP_Text, fontSize 44, Bold, 중앙 정렬, Color #FFFFFF
  - **ScorersLabel**: TMP_Text, 텍스트 `득점자`, fontSize 28, Bold, Color #CCCCCC
  - **ResultScorersText**: TMP_Text, fontSize 26, Normal, Color #FFFFFF, 멀티라인
  - **RatingsLabel**: TMP_Text, 텍스트 `주요 평점`, fontSize 28, Bold, Color #CCCCCC
  - **ResultRatingsText**: TMP_Text, fontSize 26, Normal, Color #FFFFFF, 멀티라인
  - **ResultBackButton**: MUIP `Basic/Standard.prefab`, Width 500, Height 100
    - ButtonManager: Button Text → `대시보드로`
    - ⚠️ **Click Event 연결 불필요** — 코드(Awake)에서 자동 연결됨

---

## Step 5: EventItem 프리팹 생성

1. Hierarchy 에 빈 오브젝트 생성 → 이름: **EventItem**
2. RectTransform: Width 1040, Height 60
3. Image 컴포넌트 추가 (투명하게 Color Alpha 0, 또는 Color #2A2A3E Alpha 50)
4. TMP_Text 자식 추가:
   - RectTransform: stretch-stretch, offset Left/Right 16, Top/Bottom 4
   - Font: NotoSansKR-VF SDF.asset
   - fontSize: 24, Color #FFFFFF, Auto Size 허용 (Min 18, Max 28)
   - Overflow: Overflow
5. Project 창 `Assets/Imported/FMLite UI/Prefabs/` 폴더로 **EventItem** 드래그 → `.prefab` 생성
6. Hierarchy 의 임시 EventItem 오브젝트 삭제

---

## Step 6: MatchTextController 컴포넌트 추가 + 와이어링

Canvas 또는 별도 빈 GameObject (`MatchTextController`) 에 **MatchTextController** 컴포넌트 추가.

| 필드 | 드래그 대상 | 타입 |
|------|-------------|------|
| `homeName` | `Canvas/TopBar/HomeNameText` | TMP_Text |
| `awayName` | `Canvas/TopBar/AwayNameText` | TMP_Text |
| `scoreText` | `Canvas/TopBar/ScoreGroup/ScoreText` | TMP_Text |
| `minuteText` | `Canvas/TopBar/ScoreGroup/MinuteText` | TMP_Text |
| `eventScrollRect` | `Canvas/EventScrollView` (ScrollRect 컴포넌트 있는 오브젝트) | ScrollRect |
| `eventListContent` | `Canvas/EventScrollView/Viewport/EventContent` | Transform |
| `eventItemPrefab` | `Assets/Imported/FMLite UI/Prefabs/EventItem.prefab` | GameObject |
| `speedX1Button` | `Canvas/ControlBar/SpeedX1Button` (MUIP 인스턴스) | Button |
| `speedX2Button` | `Canvas/ControlBar/SpeedX2Button` | Button |
| `speedX4Button` | `Canvas/ControlBar/SpeedX4Button` | Button |
| `skipButton` | `Canvas/ControlBar/SkipButton` | Button |
| `resultPanel` | `Canvas/ResultPanel` | GameObject |
| `resultScoreText` | `Canvas/ResultPanel/ResultBg/ResultScoreText` | TMP_Text |
| `resultScorersText` | `Canvas/ResultPanel/ResultBg/ResultScorersText` | TMP_Text |
| `resultRatingsText` | `Canvas/ResultPanel/ResultBg/ResultRatingsText` | TMP_Text |
| `resultBackButton` | `Canvas/ResultPanel/ResultBg/ResultBackButton` | Button |

⚠️ **`Button` 타입 필드에 MUIP 버튼 인스턴스 드래그 시** Unity가 자동으로 Button 컴포넌트를 할당함.  
Inspector 의 onClick / Click Event 별도 연결 불필요 — 컨트롤러 코드에서 Awake() 때 자동 연결.

---

## Step 7: 씬 등록

`File → Build Settings` → `Add Open Scenes` 클릭 → `Ctrl+S` 저장.

---

## 확인 체크리스트

- [ ] Main Camera 존재 (Tag: MainCamera, Background #1A1A2E)
- [ ] EventSystem 1개만 존재 (MUIP Canvas.prefab 포함)
- [ ] ResultPanel 초기 비활성 (Inspector 체크 해제)
- [ ] EventContent 에 VerticalLayoutGroup + ContentSizeFitter(Preferred Size) 있음
- [ ] ScrollRect.Content = EventContent, ScrollRect.Viewport = Viewport 연결됨
- [ ] 16개 SerializeField 전부 None 아님
- [ ] EventItem.prefab 이 `Assets/Imported/FMLite UI/Prefabs/` 에 저장됨
- [ ] Build Settings 에 씬 등록됨
- [ ] `Ctrl+S` 저장 완료

---

## 문제 발생 시

1. **즉시 멈춤.** `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 에러 메시지 + 어느 Step 에서 막혔는지 보고.
3. Claude Code 에 전달해 다음 단계 결정.
