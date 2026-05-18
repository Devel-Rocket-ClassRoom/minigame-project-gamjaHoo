# Algorithms

각 알고리즘의 정확한 명세. 구현 시 이 문서를 참조하여 작성한다.

밸런싱 수치는 코드에 박지 않고 `GameBalanceSO`로 외부화한다.

---

## Template

새 알고리즘 추가 시 아래 템플릿 사용.

```markdown
## N. Algorithm Name

### Purpose
- 무엇을 계산하는가
- 어떤 시점에 호출되는가

### Inputs
- 필요한 데이터

### Outputs
- 반환 데이터

### Logic
의사코드 또는 단계별 설명.

### Balancing Parameters (→ GameBalanceSO)
- 외부화될 수치들

### Edge Cases
- 0 나누기, 음수, 클램프 등

### Test Scenarios
- 정상 입력/결과 예시
```

---

## Priority Order

V0.1 시작 전 정해야 할 알고리즘 (우선순위):

1. **선수 생성 (Player Generation)** ★★★★★
2. **경기 결과 계산 (Match Simulation)** ★★★★★
3. **선수 가치 계산 (Market Value)** ★★★★
4. **유스 풀 생성 (Youth Pool Generation)** ★★★★

V0.1 진행 중 정해도 OK:

5. 선수 성장 (Player Growth)
6. 시즌 사이클 처리
7. AI 구단 의사결정 (단순 버전)

V1.0 들어갈 때:

8. 부상 시스템
9. 사기/불만 계산
10. 보드 신뢰도
11. 경기 텍스트 이벤트
12. 전술 상성
13. 스카우팅 알고리즘 상세

---

## 1. Player Generation

*(아직 작성되지 않음 — 다음 설계 세션에서 결정)*

---

## 2. Match Simulation

*(미작성)*

---

## 3. Market Value

*(미작성)*

---

## 4. Youth Pool Generation

*(미작성)*

---

## Change Log

| Date | Section | Change |
| --- | --- | --- |
| 2025-05-15 | All | Template created, sections empty (to be filled per design session) |
