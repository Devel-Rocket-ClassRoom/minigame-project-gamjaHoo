# Unity MCP & AI Assistant 활용 가이드

이 프로젝트는 두 가지 AI 도구를 병행 사용한다:

1. **Claude Code (with Unity MCP)** — VSCode에서 코드 작성, 필요 시 Unity 에디터 조작
2. **Unity AI Assistant** — Unity 에디터 안에서 직접 사용 (씬 작업, 인스펙터 설정 등)

각 도구의 역할과 사용 원칙을 정의한다.

---

## 1. 도구별 역할 분담

### Claude Code (VSCode)

**주 용도:** 코드 작성, 리팩터링, 명세 기반 구현

**적합한 작업:**
- 도메인 클래스 작성
- 시스템 / 알고리즘 구현
- 세이브/로드 / EventBus 같은 인프라
- 코드 리팩터링
- 디버그 도구 작성
- 문서 업데이트

**Unity MCP 활용:**
- Unity 에디터에서 씬 정보 / 게임오브젝트 조회
- SO 인스턴스 자동 생성
- 컴파일 에러 확인
- 기본 프리팹 구성 자동화

### Unity AI Assistant (Unity 에디터)

**주 용도:** Unity 에디터 작업, 시각적 작업

**적합한 작업:**
- UI 레이아웃 구성
- 프리팹 셋업 / 컴포넌트 연결
- 씬 구성 (게임오브젝트 배치)
- 머터리얼 / 라이팅
- ScriptableObject 인스턴스 데이터 입력
- 인스펙터 기반 디버깅

---

## 2. 작업 분담 가이드

| 작업 종류 | 권장 도구 | 이유 |
| --- | --- | --- |
| 새 클래스 작성 | Claude Code | 명세 기반 코드 |
| 알고리즘 구현 | Claude Code | algorithms.md 참조 필요 |
| 리팩터링 | Claude Code | 코드 일관성 |
| UI 화면 만들기 | Unity AI Assistant | 인스펙터 기반 작업 |
| 프리팹 셋업 | Unity AI Assistant | 시각적 작업 |
| SO 에셋 만들기 | 둘 다 (Claude Code로 생성 후 데이터 입력) | |
| 디버그 윈도우 | Claude Code | 코드 위주 |
| 씬 구성 | Unity AI Assistant | 게임오브젝트 배치 |
| 머터리얼 / 셰이더 | Unity AI Assistant | Unity 종속 |

### 협업 예시

```
1. [Claude Code] PlayerGenerator.cs 작성
2. [Claude Code] GameBalanceSO.cs 정의
3. [Unity AI Assistant] GameBalanceSO 인스턴스 생성 + 수치 입력
4. [Unity AI Assistant] 디버그 씬 구성, 테스트
5. [Claude Code] 발견된 버그 수정
```

---

## 3. Claude Code + Unity MCP 사용 원칙

### 원칙 1: 코드는 Claude Code 주도

Unity MCP가 코드를 직접 작성 가능하더라도, **소스 파일 작성은 Claude Code에서 한다.** 이유:

- Git 추적 일관성
- 코딩 컨벤션 (coding-conventions.md) 적용
- 문서와 동기화 (명세 → 코드)

Unity MCP는 보조 도구로만:
- 씬 정보 조회 (디버깅용)
- SO 인스턴스 생성 (코드 자체는 Claude Code에서 작성한 SO 클래스 기반)
- 컴파일 상태 확인

### 원칙 2: 작은 단위로 위임

Karpathy 원칙 그대로 적용:

```
❌ "Unity에 게임 전체 만들어줘"
✅ "PlayerGenerator.cs 작성한 다음, Resources/Data/Balance/ 에 GameBalanceSO 인스턴스 하나 생성해줘"
```

### 원칙 3: Unity MCP 호출 전 확인

Unity 에디터 상태를 변경하는 작업 (씬 저장, 컴포넌트 추가 등)은:

1. 사용자에게 의도 확인
2. 변경 전 백업 (씬 저장 등)
3. 변경 후 결과 보고

### 원칙 4: 에디터 의존 작업 분리

Unity 에디터에서만 가능한 작업 (Asset Database 갱신, 인스펙터 설정 등)은 Unity AI Assistant로 위임 권장. Claude Code에서 무리하게 자동화하지 말 것.

---

## 4. Unity AI Assistant 사용 원칙

### 원칙 1: 코드 자동 생성 자제

Unity AI Assistant가 스크립트 생성을 제안해도, 새 스크립트 생성은 Claude Code에서. 이유:

