# Coding Conventions

## Language

- C# 9.0+ (Unity 2021.3 LTS 이상)
- 한국어 주석 OK, 식별자는 영어

## Naming

### Casing

| 종류 | 규칙 | 예시 |
| --- | --- | --- |
| Class / Struct / Enum | PascalCase | `Player`, `MatchResult` |
| Interface | I + PascalCase | `ISimulator` |
| Method | PascalCase | `CalculateValue()` |
| Public field/property | PascalCase | `public int Money` |
| Private field | camelCase | `private int money` |
| Local variable | camelCase | `var clubName = ...` |
| Constant | PascalCase | `const int MaxTokens = 5` |
| ScriptableObject 클래스 | XxxSO 접미사 | `GameBalanceSO`, `TraitSO` |
| Event 클래스 | XxxEvent 접미사 | `MatchFinishedEvent` |

### Underscore 사용

- private 필드는 underscore 없음 (camelCase)
- `[JsonIgnore]` 내부 인덱스 캐시는 `_` 접두사 허용 (`_playerById`)

### Boolean

- `is`, `has`, `can`, `should`로 시작
- `isActiveSimulation`, `hasInjury`, `canRenewContract`

## File Organization

### One Class Per File

원칙적으로 한 파일 = 한 클래스. 작은 enum / 값 객체는 같은 파일에 둘 수 있음.

### File Name = Class Name

`Player.cs`에 `class Player`. 헷갈리게 하지 말 것.

### Folder Structure

```
Assets/_Project/Scripts/
├─ Core/              GameManager, GameTime, EventBus 등 기반 인프라
├─ Domain/            데이터 클래스 (Player, Club, Match 등)
├─ Application/       시스템 (MatchSimulator, TransferSystem 등)
├─ Persistence/       Save/Load
├─ UI/                UI Controllers
├─ Utils/             범용 유틸
└─ Editor/            커스텀 에디터 (빌드 제외)
```

## Code Style

### Properties vs Fields

- 외부 노출은 가능한 한 property
- 단순 데이터 컨테이너(`Stats`, `Contract` 등)는 public field 허용 (직렬화 편의)

### `var` 사용

- 우변에서 타입이 명확하면 `var` 사용
- 명확하지 않으면 명시적 타입

```csharp
// OK
var player = new Player();
var players = state.allPlayers;

// 명시적 타입이 나음
List<int> ids = GetIds();
```

### Expression-bodied members

단순한 경우에만:

```csharp
public int Age => DateTime.Now.Year - birthYear;
```

복잡한 로직이면 일반 메서드.

### LINQ

- 가독성 우선. 짧고 명확하면 OK
- 핫 패스(매 프레임, 매일 진행 시 호출)에서는 LINQ 자제, 명시적 `for` 사용
- 검색 같은 1회성 작업은 LINQ 적극 활용

## Domain Class Patterns

### Serializable

세이브 대상 도메인 클래스는 `[Serializable]`:

```csharp
[Serializable]
public class Player {
    public int id;
    public PersonalInfo info;
    // ...
}
```

### ID Reference

다른 도메인 객체 참조는 ID로만 (`design-decisions.md` 1번 참조).

### Helper Method Location

- 단일 객체 내부에서 완결되는 계산 → 해당 클래스의 메서드
- 여러 객체 / GameState 필요 → 시스템 클래스로

```csharp
// OK: Player 내부에서 완결
public int GetAge() => (DateTime.Now - birthDate).Days / 365;

// 시스템으로 빼야 함: Club과 Player 둘 다 필요
// MatchSimulator.Simulate(Match match, GameState state)
```

## System Class Patterns

### Stateless

시스템은 필드를 가지지 않는다. 입력으로 받고 출력으로 변경.

```csharp
// ✅
public class MatchSimulator {
    public MatchResult Simulate(Match match, GameState state) { ... }
}

// ❌
public class MatchSimulator {
    private GameState state;  // 안 됨
    public MatchResult Simulate(Match match) { ... }
}
```

