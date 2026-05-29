# Transfer / Negotiation 뒤로 가기 버튼 wiring 검증 — Unity AI 작업 지시서

> Unity AI Assistant 에 그대로 전달.
> **`.cs` 파일 절대 손대지 말 것.** 컴파일 에러 → 즉시 멈추고 보고.

---

## 컨텍스트

- 프로젝트: FM-Lite (Unity 6, 축구 매니저). UI 에셋 `Assets/Imported/Modern UI Pack/` (MUIP).
- **배경**:
  - V0.5 플레이테스트에서 Transfer / Negotiation 씬의 [뒤로] 버튼이 작동 안 함 — 사용자가 그 씬에 갇힘.
  - 코드에는 `TransferController.OnBackClicked()` / `NegotiationController.OnBackClicked()` 둘 다 정의되어 있음 (이슈 #384).
  - 원인 추정: 씬의 Back 버튼 GameObject 의 `Button.onClick` 이벤트가 해당 컨트롤러 메서드에 wire 되어 있지 않음 (Unity AI 작업 시 누락).
- **이번 작업**: 두 씬의 Back 버튼 wiring 검증 + 필요 시 wire 추가. 코드는 손대지 않음.

### 코드 참조 (Unity AI 가 참고만 — 수정 X)

- `Assets/_Project/Scripts/UI/TransferController.cs` 187: `public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);`
- `Assets/_Project/Scripts/UI/NegotiationController.cs` 99: `public void OnBackClicked() => SceneManager.LoadScene(TransferScene);`

---

## 절대 규칙

1. **`.cs` 파일 절대 금지.** 컴파일 에러 / Missing Script 가 보여도 .cs 손대지 말고 즉시 멈춰 보고.
2. **MUIP 원본 prefab 수정 금지**.
3. 작업 후 `Ctrl+S` 로 저장.

---

## Step 1 — TransferScene 의 Back 버튼 확인 + wiring

1. `Assets/Scenes/TransferScene.unity` 열기.
2. Hierarchy 에서 [뒤로] (또는 Back / 뒤로가기) 버튼 GameObject 찾기. 위치 추정: Canvas 상단 좌측 또는 하단.
3. 버튼 선택 → Inspector 의 `Button` 컴포넌트의 `On Click ()` 리스트 확인.
4. **wiring 상태별 처리**:
   - (a) 리스트가 **비어있음** → [+] 추가 → 첫 칸에 `TransferRoot` (또는 `TransferController` 컴포넌트 보유 GameObject) 드래그 → 두 번째 칸 드롭다운에서 `TransferController.OnBackClicked` 선택.
   - (b) 리스트에 항목 있는데 **함수 미선택 (No Function)** → 같은 드롭다운에서 `TransferController.OnBackClicked` 선택.
   - (c) **이미 `TransferController.OnBackClicked` 가 연결되어 있음** → 정상. Step 2 진입.
5. Play 모드 진입 → Dashboard → [이적] → TransferScene 진입 → [뒤로] 클릭 → DashboardScene 으로 복귀 확인.

> ℹ️ 버튼 GameObject 이름은 `BackButton` / `Btn_Back` / `뒤로` 등 다양할 수 있음. Inspector 의 컴포넌트 구조 (`RectTransform` + `Button` + `Image` + 텍스트 자식) 로 식별.

---

## Step 2 — NegotiationScene 의 Back 버튼 확인 + wiring

1. `Assets/Scenes/NegotiationScene.unity` 열기.
2. Hierarchy 에서 [뒤로] 버튼 GameObject 찾기.
3. 버튼 선택 → Inspector 의 `Button` 컴포넌트의 `On Click ()` 리스트 확인.
4. **wiring 상태별 처리**:
   - (a) 리스트가 **비어있음** → [+] 추가 → 첫 칸에 `NegotiationRoot` (또는 `NegotiationController` 컴포넌트 보유 GameObject) 드래그 → 두 번째 칸 드롭다운에서 `NegotiationController.OnBackClicked` 선택.
   - (b) 리스트에 항목 있는데 **함수 미선택 (No Function)** → 같은 드롭다운에서 `NegotiationController.OnBackClicked` 선택.
   - (c) **이미 `NegotiationController.OnBackClicked` 가 연결되어 있음** → 정상. Step 3 진입.
5. Play 모드 진입 → Dashboard → [이적] → 오퍼 제출 → 며칠 진행 → NegotiationScene 자동 진입 → [뒤로] 클릭 → TransferScene 으로 복귀 확인.

---

## Step 3 — Build Settings 확인 (씬 등록)

1. `File → Build Profiles` (또는 `File → Build Settings`) 열기.
2. Scenes In Build 리스트에 다음이 있어야 함:
   - `Scenes/TransferScene` (체크 박스 ON)
   - `Scenes/NegotiationScene` (체크 박스 ON)
   - `Scenes/DashboardScene` (체크 박스 ON)
3. 없으면 [Add Open Scenes] 또는 해당 .unity 파일을 드래그.

---

## Step 4 — 씬 저장

각 씬에서 `Ctrl+S`.

---

## 검증 체크리스트

- [ ] TransferScene 의 [뒤로] 버튼 `On Click ()` 에 `TransferController.OnBackClicked` 연결됨.
- [ ] NegotiationScene 의 [뒤로] 버튼 `On Click ()` 에 `NegotiationController.OnBackClicked` 연결됨.
- [ ] Build Settings 에 TransferScene / NegotiationScene / DashboardScene 등록됨.
- [ ] Console 컴파일 에러 0.

---

## (참고) Play 모드 동작 검증 — Unity AI 작업 끝난 후 사용자 수동 테스트

1. New Game → Dashboard → [이적] → TransferScene 진입.
2. [뒤로] 클릭 → DashboardScene 복귀 ✅.
3. Transfer 에서 검색 → 선수 선택 → 오퍼 제출 → Dashboard 로 복귀 (자동 또는 수동).
4. [Continue] → 1~2일 진행 → AI 응답 (CounterOffer) 도착 → Dashboard 자동 NegotiationScene 으로 라우팅 (#384 fix 후).
5. NegotiationScene 에서 [뒤로] 클릭 → TransferScene 복귀 ✅.

---

## 문제 발생 시 처리

1. **즉시 멈춤.** `.cs` 또는 MUIP 원본 prefab 절대 수정 X.
2. 에러 메시지 + 어느 Step 에서 막혔는지 + 가능하면 Inspector 스크린샷 보고.
3. 보고는 사용자가 Claude Code 에 전달해 다음 단계 결정.
