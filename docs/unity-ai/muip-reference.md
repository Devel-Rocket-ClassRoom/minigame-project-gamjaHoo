# Modern UI Pack (MUIP) — 프로젝트 전용 참조 (V1.0 확장)

> FM-Lite 프로젝트에서 실제 사용하는 컴포넌트 위주 요약.
> 전체 공식 문서: https://docs.michsky.com/docs/modern-ui-pack/
>
> **V1.0 확장 (2026-05-29):** HorizontalSelector / Slider / Switch / Progress Bar / Input Field / Notification / Window Manager / Tooltip / List View / Modal Window Style 2 / Movable Window / Context Menu / Animated Icon / UI Manager / Toggle / Toggle Group Panel 보강. V1.0 UI 폴리시 (글로벌 네비 / Options / 매치 대시보드 등) 에 필수.

---

## ⚠️ 핵심 규칙 (Unity AI 전달 필수)

1. **`.cs` 파일 절대 손대지 말 것** — 컴파일 에러가 나도 멈추고 보고.
2. **MUIP 원본 prefab 수정 금지** — 씬에 드래그해 인스턴스로만 사용.
3. **`ButtonManager.Click Event`** 사용 — Unity 기본 `Button.onClick` (Inspector 와이어링) 아님.
   _단, C# 코드에서 `AddListener` 로 연결하는 경우 Button 컴포넌트 직접 할당만 하면 됨._
4. **EventSystem 중복 금지** — MUIP Canvas.prefab 에 포함됨. 기존 씬엔 추가 금지.
5. **Main Camera 빠짐 없이** — 새 씬엔 반드시 포함.
6. **`useCustomContent = true`** — 코드로 텍스트/아이콘 세팅 시 필수 (Modal / Notification / Tooltip 등).

---

## 0. UI Manager — 일괄 테마 변경

**위치**: `Tools → Modern UI Pack → Show UI Manager`

- 전 컴포넌트 색상 / 폰트 / 모서리 둥글기를 **단일 SO** 로 제어.
- 프로젝트 표준 = `Assets/_Project/Data/UI/FMLiteUIManager.asset` (V1.0 신규 — Stage W 에서 생성).
- **사용 방법**: UI Manager 윈도우 열기 → "Apply To All" → 모든 MUIP prefab 일괄 갱신.
- **주의**: prefab override 가 있는 인스턴스는 우회됨 → 인스턴스마다 재확인.

---

## 프리팹 경로 (자주 사용)

