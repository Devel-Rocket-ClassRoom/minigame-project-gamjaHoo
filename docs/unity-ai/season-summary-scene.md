# SeasonSummaryScene Unity AI 지시서

## 목적

시즌 종료(5/15) 직후 자동으로 표시되는 요약 화면.  
`DashboardController.OnContinueClicked` 에서 5/15 감지 시 이 씬으로 전환됨.

---

## 씬 파일 경로

`Assets/Scenes/SeasonSummaryScene.unity`

---

## 사용 에셋 / 컴포넌트

- **Canvas**: Screen Space — Overlay, Reference Resolution 1080×1920
- **MUIP**: ButtonManager (버튼), 모든 텍스트는 `TextMeshPro — Text (UI)`
- **스크립트**: `Assets/_Project/Scripts/UI/SeasonSummaryController.cs` (이미 작성됨)

---

## 계층 구조 (Hierarchy)

```
SeasonSummaryScene
└── Canvas
    └── Root (VerticalLayoutGroup, padding 60, spacing 30, width=1080)
        ├── TitleText          (TMP_Text, fontSize 52, Bold, Center)
        ├── Divider            (Image, height 2, color #444444)
        ├── PositionSection
        │   ├── SectionLabel   ("📊 리그 순위", TMP_Text, fontSize 34, Bold)
        │   └── PositionText   (TMP_Text, fontSize 30, Normal)
        ├── AwardsSection
        │   ├── SectionLabel   ("🏆 수상 내역", TMP_Text, fontSize 34, Bold)
        │   └── AwardsText     (TMP_Text, fontSize 28, Normal, ContentSizeFitter)
        ├── FinanceSection
        │   ├── SectionLabel   ("💰 재정 결산", TMP_Text, fontSize 34, Bold)
        │   └── FinanceText    (TMP_Text, fontSize 28, Normal)
        ├── BoardSection
        │   ├── SectionLabel   ("📋 보드 평가", TMP_Text, fontSize 34, Bold)
        │   └── BoardText      (TMP_Text, fontSize 28, Normal)
        └── NextSeasonButton   (ButtonManager, 높이 100, 텍스트 "다음 시즌")
```

---

## SeasonSummaryController Inspector 연결

`Root` 오브젝트(또는 Canvas)에 `SeasonSummaryController` 컴포넌트 추가 후:

| 필드 | 연결 대상 |
|------|-----------|
| `titleText` | `TitleText` |
| `positionText` | `PositionSection/PositionText` |
| `awardsText` | `AwardsSection/AwardsText` |
| `financeText` | `FinanceSection/FinanceText` |
| `boardText` | `BoardSection/BoardText` |
| `nextSeasonButton` | `NextSeasonButton` |

`NextSeasonButton` 의 OnClick → `SeasonSummaryController.OnNextSeasonClicked()`

---

## 씬 등록 (EditorBuildSettings)

`File > Build Settings` 에서 `SeasonSummaryScene` 추가.  
또는 `ProjectSettings/EditorBuildSettings.asset` 에 씬 경로 추가:  
`Assets/Scenes/SeasonSummaryScene.unity`

---

## 확인 체크리스트

1. Play Mode 에서 `GameManager.Instance` 가 있는 상태에서 씬 직접 실행 → NullRef 없이 표시
2. `DashboardScene` 에서 Continue → 5/15 도달 시 이 씬으로 자동 전환
3. [다음 시즌] 버튼 클릭 → `DashboardScene` 복귀
