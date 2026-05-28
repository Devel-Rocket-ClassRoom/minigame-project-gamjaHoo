# N.6 Squad / Transfer 검색 필터 강화 — Unity AI 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.  
> **`.cs` 파일 절대 손대지 말 것.** 컴파일 에러 → 즉시 멈추고 보고.

---

## 컨텍스트

두 씬에 새 UI 요소 추가 후 Inspector 와이어링.

- **TransferScene** (`Assets/Scenes/TransferScene.unity`) — 이적 검색 필터 확장
- **SquadScene** (`Assets/Scenes/SquadScene.unity`) — 스쿼드 내 이름/포지션 검색 추가

컨트롤러는 이미 작성됨 — 수정 금지.

---

## 절대 규칙

1. `.cs` 파일 절대 손대지 말 것.
2. MUIP 원본 prefab 수정 금지.
3. 기존 오브젝트 삭제 금지. 추가만.
4. 완료 후 `Ctrl+S` 저장.

---

## 씬 A: TransferScene — 필터 확장

### 추가할 UI 오브젝트

기존 `FilterPanel` (또는 포지션/CA 필터가 있는 패널) 아래에 아래 필드들을 추가한다.

```
FilterPanel
├── (기존) PositionDropdown
├── (기존) MinCAInput
├── (기존) MaxCAInput
├── MinAgeInput          (TMP_InputField)  ← 신규
├── MaxAgeInput          (TMP_InputField)  ← 신규
├── NationalityInput     (TMP_InputField)  ← 신규
├── TraitDropdown        (TMP_Dropdown)    ← 신규
├── MinMarketValueInput  (TMP_InputField)  ← 신규
├── MaxMarketValueInput  (TMP_InputField)  ← 신규
├── MinContractMonthsInput (TMP_InputField) ← 신규
└── MaxContractMonthsInput (TMP_InputField) ← 신규
```

**각 TMP_InputField 설정** (동일):
- RectTransform: Height 40
- TMP_InputField: fontSize 24, Content Type: Integer Number (MarketValue는 Integer Number)
- NationalityInput: Content Type: Standard (문자열)
- Font: `Assets/_Project/Art/Fonts/NotoSansKR-VF SDF.asset`
- Placeholder 텍스트:
  - MinAgeInput: `"16"`
  - MaxAgeInput: `"99"`
  - NationalityInput: `"국적코드 (예: KOR)"`
  - MinMarketValueInput: `"0"`
  - MaxMarketValueInput: `""`
  - MinContractMonthsInput: `"0"`
  - MaxContractMonthsInput: `""`

**TraitDropdown 설정**:
- RectTransform: Height 40
- TMP_Dropdown: fontSize 24
- Font: NotoSansKR-VF SDF.asset
- 초기 선택: index 0 (코드에서 "전체"로 채움)

---

### TransferController Inspector 와이어링

Hierarchy 에서 **TransferController** 오브젝트 선택 → Inspector.

| 필드 | 드래그 대상 |
|------|-------------|
| `Min Age Input` | `FilterPanel/MinAgeInput` |
| `Max Age Input` | `FilterPanel/MaxAgeInput` |
| `Nationality Input` | `FilterPanel/NationalityInput` |
| `Trait Dropdown` | `FilterPanel/TraitDropdown` |
| `Min Market Value Input` | `FilterPanel/MinMarketValueInput` |
| `Max Market Value Input` | `FilterPanel/MaxMarketValueInput` |
| `Min Contract Months Input` | `FilterPanel/MinContractMonthsInput` |
| `Max Contract Months Input` | `FilterPanel/MaxContractMonthsInput` |

---

## 씬 B: SquadScene — 검색 필터 추가

### 추가할 UI 오브젝트

기존 탭 버튼 행 위 또는 아래에 새 `SquadSearchPanel` 추가.

```
Canvas
└── SquadSearchPanel         (빈 오브젝트, RectTransform)
    ├── SearchNameInput      (TMP_InputField)
    ├── SearchPositionDropdown (TMP_Dropdown)
    └── SearchButton         (MUIP ButtonManager)
```

**SquadSearchPanel 설정**:
- HorizontalLayoutGroup: Spacing 8, Child Controls Size Height ✓, Child Force Expand Height ✓

**SearchNameInput 설정**:
- RectTransform: Flexible Width, Height 40
- TMP_InputField: fontSize 24, Content Type: Standard
- Font: NotoSansKR-VF SDF.asset
- Placeholder: `"이름 검색..."`

**SearchPositionDropdown 설정**:
- RectTransform: Width 140, Height 40
- TMP_Dropdown: fontSize 24
- Font: NotoSansKR-VF SDF.asset
- 초기 선택: index 0 (코드에서 "전체"로 채움)

**SearchButton 설정**:
- MUIP ButtonManager
- Label: `"검색"`
- Click Event → `SquadController.OnSearchClicked`

---

### SquadController Inspector 와이어링

Hierarchy 에서 **SquadController** 오브젝트 선택 → Inspector.

| 필드 | 드래그 대상 |
|------|-------------|
| `Search Name Input` | `SquadSearchPanel/SearchNameInput` |
| `Search Position Dropdown` | `SquadSearchPanel/SearchPositionDropdown` |

---

## 완료 체크리스트

### TransferScene
- [ ] MinAgeInput / MaxAgeInput 추가 및 와이어링
- [ ] NationalityInput 추가 및 와이어링
- [ ] TraitDropdown 추가 및 와이어링
- [ ] MinMarketValueInput / MaxMarketValueInput 추가 및 와이어링
- [ ] MinContractMonthsInput / MaxContractMonthsInput 추가 및 와이어링

### SquadScene
- [ ] SquadSearchPanel — SearchNameInput / SearchPositionDropdown / SearchButton 추가
- [ ] SquadController 2개 필드 와이어링
- [ ] SearchButton Click Event → OnSearchClicked

- [ ] Ctrl+S 저장

---

## 문제 발생 시

즉시 멈춤. `.cs` / MUIP 원본 prefab 수정 절대 금지.