| 용도 | 경로 |
|------|------|
| Canvas (새 씬용, EventSystem 포함) | `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab` |
| 기본 외곽선 버튼 | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab` |
| 기본 채색 버튼 | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic/Standard.prefab` |
| 작은 버튼 (Minimal) | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Minimal.prefab` |
| 아이콘 버튼 (이미지만) | `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Only Icon/Standard.prefab` |
| 드롭다운 | `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab` |
| 멀티셀렉트 드롭다운 | `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown - Multi Select.prefab` |
| 가로 셀렉터 | `Assets/Imported/Modern UI Pack/Prefabs/Horizontal Selector/Horizontal Selector.prefab` |
| 슬라이더 (기본) | `Assets/Imported/Modern UI Pack/Prefabs/Slider/Standard/Standard.prefab` |
| 슬라이더 (그라데이션) | `Assets/Imported/Modern UI Pack/Prefabs/Slider/Gradient/Standard.prefab` |
| 슬라이더 (범위) | `Assets/Imported/Modern UI Pack/Prefabs/Slider/Range/Standard.prefab` |
| 스위치 (ON/OFF 토글) | `Assets/Imported/Modern UI Pack/Prefabs/Switch/Switch - Standard.prefab` |
| 토글 | `Assets/Imported/Modern UI Pack/Prefabs/Toggle/Toggle - Standard.prefab` |
| 토글 그룹 패널 | `Assets/Imported/Modern UI Pack/Prefabs/Toggle/Toggle Group Panel.prefab` |
| 인풋필드 | `Assets/Imported/Modern UI Pack/Prefabs/Input Field/Input Field - Standard (Middle).prefab` |
| 인풋필드 (멀티라인) | `Assets/Imported/Modern UI Pack/Prefabs/Input Field/Input Field - Multi-Line.prefab` |
| 진행 바 (수평) | `Assets/Imported/Modern UI Pack/Prefabs/Progress Bar/PB - Standard.prefab` |
| 진행 바 (원형) | `Assets/Imported/Modern UI Pack/Prefabs/Progress Bar/PB - Radial (Regular).prefab` |
| 모달 윈도우 Style 1 | `Assets/Imported/Modern UI Pack/Prefabs/Modal Window/Style 1.prefab` |
| 모달 윈도우 Style 2 | `Assets/Imported/Modern UI Pack/Prefabs/Modal Window/Style 2.prefab` |
| 토스트 알림 (Fading) | `Assets/Imported/Modern UI Pack/Prefabs/Notification/Fading Notification.prefab` |
| 팝업 알림 (Popup) | `Assets/Imported/Modern UI Pack/Prefabs/Notification/Popup Notification.prefab` |
| 슬라이드 알림 | `Assets/Imported/Modern UI Pack/Prefabs/Notification/Sliding Notification.prefab` |
| 툴팁 | `Assets/Imported/Modern UI Pack/Prefabs/Tooltip/Tooltip.prefab` |
| 리스트 뷰 | `Assets/Imported/Modern UI Pack/Prefabs/List View/List View.prefab` |
| 윈도우 매니저 | `Assets/Imported/Modern UI Pack/Prefabs/Window Manager/Window Manager.prefab` |
| 무버블 윈도우 | `Assets/Imported/Modern UI Pack/Prefabs/Movable Window/(직접 탐색)` |
| 컨텍스트 메뉴 | `Assets/Imported/Modern UI Pack/Prefabs/Context Menu/Context Menu.prefab` |
| 애니메이티드 아이콘 | `Assets/Imported/Modern UI Pack/Prefabs/Animated Icon/(직접 탐색)` |
| 커스텀 프리팹 저장 위치 | `Assets/Imported/FMLite UI/Prefabs/` |

---

## 1. ButtonManager

### 타입 구분
- **`ButtonManager`** — MUIP 버튼 컴포넌트. `[SerializeField] ButtonManager myBtn;` 에 할당.
- **`Button`** (Unity 기본) — MUIP 버튼 prefab 인스턴스에도 `Button` 컴포넌트가 존재. `[SerializeField] Button myBtn;` 에 드래그하면 자동 할당됨.

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

- Inspector Click Event = `ButtonManager.onClickEvent` (또는 `clickEvent` v5.5+)
- 코드에서 `myButton.onClick.AddListener(...)` 는 `Button.onClick` (다른 이벤트)
- 두 개 혼용 가능. 프로젝트 표준: 코드 쪽 = `Button` 타입 / Inspector 연결 = `ButtonManager.Click Event`

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
myDropdown.dropdownItems.Clear();              // 기존 항목 삭제
myDropdown.CreateNewItemFast("항목1", null);    // null = 아이콘 없음
myDropdown.CreateNewItemFast("항목2", null);
myDropdown.selectedItemIndex = 0;
myDropdown.SetupDropdown();                    // 반드시 호출
myDropdown.dropdownEvent.AddListener((int i) => { });
```

### 펼침 가림 문제 (V0.5 TacticScene 사례)

ScrollView 안에 dropdown 이 있을 때 펼침이 viewport 에 clip 됨 (RectMask2D 한계). 해결:
- ScrollView 폐기하고 일반 컨테이너 사용 (`docs/unity-ai/tactic-scene-redesign.md` 참조), 또는
- Dropdown 자식 RectTransform 에 `Canvas` (Override Sorting = true / Sorting Order = 20) + `Graphic Raycaster` 추가 → 다른 UI 위에 그려짐.