예외: `GameManager` 자체는 싱글톤이라 상태 보유 OK.

### Side Effects via EventBus

시스템이 게임 상태를 변경하면 이벤트 발행:

```csharp
public class MatchSimulator {
    public MatchResult Simulate(Match match, GameState state) {
        var result = ComputeResult(match, state);
        ApplyResult(match, result, state);
        EventBus.Publish(new MatchFinishedEvent(match));
        return result;
    }
}
```

UI는 이벤트 구독해서 갱신.

## ScriptableObject Patterns

### Naming

`XxxSO` 접미사. 파일도 `XxxSO.cs`.

### CreateAssetMenu

만들 수 있는 SO엔 항상 `[CreateAssetMenu]`:

```csharp
[CreateAssetMenu(fileName = "NewTrait", menuName = "FM-Lite/Trait")]
public class TraitSO : ScriptableObject { ... }
```

### SO 참조 — ID 사용

도메인 클래스가 SO 참조 시 SerializableReference 대신 ID 사용:

```csharp
public class Player {
    public List<int> traitIds;  // TraitSO의 ID
}

// 조회
TraitSO trait = GameDatabase.GetTrait(player.traitIds[0]);
```

## Comments

### When to Comment

- 의도/이유 설명 ("why", not "what")
- 비명백한 결정의 근거
- 외부 시스템과의 계약
- TODO / FIXME 명시

### When NOT to Comment

- 코드가 자명한 경우
- 변수 이름으로 표현 가능한 경우

```csharp
// ❌ 의미 없음
i++; // increment i

// ✅ 의미 있음
i++; // 박싱데이는 추가 부상 페널티 적용을 위해 별도 카운트
```

### XML Docs

public API에는 XML doc 권장:

```csharp
/// <summary>
/// 선수의 시장 가치를 산출. CA, PA, 나이, 명성, 계약 잔여 기간을 종합.
/// </summary>
public int CalculateMarketValue(Player p, GameState state) { ... }
```

## Error Handling

### When to Throw

- 프로그래밍 오류 (호출 측 잘못)
- 절대 일어나면 안 되는 상태

```csharp
if (player == null)
    throw new ArgumentNullException(nameof(player));
```

### When to Return Null/False

- 정상 흐름의 일부인 실패
- "찾기" 같은 작업

```csharp
public Player FindPlayer(int id) {
    if (_playerById.TryGetValue(id, out var p)) return p;
    return null;
}
```

### NEVER Silent Catch

```csharp
// ❌
try { ... } catch { }

// ✅
try { ... }
catch (Exception e) {
    Debug.LogError($"Failed to load save: {e}");
    throw;
}
```

## Magic Numbers

매직 넘버 금지. SO 외부화 또는 const:

```csharp
// ❌
if (player.age >= 33 && Random.value < 0.15f) Retire(player);

// ✅
if (player.age >= balance.retirementMinAge
    && Random.value < balance.retirementProbability)
    Retire(player);
```

## File Header

새 파일 생성 시 한 줄 설명만:

```csharp
// MatchSimulator.cs
// 경기 시뮬레이션 시스템. 경기 시작 직전 시드 고정 후 결과 산출.

using ...;

namespace FMLite.Application {
    public class MatchSimulator { ... }
}
```

긴 헤더 / 라이선스 / 작성자 정보 불필요 (1인 프로젝트).

## Don'ts

- ❌ Singleton 남용 (GameManager 외엔 자제)
- ❌ `MonoBehaviour` 남용 (Domain 클래스는 일반 C# 클래스)
- ❌ 매직 넘버
- ❌ TODO만 남기고 미해결 (이슈 트래커 또는 design-decisions.md에 기록)
- ❌ 깊은 상속 (3단계 이상 금지)
- ❌ 거대한 메서드 (50줄 이상이면 분리 고려)
- ❌ `Update()`에서 매 프레임 LINQ (성능 핫패스)
