# MentoringScene — Unity AI 지시서

## 목적
멘토링 그룹 관리 화면. 베테랑 1명 + 멘티 1-3명 묶음 → Hidden Attributes 수렴 (월 1회 자동 처리).

## 씬 생성

**씬 이름**: `MentoringScene`  
**저장 경로**: `Assets/_Project/Scenes/MentoringScene.unity`

---

## 계층 구조

```
Canvas (Screen Space - Overlay, 1080×1920)
└── Root (RectTransform, full stretch)
    ├── Header
    │   ├── TitleText (TMP — "멘토링 관리")
    │   └── BackButton (Button — "← 뒤로")
    ├── GroupListPanel
    │   └── GroupScrollView (ScrollView — Vertical)
    │       └── Content (VerticalLayoutGroup, spacing 8)
    │           └── [GroupItemPrefab 인스턴스들]
    ├── CreateGroupButton (Button — "+ 그룹 만들기")
    └── CreatePanel (GameObject — 초기 비활성)
        ├── PanelBG (Image, 반투명 검정 오버레이)
        └── PanelContent (VerticalLayoutGroup)
            ├── PanelTitle (TMP — "새 멘토링 그룹")
            ├── MentorLabel (TMP — "멘토 선택")
            ├── MentorDropdown (TMP_Dropdown)
            ├── MenteeLabel (TMP — "멘티 선택 (1-3명)")
            ├── MenteeScrollView (ScrollView — Vertical, 높이 300)
            │   └── Content (VerticalLayoutGroup)
            │       └── [MenteeTogglePrefab 인스턴스들]
            ├── ConfirmCreateButton (Button — "그룹 생성")
            └── CancelCreateButton (Button — "취소")
```

---

## MentoringController 연결

씬 Root 또는 빈 GameObject에 `MentoringController` 컴포넌트 추가 후 Inspector 연결:

| 필드 | 오브젝트 |
|------|---------|
| groupListParent | `GroupScrollView/Viewport/Content` |
| groupItemPrefab | `MentoringGroupItem.prefab` |
| createPanel | `CreatePanel` |
| mentorDropdown | `MentorDropdown` |
| menteeToggleParent | `CreatePanel/.../MenteeScrollView/Content` |
| menteeTogglePrefab | `MenteeToggleItem.prefab` |
| confirmCreateButton | `ConfirmCreateButton` |
| createGroupButton | `CreateGroupButton` |
| backButton | `BackButton` |

### Button OnClick 연결

| 버튼 | 메서드 |
|------|--------|
| BackButton | `MentoringController.OnBackClicked` |
| CreateGroupButton | `MentoringController.OnCreateGroupButtonClicked` |
| ConfirmCreateButton | `MentoringController.OnConfirmCreateClicked` |
| CancelCreateButton | `MentoringController.OnCancelCreateClicked` |

---

## Prefab 1: MentoringGroupItem

**경로**: `Assets/_Project/Prefabs/UI/MentoringGroupItem.prefab`

```
MentoringGroupItem (HorizontalLayoutGroup)
├── MentorLabel (TMP — 좌측, 폭 flex)
├── MenteesLabel (TMP — 중앙, 폭 flex)
└── DissolveButton (Button — "해체", 우측 고정 폭 80)
```

`MentoringGroupItem` 컴포넌트 Inspector 연결:
- mentorLabel → `MentorLabel`
- menteesLabel → `MenteesLabel`
- dissolveButton → `DissolveButton`

---

## Prefab 2: MenteeToggleItem

**경로**: `Assets/_Project/Prefabs/UI/MenteeToggleItem.prefab`

```
MenteeToggleItem (HorizontalLayoutGroup)
├── Toggle (Toggle 컴포넌트 루트)
└── Label (TMP — 선수 이름/나이)
```

`Toggle.onValueChanged` 는 `MentoringController` 코드에서 동적 등록 (prefab에 정적 연결 불필요).

---

## DashboardScene 진입 버튼

DashboardScene 사이드 메뉴에 "멘토링" 버튼 추가:
- 버튼 OnClick → `SceneManager.LoadScene("MentoringScene")`
- 또는 DashboardController에 `OnMentoringClicked` 메서드 연결