### 주의

- `Init At Start = false` 이면 코드에서 `SetupDropdown()` 호출 전까지 드롭다운이 비어있음
- 저장 기능 사용 시 인스턴스마다 고유 **Dropdown Tag** 필요

---

## 3. HorizontalSelector (V1.0 신규 정리)

좌/우 화살표로 옵션 순환. Options 의 언어 / 통화 / UI Scale, Tactic 의 Mentality 7단계 등에 사용.

### Inspector 설정

| 항목 | 값 |
|------|----|
| **Enable Icon** | 보통 체크 해제 (아이콘 없음) |
| **Enable Indicators** | 체크 유지 (하단에 점 표시) |
| **Loop Selection** | 보통 체크 해제 (끝에서 정지) |
| **Default Index** | 초기 선택 인덱스 (0 ~ N-1) |

### 스크립팅 API

```csharp
[SerializeField] HorizontalSelector mySelector;
mySelector.itemList.Clear();
mySelector.CreateNewItem("Korean");
mySelector.CreateNewItem("English");
mySelector.defaultIndex = 0;
mySelector.SetupSelector();
mySelector.selectorEvent.AddListener(OnIndexChanged);   // (int index)

// 현재 값
int idx = mySelector.index;
```

### 사용처 (V1.0)
- Options 언어 (KO/EN)
- Options 통화 (£/$/€/₩)
- Options UI Scale (90/100/110/125)
- TacticScene Mentality (VeryDefensive ~ VeryAttacking 7단계)
- MatchResultDashboard 탭 전환 (선택적)

---

## 4. Slider (V1.0 신규 정리)

### 종류

| Prefab | 용도 |
|--------|------|
| Slider/Standard/Standard.prefab | 일반 0-100 |
| Slider/Gradient/Standard.prefab | 색상 그라데이션 (예: morale 표시) |
| Slider/Range/Standard.prefab | 최소/최대 두 핸들 (예: stat 범위 필터) |
| Slider/Radial/Standard.prefab | 원형 슬라이더 |
| Slider/Outline/Standard.prefab | 아웃라인 스타일 |

### Inspector 설정 (Standard)

| 항목 | 값 |
|------|----|
| **Min Value** | 0 (또는 사용 범위) |
| **Max Value** | 100 |
| **Whole Numbers** | 정수만 받을지 |
| **Show Value** | 우측에 현재 값 표시 |
| **Value Format** | `{0}%`, `£{0}M` 등 (`{0}` = 현재 값) |

### 스크립팅 API

```csharp
[SerializeField] SliderManager mySlider;
mySlider.mainSlider.value = 80;
mySlider.UpdateUI();
mySlider.mainSlider.onValueChanged.AddListener(OnVolumeChanged);   // (float v)

// AudioMixer 연동 패턴 (Options)
mySlider.mainSlider.onValueChanged.AddListener(v => {
    audioMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(0.0001f, v / 100f)) * 20f);
});
```

### 사용처 (V1.0)
- Options Master / SFX / BGM 볼륨
- Promise 진행률 (또는 Progress Bar 더 적합)
- 매치 가속 슬라이더 (선택 — HorizontalSelector 가 더 적합)

---

## 5. Switch (V1.0 신규 정리)

ON / OFF 이진 토글. 부드러운 슬라이드 애니메이션.

### Inspector

| 항목 | 값 |
|------|----|
| **Is On** | 초기 상태 |
| **Save Value** | PlayerPrefs 자동 저장 여부 (Switch Tag 필요) |
| **Switch Tag** | PlayerPrefs 키 (Save Value 사용 시) |

### 스크립팅 API

