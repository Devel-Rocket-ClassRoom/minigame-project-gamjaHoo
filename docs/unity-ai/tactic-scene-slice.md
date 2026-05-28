# TacticScene 슬라이스 — Unity AI 작업 지시서

> Unity AI Assistant 에 그대로 전달할 작업 지시서.
> 이 문서의 모든 단계는 **에디터 안 작업** (씬 생성·프리팹 인스턴스화·인스펙터 설정·Build Settings). **`.cs` 파일은 절대 손대지 말 것.**

## 컨텍스트 (Unity AI 가 알아야 할 것)
- 프로젝트: FM-Lite (Unity 6, 축구 매니저 게임).
- 컨트롤러는 **이미 작성되어 있다**: `Assets/_Project/Scripts/UI/TacticController.cs` (FMLite.UI 어셈블리). 이 슬라이스는 이 컨트롤러를 호스트하는 새 씬을 만드는 작업.
- 컨트롤러는 5 개의 `[SerializeField]` 참조를 받는다 — 그것들을 **인스펙터에서 드래그로 와이어링** 하는 게 핵심.
- 슬라이스 범위: Formation 선택 (드롭다운) + Mentality 선택 (좌우 셀렉터) + Save / Back 버튼. **11 슬롯 Role/Duty 편집은 이번 슬라이스 제외 (후속).**
- MUIP (Modern UI Pack) 프리팹을 인스턴스로 사용. 새 prefab 은 만들지 않음 (이번 슬라이스에는).

## 절대 규칙
1. **`.cs` 파일 생성/수정/삭제 절대 금지.** 컴파일 에러가 나도 .cs 손대지 말고 멈춰서 보고만.
2. **`Assets/Imported/Modern UI Pack/` 안의 프리팹 자체를 수정하지 말 것.** 씬에 인스턴스를 드래그해 배치만.
3. 모든 작업이 끝나면 `Ctrl+S` 로 씬 저장.
4. ⚠️ **EventSystem 필수**: EventSystem 없으면 버튼·드롭다운 등 UI 인터랙션 전혀 동작 안 함. Step 3 의 MUIP Canvas.prefab 사용 시 자동 포함됨.

## 목표
- 새 씬 `Assets/Scenes/TacticScene.unity` 생성
- TacticController 컴포넌트를 호스트하는 루트 GameObject
- MUIP Dropdown / HorizontalSelector / Button×2 인스턴스 배치
- TacticController 의 5 개 SerializeField 와이어링
- Build Settings 에 씬 등록

---

## Step 1 — 새 씬 생성
1. `File → New Scene` → 템플릿: **Empty (Built-in)** 또는 **Basic (Built-in)** 둘 중 하나 선택. (URP 템플릿이 떠도 무관 — 어차피 UI Overlay 라 카메라 옵션 무관.)
2. `File → Save As` → 경로 `Assets/Scenes/TacticScene.unity` 로 저장.

## Step 2 — 기본 오브젝트 확인
- Hierarchy 에 `Main Camera` 존재 확인 (없으면 `GameObject → Camera` 로 추가).
- ⚠️ EventSystem 은 Step 3 의 MUIP Canvas.prefab 에 포함됨. 여기서 별도 추가 불필요.

## Step 3 — Canvas 생성 (MUIP Canvas.prefab 사용)

1. Project 창에서 `Assets/Imported/Modern UI Pack/Prefabs/Other/Canvas.prefab` 찾기.
2. Hierarchy 에 **드래그**.
   - **Canvas + EventSystem 이 자동 포함**됨. Canvas 설정 (1920×1080, Scale With Screen Size, Match 0.5) 도 이미 올바르게 구성됨.
3. 생성된 루트 이름이 `Canvas` 인지 확인 (다르면 이름 변경).
4. ⚠️ 기존에 Hierarchy 에 EventSystem 이 이미 있으면 중복 제거 (1개만 있어야 함).

## Step 4 — TacticRoot (컨트롤러 호스트)
1. Hierarchy 우클릭 → `Create Empty`. 이름 `TacticRoot`.
2. `TacticRoot` 선택 → Inspector → `Add Component` 버튼 → 검색창에 `TacticController` 입력 → 해당 스크립트 클릭해 추가.
   - 추가 후 Inspector 에 `Tactic Controller` 컴포넌트가 보이며 5 개 슬롯 (`Title Text` / `Formation Dropdown` / `Mentality Selector` / `Save Button` / `Back Button`) 이 `None` 으로 표시됨. **Step 10 에서 채울 것.**

## Step 5 — (선택) Background
- Canvas 자식 우클릭 → `UI → Image` → 이름 `Background`.
- RectTransform Anchor presets: **stretch-stretch** (preset 메뉴에서 우하단 stretch 아이콘. Alt 누른 채 클릭하면 offset 도 자동 0).
  - Left/Right/Top/Bottom 모두 `0` 확인.
- `Image` 컴포넌트 → `Color` = `#1A1A1A` (어두운 회색), Alpha 255.

