# N.2 Dashboard 상세 정보 — Unity AI 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.  
> **`.cs` 파일 절대 손대지 말 것.** 컴파일 에러 → 즉시 멈추고 보고.

---

## 컨텍스트

- 씬: `Assets/Scenes/DashboardScene.unity` (기존 씬 수정)
- 컨트롤러: `DashboardController` (이미 작성됨 — 수정 금지)
- 목표: 다음 매치 상세 정보 3개 + 사기/부상 요약 2개 TMP_Text 오브젝트 추가 후 Inspector 와이어링

---

## 절대 규칙

1. `.cs` 파일 절대 손대지 말 것.
2. MUIP 원본 prefab 수정 금지.
3. 기존 오브젝트 삭제 금지. 추가만.
4. 완료 후 `Ctrl+S` 저장.

---

## 추가할 UI 오브젝트

DashboardScene Hierarchy 의 **Canvas** 아래에 아래 두 패널을 새로 추가한다.

---

### 패널 A: 다음 매치 상세 (MatchDetailPanel)

**위치**: 기존 `nextMatchText` 바로 아래에 배치 (날짜 정보 다음 줄)

```
MatchDetailPanel              (빈 오브젝트, RectTransform)
├── OpponentFormText          (TMP_Text)
├── LastResultText            (TMP_Text)
└── H2HText                   (TMP_Text)
```

**MatchDetailPanel 설정**:
- RectTransform: 기존 nextMatchText 아래, top-stretch
- VerticalLayoutGroup: Spacing 4, Child Controls Size Width ✓, Child Force Expand Width ✓

**각 TMP_Text (OpponentFormText / LastResultText / H2HText) 동일 설정**:
- RectTransform: Height 30
- TMP_Text: fontSize 24, Color #CCCCCC
- Font: `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`
- 초기 텍스트: 빈 문자열 `""`

---

### 패널 B: 사기 / 부상 요약 (SquadAlertPanel)

**위치**: MatchDetailPanel 아래 (또는 기존 인박스 위)

```
SquadAlertPanel               (빈 오브젝트, RectTransform)
├── MoraleWarningText         (TMP_Text)
└── InjuryText                (TMP_Text)
```

**SquadAlertPanel 설정**:
- VerticalLayoutGroup: Spacing 4, Child Controls Size Width ✓, Child Force Expand Width ✓

**MoraleWarningText 설정**:
- TMP_Text: fontSize 24, Color **#E87040** (경고색)
- Font: NotoSansKR-VF SDF.asset
- 초기 텍스트: `""`

**InjuryText 설정**:
- TMP_Text: fontSize 24, Color **#E87040** (경고색)
- Font: NotoSansKR-VF SDF.asset
- 초기 텍스트: `""`

---

## DashboardController Inspector 와이어링

Hierarchy 에서 **DashboardController** 오브젝트 선택 → Inspector.

| 필드 | 드래그 대상 |
|------|-------------|
| `Opponent Form Text` | `MatchDetailPanel/OpponentFormText` |
| `Last Result Text` | `MatchDetailPanel/LastResultText` |
| `H2h Text` | `MatchDetailPanel/H2HText` |
| `Morale Warning Text` | `SquadAlertPanel/MoraleWarningText` |
| `Injury Text` | `SquadAlertPanel/InjuryText` |

---

## 완료 체크리스트

- [ ] MatchDetailPanel — OpponentFormText / LastResultText / H2HText 3개 TMP_Text 추가
- [ ] SquadAlertPanel — MoraleWarningText / InjuryText 2개 TMP_Text 추가
- [ ] DashboardController Inspector 5개 필드 None 아님
- [ ] Ctrl+S 저장

---

## 문제 발생 시

즉시 멈춤. `.cs` / MUIP 원본 prefab 수정 절대 금지.