```csharp
[SerializeField] SwitchManager mySwitch;
mySwitch.isOn = true;
mySwitch.UpdateUI();
mySwitch.onValueChanged.AddListener(OnAutoSaveToggled);   // (bool v)
```

### 사용처 (V1.0)
- Options 자동 저장 ON/OFF
- Debug 모드 토글 (Debug Window 내)
- Squad 검색 비교 모드 ON/OFF

---

## 6. Toggle / Toggle Group Panel (V1.0 신규 정리)

### Toggle (단일)
- 체크박스 형태. Switch 와 달리 ✓ / ☐ 아이콘.

```csharp
[SerializeField] ToggleManager myToggle;
myToggle.isOn = false;
myToggle.UpdateUI();
myToggle.onValueChanged.AddListener(OnToggled);
```

### Toggle Group Panel
- 라디오 그룹. 여러 Toggle 중 하나만 선택.
- Inspector → `Toggle Group` 컴포넌트 → 자식 Toggles 가 자동 그룹화.

### 사용처 (V1.0)
- Tactic 의 그룹 훈련 강도 (Low / Medium / High) — Toggle Group
- Squad 의 1군/유스 탭 전환 (또는 Window Manager)

---

## 7. Progress Bar (V1.0 신규 정리)

### 종류

| Prefab | 용도 |
|--------|------|
| PB - Standard.prefab | 수평 막대 |
| PB - Radial (Regular).prefab | 원형 (회전 진행) |
| PB - Radial Filled Horizontal.prefab | 원형 filled (수평) |
| PB - Radial (Bold/Light/Thin).prefab | 원형 스타일 변형 |

### Inspector

| 항목 | 값 |
|------|----|
| **Current Percent** | 현재 진행률 (0-100) |
| **Show Percent** | 가운데 % 텍스트 표시 |
| **Speed** | 애니메이션 속도 (default 0.1) |
| **Restart On Enable** | 활성화 시 0 부터 다시 |

### 스크립팅 API

```csharp
[SerializeField] ProgressBar myProgress;
myProgress.currentPercent = 75;
myProgress.UpdateUI();
```

### 사용처 (V1.0)
- 시설 업그레이드 진행률 (현재일 / 완료일)
- Promise 진행률 ("출전 시간 35% / 목표 50%")
- Mentoring 수렴률 (Hidden Attribute 차이)
- 매치 가속 진행 표시 (선택)

---

## 8. Input Field (V1.0 신규 정리)

### 종류

| Prefab | 용도 |
|--------|------|
| Input Field - Standard (Middle).prefab | 일반 1줄 |
| Input Field - Standard (Left/Right).prefab | 정렬 변형 |
| Input Field - Fading (Middle).prefab | 포커스 시 라벨 페이드 |
| Input Field - Multi-Line.prefab | 여러 줄 (Description 등) |

### 스크립팅 API

MUIP InputField 는 내부에 Unity 기본 `TMP_InputField` 가 있음.

```csharp
[SerializeField] TMP_InputField myInput;
myInput.text = "default";
myInput.onValueChanged.AddListener(OnInputChanged);    // (string text)
myInput.onSubmit.AddListener(OnSubmit);
```

### 사용처 (V1.0)
- Save 슬롯명 입력 (§3.13)
- Squad / Transfer 이름 검색
- 디버그 모드 시드 입력

---

## 9. ModalWindowManager (V1.0 보강)

### Style 비교

| Prefab | 형태 |
|--------|------|
| Modal Window/Style 1.prefab | 좌측 아이콘 + 텍스트 + 버튼 |
| Modal Window/Style 2.prefab | 상단 헤더 색상 강조 |

### 스크립팅 API

```csharp
[SerializeField] ModalWindowManager myModal;
myModal.useCustomContent = true;          // ← 코드 세팅 시 필수
myModal.titleText = "확인";
myModal.descriptionText = "정말로 하시겠습니까?";
myModal.UpdateUI();
myModal.OpenWindow();
myModal.confirmButton.onClick.AddListener(OnConfirmed);   // ButtonManager 의 Button 직접 접근
myModal.cancelButton.onClick.AddListener(OnCancelled);
```