## Step 6 — Header / TitleText
1. Canvas 자식 우클릭 → `UI → Text - TextMeshPro` → 이름 `TitleText`.
   - "Import TMP Essentials" 다이얼로그가 뜨면 `Import TMP Essentials` 클릭.
2. RectTransform:
   - Anchor preset = **top-center** (Alt 누른 채로 클릭하면 pivot 도 자동 위쪽).
   - Pos Y = `-80` (음수 = Canvas 위쪽에서 아래로 80px).
   - Width = `600`, Height = `60`.
3. `TextMeshPro - Text (UI)` 컴포넌트:
   - `Text` = `Tactic` (어차피 컨트롤러가 런타임에 로컬라이즈로 덮어씀 — 자리표시자).
   - `Font Size` = `48`.
   - `Alignment` = 가로 `Center`, 세로 `Middle`.

## Step 7 — FormationRow + Dropdown 인스턴스
1. Canvas 자식 우클릭 → `Create Empty` → 이름 `FormationRow`.
2. RectTransform:
   - Anchor preset = **top-center**.
   - Pos Y = `-200`, Width = `600`, Height = `50`.
3. **드롭다운 인스턴스 추가**:
   - Project 창에서 다음 경로의 프리팹을 찾는다:
     `Assets/Imported/Modern UI Pack/Prefabs/Dropdown/Dropdown.prefab`
   - 해당 prefab 을 Hierarchy 의 `FormationRow` 위로 **드래그** (자식으로 들어감).
   - 드래그된 새 인스턴스를 선택 → Inspector 의 **`Custom Dropdown`** 컴포넌트:
     - **`Init At Start`** 체크박스 → **체크 해제** (false). ⚠️ 중요 — 컨트롤러가 Start 에서 채우므로 prefab 의 Awake 자동 초기화는 끔.
     - **`Enable Icon`** → **체크 해제** (false). 포메이션 이름만 표시.
     - 나머지 설정은 prefab 기본값 그대로.

## Step 8 — MentalityRow + HorizontalSelector 인스턴스
1. Canvas 자식 우클릭 → `Create Empty` → 이름 `MentalityRow`.
2. RectTransform:
   - Anchor preset = **top-center**.
   - Pos Y = `-300`, Width = `600`, Height = `80`.
3. **셀렉터 인스턴스 추가**:
   - Project 창에서 다음 prefab 찾기:
     `Assets/Imported/Modern UI Pack/Prefabs/Horizontal Selector/Horizontal Selector.prefab`
   - Hierarchy 의 `MentalityRow` 위로 **드래그**.
   - 인스턴스 선택 → Inspector 의 **`Horizontal Selector`** 컴포넌트:
     - **`Enable Icon`** → **체크 해제** (false). 멘탈리티는 아이콘 없음.
     - `Enable Indicators` 는 그대로 `true` (7 개 점 표시).
     - 나머지 그대로.

## Step 9 — Footer + Save / Back 버튼
1. Canvas 자식 우클릭 → `Create Empty` → 이름 `Footer`.
2. RectTransform:
   - Anchor preset = **bottom-center**.
   - Pos Y = `80`, Width = `500`, Height = `60`.
3. `Footer` 선택 후 `Add Component` → `Horizontal Layout Group`:
   - `Child Alignment` = `Middle Center`
   - `Spacing` = `20`
   - `Child Force Expand`: Width / Height 모두 체크 해제 (각 버튼이 자기 크기 유지).
4. **버튼 인스턴스 2 개 추가**:
   - 사용할 prefab: `Assets/Imported/Modern UI Pack/Prefabs/Button/Basic - Outline/Standard.prefab`
     ⚠️ "Basic" 폴더(`Basic/Standard.prefab`) 아님! 반드시 **"Basic - Outline"** 폴더. 컨트롤러가 요구하는 `ButtonManager` 타입은 Outline 변형에만 들어있음.
   - 이 prefab 을 Hierarchy 의 `Footer` 위로 **두 번** 드래그.
   - 첫 번째 인스턴스 이름 변경: `SaveButton`.
   - 두 번째 인스턴스 이름 변경: `BackButton`.
   - 두 버튼 모두 ButtonManager 의 `Button Text` 필드는 prefab 기본값 그대로 (컨트롤러가 런타임에 로컬라이즈된 "저장"/"뒤로" 로 덮어씀).