- 코딩 컨벤션 일관성
- 폴더 위치 / 네임스페이스 일관성
- 명세 기반 작성 보장

**예외:** UI 컨트롤러처럼 매우 간단한 MonoBehaviour는 Unity AI Assistant로 OK. 단, coding-conventions.md 준수 확인.

### 원칙 2: 인스펙터 작업에 활용

- 프리팹 컴포넌트 연결
- ScriptableObject 데이터 입력
- 씬 게임오브젝트 배치
- 머터리얼 설정

이런 작업은 Unity AI Assistant가 효율적.

### 원칙 3: 디자인 결정은 사람이

Unity AI Assistant가 "이런 레이아웃 어떠세요?" 제안해도, **UI / 게임 디자인 결정은 사람이 한다.** AI는 작업 실행만.

---

## 5. 두 도구 간 컨텍스트 동기화

### 문제

Claude Code와 Unity AI Assistant는 서로 메모리를 공유하지 않는다.

### 해결

**1. 단일 진실의 원천(SoT) 유지**

- 코드 = Git 저장소
- 디자인 결정 = `docs/design-decisions.md`
- 알고리즘 = `docs/algorithms.md`
- 작업 진행도 = GitHub Issues + `docs/v0.1-tasks.md`

두 도구 모두 위 SoT를 참조.

**2. 도구 간 핸드오프**

```
[Claude Code] 작업 완료
  ↓
GitHub Issue 업데이트 + 문서 갱신 + 커밋
  ↓
[Unity AI Assistant] 작업 시작 시
  - 최신 커밋 pull
  - 관련 문서 확인
  - 작업 진행
```

**3. 결정사항은 즉시 문서로**

Unity AI Assistant 사용 중 새 결정이 발생하면:
- `design-decisions.md`에 추가
- 다음 Claude Code 세션에서 인식

---

## 6. 실용 예시

### 예시 1: PlayerGenerator 구현 + 테스트

```
[Claude Code]
1. algorithms.md #1 명세 확인
2. PlayerGenerator.cs 작성
3. GameBalanceSO 필드 추가
4. 단위 테스트 작성 (선택)
5. 커밋 + 이슈 close

[Unity AI Assistant]
6. GameBalanceSO 인스턴스 생성
7. 인스펙터에서 수치 입력
8. 테스트 씬에서 PlayerGenerator 호출
9. 결과 확인
```

### 예시 2: 유스 풀 UI 화면

```
[Claude Code]
1. YouthPoolView.cs (컨트롤러) 작성
2. EventBus 구독 / 데이터 바인딩 로직
3. 커밋

[Unity AI Assistant]
4. UI 프리팹 생성 (Canvas, Panel, ScrollView 등)
5. YouthPoolView 컴포넌트 연결
6. 필드 참조 설정 (인스펙터)
7. 씬에 배치
8. 플레이 모드 확인
```

### 예시 3: 디버그 씬 셋업

```
[Unity AI Assistant]
1. 빈 씬 생성: Debug_Test.unity
2. GameManager 게임오브젝트 배치
3. 디버그 UI 추가 (테스트 버튼들)
4. 씬 저장

[Claude Code]
5. 디버그 윈도우 코드 보강 (필요 시)
```

---

## 7. 주의사항

### Unity MCP 비활성화 시 대응

Unity MCP가 연결되지 않은 상태에서 Claude Code 작업 시:
- Unity 에디터 조작 작업은 사용자에게 위임
- Claude Code는 코드 작성에 집중
- 사용자가 Unity에서 수동 작업 후 결과 보고

### Asset Database 충돌 방지

Unity가 열려있는 상태에서 외부 도구가 파일 시스템을 변경하면 충돌 가능. 해결:
- 큰 변경 시 Unity 닫고 작업
- 또는 변경 후 Unity에서 `Assets → Refresh`

### 두 AI 동시 사용 금지

같은 시점에 Claude Code와 Unity AI Assistant가 같은 파일을 변경하면 충돌. 한 번에 하나만.

---

## 8. 워크플로우 요약

```
설계 / 의사결정
    ↓
채팅 인터페이스 (Claude) → docs/ 업데이트
    ↓
코드 작성
    ↓
Claude Code (with Unity MCP) → Git 커밋
    ↓
Unity 에디터 작업 (씬, 프리팹, SO 데이터)
    ↓
Unity AI Assistant → Asset 변경 → Git 커밋
    ↓
플레이 테스트
    ↓
발견 사항 → docs/ 업데이트 → 새 이슈
```

---

## Change Log

| Date | Change |
| --- | --- |
| 2025-05-15 | 초안 작성 |