### 사용처 (V1.0)
- TopBar 홈 버튼 → "메인 메뉴로 돌아갈까요?" 확인 모달
- 보드 약속 수락/거절 모달
- TransferRequest 다이얼로그 (이미 V0.5 사용)
- 단축키 안내 (Options 내)
- GlobalSavePanel (모달 형태)

### 주의
- `useCustomContent = false` 면 UI Manager 가 prefab 의 정적 텍스트로 덮어씀.
- Open / Close 애니메이션 = `Animator` 컴포넌트가 처리 (Modal 가 가진 Animator).

---

## 10. Notification (V1.0 신규 정리)

토스트 / 팝업 알림. 일정 시간 후 자동 사라짐.

### 종류

| Prefab | 형태 |
|--------|------|
| Fading Notification.prefab | 페이드 인/아웃 |
| Popup Notification.prefab | 팝업 (애니메이션) |
| Sliding Notification.prefab | 화면 가장자리에서 슬라이드 |

### 스크립팅 API

```csharp
[SerializeField] NotificationManager myNotif;
myNotif.useCustomContent = true;
myNotif.title = "저장 완료";
myNotif.description = "슬롯: autosave_001";
myNotif.UpdateUI();
myNotif.OpenNotification();   // 자동 닫힘 (Animator)
```

### 사용처 (V1.0)
- 자동 저장 완료 토스트 (우상단 슬라이드, 3초)
- 인박스 신규 알림 토스트 (NEW 표시 후 자동 페이드)
- 매치 결과 도착 알림 (사용자 매치 외 동시 매치 결과)

### Inbox 와 차이
- **Notification**: 일회성 / 자동 사라짐 / 클릭 X
- **Inbox (§3.18)**: 누적 / 사용자가 명시적 처리 / 카테고리

---

## 11. Tooltip (V1.0 신규 정리)

호버 시 부가 설명 표시.

### Inspector

호버 대상 GameObject 에 `TooltipContent` 컴포넌트 추가:

| 항목 | 값 |
|------|----|
| **Description** | 툴팁 텍스트 |
| **Tooltip Position** | Top / Bottom / Left / Right |
| **Delay** | 호버 후 표시 지연 (default 0.5s) |

### Manager

씬에 하나의 `Tooltip` prefab 배치 (싱글톤 역할). 모든 `TooltipContent` 가 자동 사용.

### 사용처 (V1.0)
- stat 색상 코딩 호버 → "Elite (80+) — 동일 라인 상위 10%" 같은 설명
- 시너지 표시 호버 → "빅앤스몰: 키 큰 ST + 작은 윙 조합 보너스"
- 시설 효과 호버 → 등급별 효과 미리보기

---

## 12. List View (V1.0 신규 정리)

스크롤 가능 동적 목록. ScrollView + ViewportContent + LayoutGroup 사전 셋업.

### 사용 패턴

```csharp
[SerializeField] Transform listContent;
[SerializeField] GameObject itemPrefab;

void PopulateList(List<Item> items) {
    foreach (Transform child in listContent)
        Destroy(child.gameObject);

    foreach (var item in items) {
        var go = Instantiate(itemPrefab, listContent);
        go.GetComponent<MyItemController>().Setup(item);
    }
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)listContent);
}
```

### MUIP List View prefab 사용
- 검색 + 정렬 + 필터 통합 prefab. 복잡 케이스 (1000+ 항목) 에서 유리.
- 단순 케이스는 ScrollView + VerticalLayoutGroup 자체 구현 충분.

### 사용처 (V1.0)
- Scout 명단 (3000+ 선수)
- Inbox 카테고리 탭
- Squad 비교 결과 후보 목록

---

## 13. Window Manager (V1.0 신규 정리)