## Step 10 — ⭐ TacticController 와이어링 (가장 중요)
1. Hierarchy 에서 **`TacticRoot`** 선택.
2. Inspector 의 **`Tactic Controller`** 컴포넌트의 5 개 필드를 다음과 같이 채운다 — Hierarchy 의 해당 GameObject 를 Inspector 의 슬롯으로 **드래그**:

   | TacticController 필드 | 드래그할 Hierarchy 오브젝트 |
   |---|---|
   | `Title Text` | `Canvas/TitleText` |
   | `Formation Dropdown` | `Canvas/FormationRow/Dropdown` (인스턴스 루트) |
   | `Mentality Selector` | `Canvas/MentalityRow/Horizontal Selector` (인스턴스 루트) |
   | `Save Button` | `Canvas/Footer/SaveButton` |
   | `Back Button` | `Canvas/Footer/BackButton` |

   ⚠️ 각 인스턴스의 **루트 GameObject** 자체를 드래그할 것 (자식 오브젝트나 개별 컴포넌트가 아니라). Unity 가 자동으로 해당 GameObject 의 컴포넌트 (`CustomDropdown`/`HorizontalSelector`/`ButtonManager`/`TMP_Text`) 를 찾아 바인딩한다.

3. 와이어링 후 TacticController 의 5 개 필드 모두 슬롯에 GameObject 이름이 표시되어 있어야 함 (`None` 이 하나도 없어야 함).

## Step 11 — Build Settings 등록
1. `File → Build Settings` 창 열기.
2. 현재 TacticScene 이 열려 있는 상태에서 `Add Open Scenes` 버튼 클릭.
3. `Scenes In Build` 목록 끝에 `Scenes/TacticScene` 이 추가됨 (인덱스는 11 또는 그 시점의 마지막). 그대로 둠.
4. 창 닫기 (`Close`).

## Step 12 — 저장
- `Ctrl+S` 로 씬 저장.

---

## 검증 체크리스트 (작업 완료 후 모두 확인)

- [ ] `Assets/Scenes/TacticScene.unity` 파일 존재.
- [ ] `Build Settings → Scenes In Build` 목록에 `Scenes/TacticScene` 포함.
- [ ] `TacticRoot` 선택 시 Inspector 의 `Tactic Controller` 5 개 필드가 모두 `None` 아닌 GameObject 가 바인딩됨.
- [ ] FormationRow 의 Dropdown 인스턴스: `Custom Dropdown` 컴포넌트에서 `Init At Start = false`, `Enable Icon = false`.
- [ ] MentalityRow 의 Horizontal Selector 인스턴스: `Horizontal Selector` 컴포넌트에서 `Enable Icon = false`.
- [ ] Footer 의 두 버튼 (`SaveButton`, `BackButton`) 모두 **`ButtonManager`** 컴포넌트가 붙어있음 (Inspector 확인). `ButtonManagerBasic` 이 붙어있으면 잘못된 prefab 을 쓴 것 — 삭제 후 Step 9 의 정확한 경로로 재배치.
- [ ] Hierarchy 구조 (대략):
  ```
  TacticScene
  ├── Main Camera
  ├── EventSystem
  ├── TacticRoot          (← TacticController 컴포넌트)
  └── Canvas              (Overlay, 1920×1080, Match 0.5)
      ├── Background      (optional)
      ├── TitleText       (TMP_Text)
      ├── FormationRow
      │   └── Dropdown    (CustomDropdown 인스턴스)
      ├── MentalityRow
      │   └── Horizontal Selector  (HorizontalSelector 인스턴스)
      └── Footer          (HorizontalLayoutGroup)
          ├── SaveButton  (ButtonManager 인스턴스)
          └── BackButton  (ButtonManager 인스턴스)
  ```
- [ ] Console 에 컴파일 에러 / 에러 0.

## 문제 발생 시 처리
1. **즉시 멈춤.** .cs 파일 / MUIP 원본 prefab 파일을 절대 수정하지 말 것.
2. 다음을 보고:
   - 발생한 에러 메시지 전문 (Console 복사).
   - 어느 Step 번호에서 막혔는지.
   - 가능하면 해당 시점의 Inspector 또는 Hierarchy 스크린샷.
3. 보고는 사용자가 Claude Code 에 전달해 다음 단계 결정.

---

## (참고) 슬라이스 동작 검증 — Unity AI 가 끝낸 후 사용자가 수동 테스트

위 작업이 끝나면 사용자가 Play 모드로 다음을 확인:
1. MainMenu → 새 게임 → Dashboard 도달.
2. **(임시)** Dashboard 에서 TacticScene 으로 가는 버튼이 아직 없으므로, Hierarchy 에서 TacticScene 을 직접 열어 Play 또는 `Build Settings` 에서 시작 씬을 TacticScene 으로 잠시 바꿔 Play.
3. Play 시 확인:
   - TitleText 가 "전술" (또는 "Tactic", 언어 설정에 따라).
   - Formation 드롭다운에 6 개 포메이션 표시, 현재 클럽의 포메이션이 기본 선택.
   - Mentality 셀렉터에 7 개 라벨 ("매우 수비적" ~ "매우 공격적"), 현재 클럽 mentality 가 기본.
   - Save 버튼 텍스트 "저장", Back 버튼 텍스트 "뒤로".
   - Save 클릭 → DashboardScene 로 이동 + club.tactic 의 formationId / mentality 가 선택값으로 반영됨.
   - Back 클릭 → DashboardScene 로 이동 (변경 미반영).
