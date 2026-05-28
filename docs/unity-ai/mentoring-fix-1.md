# MentoringScene 수정 명령 — Unity AI 작업 지시서

> **`.cs` 파일 절대 금지.** 컴파일 에러가 나도 .cs 수정 없이 멈춰서 보고.

---

## Fix 1 — MentorDropdown 드롭다운 리스트가 아래 UI 요소에 가려지는 현상 수정

**문제**: CreatePanel 에서 MentorDropdown 을 클릭해 열면 드롭다운 리스트가 `MenteeSectionTitle` / `MenteeScrollView` / `ButtonRow` 뒤로 숨어버림.

**원인**: MUIP CustomDropdown 의 드롭다운 리스트가 부모 Canvas 와 같은 렌더 레이어에서 Hierarchy 순서에 따라 렌더링됨. 형제 오브젝트들이 더 뒤에 있으면 그 위에 그려짐.

**수정 방법**:

1. `Assets/Scenes/MentoringScene.unity` 열기.
2. Hierarchy 에서 `Canvas/CreatePanel/PanelBox/MentorDropdown` 선택.
3. Inspector → `Add Component` → `Canvas`:
   - `Override Sorting` **체크**
   - `Sorting Order` = `10`
4. 같은 `MentorDropdown` 에 → `Add Component` → `Graphic Raycaster`.
   - 기본값 그대로 (Block Mask 등 변경 불필요).
5. `Ctrl+S` 저장.

> ℹ️ Canvas 컴포넌트를 Canvas 안의 자식에 추가하면 "Sub-Canvas (Nested Canvas)" 가 됩니다.
> Sorting Order = 10 으로 설정하면 이 드롭다운 리스트가 부모 Canvas 의 모든 다른 요소보다 위에 렌더링됩니다.
> Graphic Raycaster 는 Sub-Canvas 에 버튼/토글 입력이 통하려면 반드시 필요합니다.

**검증**: Play 모드에서 CreatePanel 열기 → MentorDropdown 클릭 → 리스트가 `MenteeSectionTitle`, `MenteeScrollView` 위에 표시되는지 확인.

---

## Fix 2 — DashboardScene 멘토링 버튼 중복 제거

**문제**: DashboardScene 에 "멘토링" 버튼 또는 `MentoringButton` 이름의 오브젝트가 2개 있음.

**수정 방법**:

1. `Assets/Scenes/DashboardScene.unity` 열기.
2. Hierarchy 에서 버튼들이 있는 사이드 메뉴 컨테이너 찾기.
3. `MentoringButton` 이름의 오브젝트가 2개인지 확인.
   또는 `Button Manager` 컴포넌트의 `Button Text` = "멘토링" 인 것이 2개인지 확인.
4. 2개 중 `Button Manager → Click Event` 에 아무것도 연결 **안 된** 것을 선택해 **Delete**.
5. 남은 하나의 `Button Manager → Click Event` 에 `DashboardController.OnMentoringClicked` 연결 확인:
   - 연결이 없으면: `+` → DashboardController 오브젝트 드래그 → `DashboardController.OnMentoringClicked` 선택.
6. `Ctrl+S` 저장.

**검증**: Hierarchy 에 `MentoringButton` 이 정확히 1개인지 확인. Play 모드 → 버튼 클릭 → MentoringScene 진입 확인.

---

## 검증 체크리스트

- [ ] `MentorDropdown` 에 `Canvas` 컴포넌트 (Override Sorting = true, Sorting Order = 10) 추가됨.
- [ ] `MentorDropdown` 에 `Graphic Raycaster` 컴포넌트 추가됨.
- [ ] Play 모드 → MentorDropdown 클릭 시 리스트가 다른 UI 요소 위에 표시됨.
- [ ] DashboardScene 에 `MentoringButton` 정확히 1개.
- [ ] `MentoringButton` → `DashboardController.OnMentoringClicked` 연결됨.
- [ ] Console 에러 0.