여러 패널 / 탭을 한 컨테이너에서 전환. TacticLineup 통합 (§3.7) 에 유용.

### 사용 패턴

```csharp
[SerializeField] WindowManager windowManager;

void OnTabClicked(int index) {
    windowManager.OpenWindow(index);   // 0=Tactic / 1=Lineup / 2=SetPieces
}
```

### Inspector
- `windows` 리스트에 각 패널 GameObject 등록
- 첫 윈도우 = 기본 표시
- 애니메이션 (Fade / Slide) 자동

### 사용처 (V1.0)
- §3.7 TacticLineupScene 통합 — 탭 전환
- §3.23 MatchResultDashboard 6탭 (개요/평점/통계/히트맵/슛맵/이벤트)
- Squad 화면 1군/유스/Mentoring 탭

---

## 14. Movable Window (V1.0 신규 정리)

드래그로 이동 가능한 윈도우. 비교 도구 / 디버그 패널에 유용.

### Inspector
- `MovableWindow` 컴포넌트 + `DragHandler`
- 드래그 영역 = 헤더 GameObject 지정

### 사용처 (V1.0)
- SquadComparisonScene 의 비교 패널 (선택적 — 단일 풀스크린이 더 적합)
- 디버그 윈도우 (Play Mode)

---

## 15. Context Menu (V1.0 신규 정리)

우클릭 메뉴. 선수 row 에 컨텍스트 메뉴 (면담 / 재계약 / 방출 / 캡틴 임명).

### Inspector
- `Context Menu Manager` 컴포넌트 + 자식 `Context Menu Button` / `Context Menu Separator` 배치
- `Context Menu Sub Menu` 로 계층 메뉴

### 스크립팅 API

```csharp
[SerializeField] ContextMenuManager contextMenu;

void OnPlayerRowRightClick(int playerId) {
    _selectedPlayerId = playerId;
    contextMenu.SetPosition(Input.mousePosition);
    contextMenu.Show();
}
```

### 사용처 (V1.0)
- Squad / PlayerProfile — 선수 우클릭 (면담 / 재계약 / 방출 / 캡틴 / 비교 추가)
- Schedule — 매치 우클릭 (결과 보기 / 라인업 보기)

---

## 16. Animated Icon (V1.0 신규 정리)

루프 애니메이션 아이콘. 인박스 unread 표시 / 로딩 / 알림.

### Inspector
- `AnimatedIcon` 컴포넌트 + Animator
- `Auto Start` 체크 시 활성 시 자동 재생

### 사용처 (V1.0)
- 인박스 unread 카운트 옆 깜빡임
- 로딩 (시즌 시뮬 진행 중)
- 매치 진행 표시 (MatchTextScene 동안)

---

## 17. 자주 쓰는 표준 패턴 (V1.0 갱신)

### Canvas (새 씬)

```
MUIP Canvas.prefab 드래그
→ Screen Space — Overlay
→ Reference Resolution: 1920 × 1080  ← V1.0 변경 (가로 데스크탑)
→ Scale With Screen Size, Match 0.5
(EventSystem 포함됨 — 별도 추가 금지)
```

### GlobalNav 통합 패턴 (V1.0 신규)

모든 컨텐츠 씬:

