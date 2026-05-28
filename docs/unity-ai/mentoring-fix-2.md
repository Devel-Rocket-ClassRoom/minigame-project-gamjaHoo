# MentoringScene 수정 명령 2 — Unity AI 작업 지시서

> **`.cs` 파일 절대 금지.**

---

## Fix 1 — MenteeToggleItem 목록이 흰 화면으로 보이는 현상 수정

**문제**: CreatePanel 에서 MenteeScrollView 안에 멘티 토글 항목들이 생성됐지만 흰 직사각형으로만 보임. 이름 텍스트가 안 보임.

**원인**: MUIP Toggle 언팩 후 각 토글 아이템이 흰색 배경(Image) 위에 흰색 텍스트로 배치되어 대비가 없음.

### 1-A. MenteeScrollView 배경을 어둡게

1. Hierarchy 에서 `Canvas/CreatePanel/PanelBox/MenteeScrollView` 선택.
2. Inspector 에서 Image 컴포넌트 확인 (있으면):
   - Color: `#1E1E2E`, Alpha `255`
3. Image 없으면 `Add Component → Image` → Color: `#1E1E2E`, Alpha `255`.
4. `MenteeScrollView/Viewport` 선택 → Inspector:
   - Image 컴포넌트가 있으면 Color Alpha를 `0` (완전 투명) 으로 설정.
   - 또는 Image 컴포넌트 왼쪽 체크박스 해제 (disable).

### 1-B. MenteeToggleItem.prefab 배경 + 텍스트 색 수정

1. Project 창에서 `Assets/Imported/FMLite UI/Prefabs/MenteeToggleItem.prefab` 더블클릭 → **Prefab 편집 모드** 진입.

2. **루트 오브젝트** (`MenteeToggleItem`) 선택 → Inspector:
   - Image 컴포넌트가 있으면:
     - Color: `#2C2C3E`, Alpha `220`
   - Image 컴포넌트가 없으면:
     - `Add Component → Image` → Color: `#2C2C3E`, Alpha `220`

3. Hierarchy 에서 `MenteeToggleItem` 펼치기 → `NameLabel` 오브젝트 찾아 선택.
4. Inspector 의 `TextMeshPro - Text (UI)` 컴포넌트:
   - Color: **White** (`#FFFFFF`), Alpha `255`
   - Font Size: `26`
   - Alignment: 가로 `Left`, 세로 `Middle`

5. **Prefab 편집 모드 저장**: 상단 `<` 화살표 클릭 (씬으로 복귀) → 다이얼로그가 뜨면 **Save**.

### 1-C. MentoringScene.unity 에서 확인

1. `Assets/Scenes/MentoringScene.unity` 열기 (또는 이미 열려있으면 그대로).
2. Play 모드 진입 → "+ 그룹 만들기" 버튼 클릭 → 멘티 목록에 선수 이름이 보이는지 확인.

---

## Fix 2 — (추가 확인) NameLabel 오브젝트 없는 경우

만약 1-B 에서 `NameLabel` 오브젝트를 찾을 수 없다면 (Unity AI 가 이름을 다르게 지었을 수 있음):

1. Prefab 편집 모드에서 Hierarchy 를 모두 펼치기.
2. `TMP_Text` 컴포넌트가 붙어있는 자식 오브젝트를 찾는다 (이름이 `Label`, `Toggle Label`, `Text` 등일 수 있음).
3. 그 오브젝트 이름을 `NameLabel` 로 변경.
4. TMP_Text 컴포넌트 → Color: White, Font Size: `26`, Alignment: Middle Left.
5. Save.

---

## 검증 체크리스트

- [ ] Play 모드 → "+ 그룹 만들기" 클릭 → CreatePanel 열림.
- [ ] 멘티 선택 목록에 선수 이름이 (이름 + 나이 형식으로) 여러 명 표시됨.
- [ ] 토글 체크박스를 클릭하면 선택/해제 시각 피드백 있음.
- [ ] 멘티 1~3명 선택 후 "그룹 생성" 클릭 → 그룹 목록에 추가됨.
- [ ] Console 에러 0.