```
Scene Root
├── Main Camera
├── EventSystem
├── Canvas (MUIP, 1920×1080)
│   ├── GlobalNavPrefab          ← V1.0 모든 씬 공통 (Stage W)
│   │   ├── TopBar               (80px, 상단 stretch)
│   │   │   ├── BackButton       (좌상단)
│   │   │   ├── DateText
│   │   │   ├── MoneyText
│   │   │   ├── TokenText
│   │   │   ├── InboxButton + Badge
│   │   │   ├── OptionsButton    (우상단)
│   │   │   ├── SaveButton
│   │   │   └── HomeButton
│   │   ├── SideBar              (200px, 좌측 stretch, TopBar 아래)
│   │   │   └── 9 메인 메뉴 버튼
│   │   └── InboxPanel           (우측 슬라이드, 비활성 시작)
│   └── ContentRoot              (200, -80 offset, 나머지 영역)
│       └── (씬별 컨텐츠)
└── (씬별 컨트롤러 GameObject)
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

## 18. 폰트 / 색상 표준 (V1.0 다크 유지)

### 폰트
모든 TMP_Text: `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`

### 색상 팔레트 (V0.5 그대로 + V1.0 추가)

| 용도 | Hex | V1.0 추가 |
|------|-----|-----------|
| 씬 배경 | #1A1A2E | (유지) |
| 패널 배경 | #2A2A3E | (유지) |
| 기본 텍스트 | #FFFFFF | (유지) |
| 보조 텍스트 | #CCCCCC | (유지) |
| 강조 (버튼) | #4A90D9 | (유지) |
| 경고 | #E87040 | (유지) |
| 성공 | #4CAF50 | (유지) |
| 딤 배경 | #000000 Alpha 180 | (유지) |
| **Stat Elite** (80+) | — | #2ECC71 (진녹) — V1.0 신규 |
| **Stat Good** (65-79) | — | #82E08A (녹) |
| **Stat Average** (50-64) | — | #BBBBBB (회) |
| **Stat Weak** (35-49) | — | #F39C12 (주황) |
| **Stat Poor** (-34) | — | #E74C3C (빨강) |
| **인박스 우선순위 High** | — | #FF6B6B |
| **인박스 RequiresAction** | — | #FFD93D (노랑) |

---

## 19. RectTransform 앵커

| 용도 | 방법 |
|------|------|
| 전체 화면 | Alt 누른 채 stretch-stretch → offset 0 |
| 중앙 고정 (모달) | Alt 누른 채 middle-center → W/H 직접 입력 |
| 상단 고정 (TopBar) | Alt 누른 채 top-stretch → Height 80 |
| 좌측 고정 (SideBar) | Alt 누른 채 stretch-left → Width 200 (TopBar 아래) |
| 컨텐츠 영역 (V1.0) | Top-Left anchor, offsetMin=(200, -하단), offsetMax=(0, -80) |
| 우측 슬라이드 패널 | Alt 누른 채 stretch-right → Width N, 초기 비활성 |

---

## 20. 자주 발생하는 문제 (V1.0 보강)

| 증상 | 원인 / 해결 |
|------|-------------|
| SetText / SetIcon 효과 없음 | `useCustomContent = true` 체크 확인 |
| Dropdown 선택 시 onValueChanged 안 됨 | `SetupDropdown()` 호출 여부 확인 |
| 씬 첫 실행 시 NullRef | Enter Play Mode Options → "Reload Scene" 활성화 |
| EventSystem 중복 경고 | MUIP Canvas.prefab 이미 EventSystem 포함 — 중복 삭제 |
| Button 클릭 이벤트 안 됨 | Click Event 는 `ButtonManager` 컴포넌트 안에 있음 (`Button.onClick` 아님) |
| Dropdown 펼침이 ScrollView 안에 가려짐 | RectMask2D 한계. ScrollView 폐기 또는 Dropdown 에 Sub-Canvas + Sorting Order ↑ |
| HorizontalSelector 첫 표시가 빈 칸 | `SetupSelector()` 호출 누락 / `defaultIndex` 범위 외 |
| Slider value 가 정수 안 됨 | `Whole Numbers` 체크 |
| Switch 가 PlayerPrefs 자동 저장 안 됨 | `Save Value` + `Switch Tag` 둘 다 설정 |
| ProgressBar 가 100% 가 안 됨 | `Speed` 가 너무 느림 (애니메이션 도중) — `Restart On Enable` 해제 검토 |
| Modal Open 후 확인 클릭 시 두 번 발화 | `OnEnable` 마다 `AddListener` 누적 — `RemoveAllListeners` 후 추가 |
| Notification 자동 안 닫힘 | Animator State 의 Exit Time 확인 |
| Tooltip 안 뜸 | 씬에 `Tooltip` prefab 인스턴스 없음 — 한 개 배치 필수 |
| Window Manager 탭 전환 시 데이터 안 새로고침 | `OpenWindow` 후 자체 Refresh 함수 호출 |
| Context Menu 가 화면 밖에 뜸 | `SetPosition` 후 Clamp 적용 (자체 보정 안 함) |
| GlobalNav 가 다른 UI 위에 안 그려짐 | Canvas Override Sorting + sortingOrder = 100 (V1.0 표준) |
| **HorizontalSelector** Mentality 7단계 표시 폭 부족 | Selector 폭 360 ~ 400px 필요. Width 작으면 잘림 |
| **Slider** value text 표시 안 됨 | `Show Value` 체크 + `Value Format` 비어있지 않음 |
| **UI Manager** 일괄 변경 후 일부 prefab 만 갱신됨 | prefab override 가 있는 인스턴스는 우회됨. 인스턴스마다 "Revert" 또는 수동 재설정 |

---

## 21. V1.0 새 화면별 MUIP 컴포넌트 매트릭스

| 씬 / 패널 | 핵심 MUIP 컴포넌트 |
|----------|-------------------|
| **GlobalNavPrefab** (§3.19) | ButtonManager × 9 / TMP_Text × 4 / Notification (Inbox slide) / Animated Icon (unread badge) |
| **OptionsScene** (§3.20) | Slider × 3 / HorizontalSelector × 3 / Switch / ModalWindow (단축키 안내) / ButtonManager |
| **MatchResultDashboard** (§3.23) | Window Manager (6 탭) / ListView (이벤트 시계열) / Tooltip (stat 호버) |
| **TrainingTab** (§3.24) | Toggle Group Panel (강도) / Dropdown (포지션 그룹) / ModalWindow (개인 훈련 설정) |
| **SquadComparisonScene** (§3.25) | ListView / Tooltip / ProgressBar (능력 곡선 시각) |
| **TacticLineupScene 통합** (§3.7) | Window Manager (Tactic/Lineup/SetPieces 탭) / CustomDropdown (Role/Duty 팝업) |
| **CupBracketScene** (§3.14) | ListView (라운드별) / ButtonManager |
| **MatchPreviewScene** (§3.7) | ButtonManager (시작) / Tooltip (시너지) |
| **YouthManagementScene** (§3.4) | ProgressBar (성장 동향) / Tooltip (예고 정보) |
| **GlobalSavePanel (모달)** (§3.13) | ModalWindow Style 1 / Input Field / ButtonManager |
| **InboxPanel (TopBar 슬라이드)** (§3.18) | ListView (카테고리 탭) / Animated Icon / Tooltip |

---

## Change Log

| Date | Change |
| --- | --- |
| 2026-04-XX (V0.5) | 초안 — ButtonManager / CustomDropdown / ModalWindowManager + Canvas / ScrollRect / VerticalLayoutGroup / 라운드 패널 + 폰트 / 색상 / 앵커. 6 섹션. |
| 2026-05-29 (V1.0) | **대규모 보강** — 12 컴포넌트 추가 (HorizontalSelector / Slider / Switch / Toggle / Progress Bar / Input Field / Notification / Tooltip / List View / Window Manager / Movable Window / Context Menu / Animated Icon). UI Manager 도입 + 일괄 테마 변경 패턴. V1.0 색상 팔레트 확장 (stat 5단계 + 인박스 우선순위). GlobalNav 통합 패턴 표준화 (§3.19). 새 화면별 MUIP 매트릭스 (§21). Reference Resolution 1920×1080 (가로 데스크탑) 변경. 자주 발생하는 문제 ~15 추가. |
