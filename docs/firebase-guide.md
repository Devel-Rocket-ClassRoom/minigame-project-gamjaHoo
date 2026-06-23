# Firebase 명예의 전당 — 전체 구현 가이드

> FM-Lite(축구 매니저 게임)에 **Firebase Realtime Database**를 연동해 "글로벌 명예의 전당"(감독 커리어 랭킹 + 내 역대 시즌)을 만든 과제의 **A to Z 설명서**.
> Firebase·Unity·C#을 처음 보는 사람도 이해할 수 있도록, 설정부터 코드 한 줄까지 흐름을 따라간다.

---

## 0. 한눈에 보기 — 무엇을 만들었나?

이 게임은 원래 **혼자 하는(오프라인)** 축구 매니저 게임이다. 여기에 인터넷 기능을 붙여서:

1. **감독 계정** — 이메일/비밀번호로 회원가입·로그인 (또는 로그인 없이 "게스트"로 플레이)
2. **글로벌 랭킹** — 전 세계 플레이어의 감독 커리어 점수 순위 (실시간 갱신)
3. **내 역대 시즌** — 내가 거쳐온 시즌들의 성적 기록

이 데이터들은 내 컴퓨터가 아니라 **구글의 Firebase 서버**(클라우드)에 저장된다. 그래서 다른 사람의 점수도 보이고, 내가 계정으로 로그인하면 어느 기기에서든 내 기록이 따라온다.

### 과제의 4가지 요구사항과 결과물

| 요구사항 | 구현 |
| --- | --- |
| **1. 사용자 인증** | Firebase Authentication 이메일/비밀번호 로그인·회원가입 (+ 익명 게스트) |
| **2. 데이터베이스 설계** | Realtime Database 스키마 + 보안규칙 (`docs/firebase-rtdb.md`) |
| **3. 재사용 가능한 매니저** | `FirebaseKit` — 다른 프로젝트에 폴더만 복사하면 쓰는 독립 모듈 |
| **4. 미니게임에 적용** | 명예의 전당 화면 + 시즌 종료 시 자동 업로드 |

---

## 1. 큰 그림 — 아키텍처(설계 구조)

코드는 **3개의 층(Layer)**으로 나뉜다. 위층은 아래층을 사용할 수 있지만, 아래층은 위층을 모른다(단방향 의존). 이렇게 나누는 이유는 **재사용**과 **유지보수** 때문이다.

```
┌──────────────────────────────────────────────────────────┐
│  UI 층 (FMLite.UI)                                          │
│   · HallOfFameController  (명예의 전당 화면)                  │
│   · MainMenuController     (로그인/회원가입 패널)             │
│   · HallOfFameService      (시즌 종료 → 자동 업로드)          │
└───────────────┬──────────────────────────────────────────┘
                │ 사용
┌───────────────▼──────────────────────────────────────────┐
│  어댑터 층 (FMLite.Persistence.Cloud) — 이 게임 전용          │
│   · CloudProfileRepository / LeaderboardRepository /         │
│     CareerRepository  (게임 데이터 ↔ 클라우드 경로 변환)      │
│   · DTO: UserProfile / LeaderboardEntry / SeasonRecord       │
└───────────────┬──────────────────────────────────────────┘
                │ 사용
┌───────────────▼──────────────────────────────────────────┐
│  ★ 재사용 층 (FirebaseKit) — 게임과 무관, 다른 프로젝트도 OK  │
│   · FirebaseService          (SDK 초기화 + Auth/DB 보유)      │
│   · AuthManager              (로그인/회원가입/로그아웃)        │
│   · RealtimeDatabaseService  (제네릭 DB 읽기/쓰기/실시간)      │
└──────────────────────────────────────────────────────────┘
                │ 사용
          [ Firebase SDK ] → [ 구글 Firebase 서버(클라우드) ]
```

### 왜 이렇게 나눴나? (요구사항 3 = 재사용)

- **FirebaseKit**(맨 아래)은 "FM-Lite"라는 단어를 전혀 모른다. `leaderboard`, `careers` 같은 게임 용어가 없다. 그냥 "경로(string)에 객체(T)를 저장/조회/구독"하는 **범용 도구**다.
- **어댑터 층**이 게임의 개념(리더보드, 커리어)을 FirebaseKit의 범용 호출로 번역한다.
- 그래서 다른 게임을 만들 때 `Assets/_Project/FirebaseKit/` 폴더만 복사하면 인증+DB 기능을 그대로 쓸 수 있다.

Unity에서는 이 층들을 **asmdef(어셈블리 정의)** 파일로 강제 분리한다. `FirebaseKit.asmdef`의 참조 목록이 비어 있어서(`"references": []`), FirebaseKit 코드가 실수로 게임 코드를 가져다 쓰면 **컴파일 에러**가 난다 → 재사용성이 코드로 보장된다.

---

## 2. Firebase 설정 (코드 작성 전 준비)

### 2-1. Firebase 콘솔에서 한 일

1. **Firebase 프로젝트 생성** (console.firebase.google.com)
2. **Realtime Database 생성** — 리전 `asia-southeast1` (싱가포르, 한국에서 가깝다)
3. **Authentication 제공자 활성화**
   - 익명(Anonymous) — 게스트용
   - 이메일/비밀번호(Email/Password) — 계정용
4. **보안 규칙(Security Rules)** 설정 — 누가 무엇을 읽고 쓸 수 있는지 서버에서 강제:

```json
{
  "rules": {
    "leaderboard": {
      ".read": true,                                      // 랭킹은 누구나 읽기
      ".indexOn": ["score"],                              // 점수 정렬 빠르게
      "$uid": { ".write": "auth != null && auth.uid === $uid" }  // 내 행만 쓰기
    },
    "careers": {
      "$uid": {
        ".read": "auth != null && auth.uid === $uid",     // 내 커리어만 읽기
        ".write": "auth != null && auth.uid === $uid",
        "seasons": { ".indexOn": ["timestamp"] }
      }
    },
    "users": {
      "$uid": {
        ".read": "auth != null && auth.uid === $uid",
        ".write": "auth != null && auth.uid === $uid"
      }
    }
  }
}
```

> **핵심 개념**: `auth.uid`는 로그인한 사용자의 고유 ID다. 규칙이 `auth.uid === $uid`이면 "URL 경로의 `$uid` 자리와 내 로그인 ID가 같을 때만" 쓰기를 허용한다 → 남의 데이터를 못 건드린다. **보안은 클라이언트(게임)가 아니라 서버(규칙)가 지킨다.**

### 2-2. Unity 프로젝트에 한 일

1. **Firebase Unity SDK 13.12.0** 임포트 — `Firebase.Auth`, `Firebase.Database` 패키지
2. **`google-services.json` / `google-services-desktop.json`** 배치 — 내 Firebase 프로젝트와 연결하는 설정 파일(어떤 프로젝트인지, DB 주소 등)

이 둘이 있어야 SDK가 "어느 Firebase 프로젝트에 접속할지" 안다.

---

## 3. 재사용 층 — FirebaseKit (요구사항 3)

`Assets/_Project/FirebaseKit/` 폴더. 게임과 무관한 3개 클래스 + asmdef.

### 3-1. `FirebaseService` — 시동 거는 곳

게임이 켜지면 가장 먼저 Firebase SDK를 **초기화**해야 한다. 이 클래스가 그 일을 한다. Unity의 `MonoBehaviour`(씬의 게임오브젝트에 붙는 컴포넌트)다.

```csharp
public class FirebaseService : MonoBehaviour
{
    public static FirebaseService Instance { get; private set; }  // 어디서든 접근
    public bool IsReady { get; private set; }                     // 준비 완료?
    public FirebaseDatabase Database { get; private set; }        // DB 핸들
    public FirebaseAuth Auth { get; private set; }                // 인증 핸들

    [SerializeField] private bool signInAnonymouslyOnReady = true; // 시작 시 게스트 로그인?

    public event Action OnReady { /* 이미 준비됐으면 즉시 호출 */ }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);   // 씬이 바뀌어도 안 사라짐
    }

    private async void Start() => await InitializeAsync();

    private async Task InitializeAsync()
    {
        // 1) SDK가 정상 작동할 수 있는지 의존성 체크 (네이티브 라이브러리 등)
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available) { Debug.LogError(...); return; }

        // 2) DB와 Auth 핸들 확보
        Database = FirebaseDatabase.DefaultInstance;
        Auth = FirebaseAuth.DefaultInstance;

        // 3) 로그인 안 돼 있으면 익명(게스트) 로그인 → DB 접근하려면 누구든 로그인 필요
        if (signInAnonymouslyOnReady && Auth.CurrentUser == null)
            await AuthManager.SignInAnonymouslyAsync();

        // 4) 준비 완료 알림
        IsReady = true;
        _onReady?.Invoke();
    }
}
```

**함수 역할 정리**
- `Awake()` — 게임오브젝트가 생기는 즉시 1회. **싱글톤**(Instance 하나만) 보장 + `DontDestroyOnLoad`로 씬 전환에도 유지.
- `Start()` → `InitializeAsync()` — 비동기로 SDK 초기화. `async/await`는 "네트워크 응답을 기다리되 게임은 멈추지 않게" 하는 문법.
- `OnReady` 이벤트 — "준비됐다"를 다른 코드에 알린다. **이미 준비된 뒤에 구독해도 즉시 호출**되도록 만들어서, 타이밍 경쟁(race)을 피한다.

> **핵심 개념 (익명 인증)**: 회원가입 안 해도 `SignInAnonymouslyAsync()`로 임시 ID(uid)를 발급받는다. 이 uid는 **기기/설치당 1개**로 로컬에 캐시된다. 그래서 DB 보안규칙(`auth != null`)을 통과할 수 있다. 게스트도 점수를 올릴 수 있는 이유다.

### 3-2. `AuthManager` — 인증 전담 (요구사항 1)

로그인/회원가입/로그아웃을 한 곳에 모았다. `static`이라 `AuthManager.Uid`처럼 바로 호출한다.

```csharp
public static class AuthManager
{
    private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

    public static FirebaseUser CurrentUser => Auth?.CurrentUser;
    public static bool IsSignedIn => CurrentUser != null;
    public static bool IsAnonymous => CurrentUser?.IsAnonymous ?? false;  // 게스트?
    public static string Uid => CurrentUser?.UserId;                       // 내 고유 ID
    public static string Email => CurrentUser?.Email;

    public static event Action StateChanged;   // 로그인 상태 바뀌면 발행

    // 게스트 로그인
    public static async Task<FirebaseUser> SignInAnonymouslyAsync()
    {
        AuthResult result = await Auth.SignInAnonymouslyAsync();
        StateChanged?.Invoke();
        return result.User;
    }

    // 회원가입 (이메일/비밀번호로 새 계정 생성)
    public static async Task<FirebaseUser> SignUpEmailAsync(string email, string password)
    {
        AuthResult result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
        StateChanged?.Invoke();
        return result.User;
    }

    // 로그인 (기존 계정)
    public static async Task<FirebaseUser> SignInEmailAsync(string email, string password)
    {
        AuthResult result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
        StateChanged?.Invoke();
        return result.User;
    }

    // 로그아웃 후 다시 게스트로 (로그아웃만 하면 uid가 없어져 DB 접근 불가하므로)
    public static async Task SignOutToGuestAsync()
    {
        Auth.SignOut();
        await SignInAnonymouslyAsync();
    }
}
```

**함수 역할 정리**
- `Uid` — 모든 데이터의 주인을 가리키는 열쇠. 익명이든 계정이든 이 값으로 내 데이터를 찾는다.
- `IsAnonymous` — 게스트인지 진짜 계정인지 구분. UI에서 "게스트" vs 이메일 표시에 쓴다.
- `SignUpEmailAsync` / `SignInEmailAsync` — Firebase SDK의 계정 생성/로그인 함수를 감싼다. 성공하면 `CurrentUser`가 바뀌고 `StateChanged` 이벤트가 발행돼 UI가 갱신된다.
- `StateChanged` 이벤트 — "방금 로그인/로그아웃했어!"를 UI에 알려서 화면(게스트→이메일)을 자동 갱신한다.

> **계정 로그인의 의미**: 계정으로 로그인하면 uid가 **익명 uid → 계정 uid**로 바뀐다. 그래서 계정마다 데이터가 자동 분리된다(다른 계정 = 다른 uid = 다른 데이터). 같은 계정으로 다른 기기에서 로그인하면 같은 uid → 내 기록이 따라온다.

### 3-3. `RealtimeDatabaseService` — 범용 DB 도구

Firebase DB를 다루는 **게임 무관 범용 함수들**. 경로(`"leaderboard"`)와 타입(`<T>`)만 받는다.

```csharp
public static class RealtimeDatabaseService
{
    public static bool IsReady =>
        FirebaseService.Instance != null && FirebaseService.Instance.IsReady;

    public static object ServerTimestamp => ServerValue.Timestamp;  // 서버 시각 센티넬

    // 경로 문자열 "a/b/c" → DB 참조로 변환
    private static DatabaseReference Ref(string path)
    {
        var r = FirebaseService.Instance.Database.RootReference;
        foreach (var seg in path.Split('/'))
            if (!string.IsNullOrEmpty(seg)) r = r.Child(seg);
        return r;
    }

    // ── 쓰기 ──
    public static Task SetAsync<T>(string path, T value)        // 객체를 JSON으로 통째 저장
        => Ref(path).SetRawJsonValueAsync(JsonUtility.ToJson(value));

    public static Task SetValueAsync(string path, object value) // 한 칸(스칼라)만 저장
        => Ref(path).SetValueAsync(value);

    public static Task UpdateChildrenAsync(string path, IDictionary<string, object> updates)
        => Ref(path).UpdateChildrenAsync(updates);             // 여러 경로 원자적 갱신

    public static string GeneratePushKey(string path)          // 시간순 정렬 가능한 키 생성
        => Ref(path).Push().Key;

    // ── 읽기 ──
    public static async Task<T> GetAsync<T>(string path) where T : class
    {
        DataSnapshot snap = await Ref(path).GetValueAsync();
        return snap.Exists ? JsonUtility.FromJson<T>(snap.GetRawJsonValue()) : null;
    }

    // orderByChild로 서버 정렬한 상위 N개 (키+값 쌍 리스트로)
    public static async Task<List<DbEntry<T>>> QueryListAsync<T>(
        string path, string orderByChild, int limitToLast)
    {
        Query query = Ref(path).OrderByChild(orderByChild).LimitToLast(limitToLast);
        return Parse<T>(await query.GetValueAsync());
    }

    // ── 실시간 구독 ── (값이 바뀔 때마다 콜백)
    public static IDisposable SubscribeList<T>(
        string path, string orderByChild, int limitToLast, Action<List<DbEntry<T>>> onChanged)
    {
        Query query = Ref(path).OrderByChild(orderByChild).LimitToLast(limitToLast);
        EventHandler<ValueChangedEventArgs> handler = (s, e) =>
            onChanged?.Invoke(Parse<T>(e.Snapshot));
        query.ValueChanged += handler;
        return new Subscription(() => query.ValueChanged -= handler);  // Dispose()로 해제
    }
}

public class DbEntry<T> { public string Key; public T Value; }  // DB 한 줄 = 키 + 값
```

**함수 역할 정리**
- `SetAsync<T>` — C# 객체를 JSON 문자열로 바꿔(`JsonUtility.ToJson`) 해당 경로에 통째 저장. (예: `leaderboard/{uid}`에 내 랭킹 한 줄)
- `GetAsync<T>` — 경로의 JSON을 읽어 C# 객체로 복원(`JsonUtility.FromJson`).
- `QueryListAsync<T>` — "score 기준 상위 20개"처럼 **서버가 정렬·필터**해서 일부만 가져온다(전체를 받지 않아 효율적).
- `SubscribeList<T>` — **실시간 리스너**. 누가 점수를 올려서 상위 N이 바뀌면 콜백이 자동 호출된다 → 새로고침 없이 랭킹이 갱신된다. 반환된 `IDisposable`의 `Dispose()`를 부르면 구독 해제(메모리 누수 방지).
- `UpdateChildrenAsync` — 여러 칸을 **한 번에, 전부 성공 아니면 전부 실패**(원자적)로 갱신. 커리어 기록에서 "시즌 추가 + 누적점수 갱신"을 동시에 할 때 쓴다.

> **핵심 개념 (제네릭 `<T>`)**: `SetAsync<LeaderboardEntry>`, `GetAsync<UserProfile>`처럼 **어떤 타입이든** 받는다. 그래서 이 도구는 리더보드든 프로필이든 상관없이 동작 → 재사용 가능.

> **핵심 개념 (메인 스레드)**: `SubscribeList`의 콜백은 Firebase의 별도 스레드에서 올 수 있다. Unity의 UI는 **메인 스레드에서만** 만질 수 있으므로, UI를 갱신하는 쪽(컨트롤러)에서 메인 스레드로 넘겨줘야 한다(아래 4-3, 5-1 참고).

---

## 4. 어댑터 층 — 게임 데이터 ↔ 클라우드 (요구사항 2의 코드부)

FirebaseKit의 범용 함수를 **게임 개념으로 번역**하는 얇은 클래스들. `Assets/_Project/Scripts/Persistence/Cloud/`.

### 4-1. DTO (Data Transfer Object) — 저장 형태

클라우드에 저장될 데이터의 **모양**을 정의하는 단순 클래스. `[Serializable]`이면 `JsonUtility`가 JSON으로 변환할 수 있다.

```csharp
[Serializable] public class UserProfile {          // users/{uid}
    public string nickname;
    public long createdAt;        // 생성 시각 (유닉스 밀리초)
}
[Serializable] public class LeaderboardEntry {     // leaderboard/{uid}
    public string nickname;
    public string clubName;
    public int score;
    [NonSerialized] public string uid;  // 노드 키 → 저장은 안 하고 읽을 때 채움
}
[Serializable] public class SeasonRecord {         // careers/{uid}/seasons/{key}
    public int year;
    public string clubName;
    public int position;          // 최종 순위
    public int points;            // 시즌 승점
    public long timestamp;        // 서버 기록 시각
}
```

> **비정규화(denormalization)**: `LeaderboardEntry`에 `nickname`/`clubName`을 **중복 저장**한다. 랭킹을 보여줄 때 `users` 테이블을 따로 조회(조인)하지 않아도 되도록. Realtime Database는 SQL 같은 조인이 없어서, 보여줄 데이터를 미리 함께 저장하는 게 정석이다.

### 4-2. `LeaderboardRepository` — 랭킹 읽기/쓰기/실시간

```csharp
public static class LeaderboardRepository
{
    // 내 랭킹 한 줄 제출 (leaderboard/{uid} 덮어쓰기 → 1인 1행)
    public static Task SubmitEntryAsync(string nickname, string clubName, int score)
    {
        string uid = RequireUid();
        var entry = new LeaderboardEntry { nickname = nickname, clubName = clubName, score = score };
        return RealtimeDatabaseService.SetAsync($"leaderboard/{uid}", entry);
    }

    // 상위 N명 (서버는 오름차순으로 주므로 점수 내림차순 재정렬)
    public static async Task<List<LeaderboardEntry>> LoadTopAsync(int limit)
    {
        RequireUid();
        var rows = await RealtimeDatabaseService.QueryListAsync<LeaderboardEntry>("leaderboard", "score", limit);
        return ToSortedEntries(rows);
    }

    // 실시간 구독 (상위 N이 바뀌면 onChanged 호출)
    private static IDisposable _subscription;
    public static void StartListener(int limit, Action<List<LeaderboardEntry>> onChanged)
    {
        StopListener();
        RequireUid();
        _subscription = RealtimeDatabaseService.SubscribeList<LeaderboardEntry>(
            "leaderboard", "score", limit,
            rows => onChanged?.Invoke(ToSortedEntries(rows)));
    }
    public static void StopListener() { _subscription?.Dispose(); _subscription = null; }

    private static List<LeaderboardEntry> ToSortedEntries(List<DbEntry<LeaderboardEntry>> rows)
    {
        var list = new List<LeaderboardEntry>();
        foreach (var row in rows) { row.Value.uid = row.Key; list.Add(row.Value); }  // 키→uid
        list.Sort((a, b) => b.score.CompareTo(a.score));   // 1위 먼저
        return list;
    }

    private static string RequireUid()  // 준비/로그인 확인 후 uid 반환
    {
        if (!RealtimeDatabaseService.IsReady) throw new InvalidOperationException("Firebase 미준비");
        string uid = AuthManager.Uid;
        if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("로그인 안 됨");
        return uid;
    }
}
```

**역할**: 게임은 `LeaderboardRepository.SubmitEntryAsync("gamja_hoo", "감자FC", 790)`처럼 부르기만 하면, 내부에서 `leaderboard/{내uid}` 경로로 변환해 FirebaseKit에 넘긴다. `StartListener`로 실시간 랭킹을 구독하고, 화면을 닫을 때 `StopListener`로 해제한다.

### 4-3. `CareerRepository` — 역대 시즌 기록

```csharp
public static class CareerRepository
{
    // 한 시즌 기록 추가 + 누적점수 갱신을 "한 번에"(원자적)
    public static Task RecordSeasonAsync(string clubName, int year, int position, int points, int totalScore)
    {
        string uid = RequireUid();
        string seasonKey = RealtimeDatabaseService.GeneratePushKey($"careers/{uid}/seasons");

        var seasonData = new Dictionary<string, object> {
            { "year", year }, { "clubName", clubName }, { "position", position },
            { "points", points }, { "timestamp", RealtimeDatabaseService.ServerTimestamp },
        };
        // seasons/{key} 전체 + score 를 동시에 갱신
        var updates = new Dictionary<string, object> {
            { $"seasons/{seasonKey}", seasonData }, { "score", totalScore },
        };
        return RealtimeDatabaseService.UpdateChildrenAsync($"careers/{uid}", updates);
    }

    // 최근 N개 시즌 (최신순)
    public static async Task<List<SeasonRecord>> LoadRecentSeasonsAsync(int limit)
    {
        string uid = RequireUid();
        var rows = await RealtimeDatabaseService.QueryListAsync<SeasonRecord>(
            $"careers/{uid}/seasons", "timestamp", limit);
        var list = new List<SeasonRecord>();
        foreach (var row in rows) list.Add(row.Value);
        list.Reverse();   // 서버는 오름차순 → 최신순으로 뒤집기
        return list;
    }
}
```

> **`ServerTimestamp`와 멀티패스 갱신**: `timestamp`를 게임 기기 시각이 아니라 **서버 시각**으로 박는다(기기 시계 조작 방지 + 정렬 일관성). 그리고 "시즌 추가"와 "누적점수 변경"을 `UpdateChildrenAsync` 한 번으로 처리해서, 둘 중 하나만 저장되는 사고를 막는다.

### 4-4. `CloudProfileRepository` — 감독 프로필

```csharp
public static class CloudProfileRepository
{
    public static Task SetProfileAsync(string nickname)   // 회원가입 시 호출
    {
        string uid = RequireUid();
        var profile = new UserProfile { nickname = nickname,
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        return RealtimeDatabaseService.SetAsync($"users/{uid}", profile);
    }
    public static Task<UserProfile> GetProfileAsync()     // 업로드 시 닉네임 조회
        => RealtimeDatabaseService.GetAsync<UserProfile>($"users/{AuthManager.Uid}");
}
```

---

## 5. 적용 층 — 게임에 붙이기 (요구사항 4)

### 5-1. `CareerScore` — 점수 계산 (순수 함수)

명예의 전당에 올릴 **감독 커리어 점수**를 계산한다. 게임 상태(`GameState`)만 입력받아 계산하고 아무것도 바꾸지 않는다(부수효과 없음).

```csharp
public static class CareerScore
{
    public const int TitleBonus = 200;   // 우승 보너스

    public static int ComputeTotal(GameState state)
    {
        var club = state?.GetClub(state.userClubId);
        if (club == null) return 0;

        int score = state.managerReputation;            // 기본: 감독 명성
        foreach (var league in state.leagues)
            foreach (var h in league.history)            // 완료된 모든 시즌
            {
                var entry = FindEntry(h.standings, club.id);
                if (entry == null) continue;             // 내 구단이 참여한 시즌만
                int position = PositionOf(h.standings, club.id);
                score += entry.points + (position == 1 ? TitleBonus : 0);  // 승점 + 우승보너스
            }
        return score;
    }

    public static SeasonOutcome GetLatestSeason(GameState state) { /* 가장 최근 시즌 성적 */ }
}
```

> **멱등성(idempotent)**: 매번 전체 history를 **다시 계산**한다. 그래서 같은 시즌을 두 번 업로드해도 점수가 중복으로 쌓이지 않는다. "더하기"가 아니라 "전체를 새로 합산"하는 방식이라 안전하다.

### 5-2. `HallOfFameService` — 시즌 종료 시 자동 업로드

게임에서 시즌이 끝나면(`SeasonEndedEvent` 발생) **자동으로** 클라우드에 업로드한다. `GameManager` 게임오브젝트에 붙는 영속 컴포넌트.

```csharp
public class HallOfFameService : MonoBehaviour
{
    private void Awake()
    {
        Instance = this;
        _onSeasonEnded = _ => UploadFireAndForget();
        EventBus.Subscribe(_onSeasonEnded);   // 시즌 종료 이벤트 구독
    }

    public async Task UploadCurrentCareerAsync()
    {
        if (!RealtimeDatabaseService.IsReady) return;          // Firebase 준비 확인
        var state = GameManager.Instance?.State;
        var club = state?.GetClub(state.userClubId);
        if (club == null) return;

        int total = CareerScore.ComputeTotal(state);            // 1) 점수 계산
        SeasonOutcome latest = CareerScore.GetLatestSeason(state);
        if (latest != null)                                     // 2) 시즌 기록 저장
            await CareerRepository.RecordSeasonAsync(
                latest.clubName, latest.year, latest.position, latest.points, total);

        // 3) 닉네임 결정: 가입 프로필 닉네임 우선, 없으면(게스트) 구단명
        UserProfile profile = await CloudProfileRepository.GetProfileAsync();
        string nickname = !string.IsNullOrEmpty(profile?.nickname) ? profile.nickname : club.name;

        await LeaderboardRepository.SubmitEntryAsync(nickname, club.name, total);  // 4) 랭킹 갱신
    }
}
```

**흐름**: 시즌 종료 → 점수 계산 → 커리어(seasons)에 이번 시즌 추가 → 리더보드에 내 점수/닉네임 갱신. 이 4단계가 한 번에 일어난다. **로그인했으면 가입 닉네임**, 게스트면 구단명으로 표시된다(요구사항 4의 닉네임 처리).

### 5-3. `HallOfFameController` — 명예의 전당 화면

`HallOfFameScene`의 컨트롤러. 좌측 = 실시간 글로벌 랭킹, 우측 = 내 역대 시즌.

```csharp
public class HallOfFameController : MonoBehaviour
{
    private SynchronizationContext _mainThread;   // 메인 스레드 핸들

    private void Start()
    {
        _mainThread = SynchronizationContext.Current;   // 메인 스레드 캡처
        ...
        var fb = FirebaseService.Instance;
        if (fb == null) { SetStatus("Firebase 미초기화"); return; }
        fb.OnReady += OnFirebaseReady;                  // 준비되면 데이터 로드
    }

    private void OnFirebaseReady()
    {
        StartLeaderboardListener();   // 실시간 랭킹 구독
        LoadMySeasons();              // 내 시즌 1회 로드
    }

    private void StartLeaderboardListener()
        => LeaderboardRepository.StartListener(topCount, OnLeaderboardChanged);

    // ★ Firebase 스레드일 수 있는 콜백을 메인 스레드로 넘김
    private void OnLeaderboardChanged(List<LeaderboardEntry> entries)
        => _mainThread.Post(_ => PopulateLeaderboard(entries), null);

    private void PopulateLeaderboard(List<LeaderboardEntry> entries)
    {
        ClearList(leaderboardListParent);
        string myUid = AuthManager.Uid;                 // 내 행 강조용
        for (int i = 0; i < entries.Count; i++)
        {
            var go = Instantiate(rankItemPrefab, leaderboardListParent);
            go.GetComponent<HallOfFameRankItem>().Setup(i + 1, entries[i], myUid);
        }
    }

    private async void LoadMySeasons()
    {
        var seasons = await CareerRepository.LoadRecentSeasonsAsync(seasonCount);
        foreach (var rec in seasons)
            Instantiate(seasonItemPrefab, seasonListParent)
                .GetComponent<HallOfFameSeasonItem>().Setup(rec);
    }

    private void OnDestroy()   // 화면 떠날 때 구독 해제 (필수)
    {
        if (FirebaseService.Instance != null) FirebaseService.Instance.OnReady -= OnFirebaseReady;
        LeaderboardRepository.StopListener();
    }
}
```

**함수 역할**
- `Start` — 메인 스레드를 기억해두고, Firebase가 준비되면(`OnReady`) 데이터를 로드하도록 예약.
- `OnLeaderboardChanged` — **실시간 콜백이 다른 스레드에서 와도** `_mainThread.Post(...)`로 메인 스레드에 넘겨서 안전하게 UI를 그린다. (이걸 안 하면 가끔 크래시/에러)
- `PopulateLeaderboard` — 받은 랭킹 리스트로 행 프리팹(`HallOfFameRankItem`)을 하나씩 생성. 내 uid와 같은 행은 강조색.
- `LoadMySeasons` — `await`로 내 시즌을 한 번 가져와 행 생성. `await`의 다음 줄은 Unity가 자동으로 메인 스레드에서 이어줘서 마샬링이 필요 없다(리스너와 다른 점).
- `OnDestroy` — **반드시** 리스너를 해제한다. 안 하면 화면을 나가도 콜백이 죽은 UI를 건드리려다 에러난다.

행 컨트롤러(`HallOfFameRankItem`)는 받은 데이터로 텍스트만 채운다:

```csharp
public void Setup(int rank, LeaderboardEntry entry, string myUid)
{
    rankText.text = rank.ToString();
    nicknameText.text = string.IsNullOrEmpty(entry.nickname) ? "익명" : entry.nickname;
    clubText.text = entry.clubName;
    scoreText.text = entry.score.ToString("N0");
    backgroundImage.color = (entry.uid == myUid) ? myEntryColor : defaultColor;  // 내 행 강조
}
```

### 5-4. `MainMenuController` — 로그인/회원가입 패널

메인 메뉴에서 계정 인증을 처리한다(요구사항 1의 UI). 핵심만:

```csharp
// 게스트면 로그인 패널 열기, 계정이면 로그아웃
private void OnAccountActionClicked()
{
    if (AuthManager.IsSignedIn && !AuthManager.IsAnonymous) LogoutToGuest();
    else ShowAuthPanel();
}

// 로그인 버튼
private async void OnLoginClicked()
{
    if (!ValidateAuthInputs(email, pw)) return;
    try { await AuthManager.SignInEmailAsync(email.Trim(), pw); ShowMainPanel(); }
    catch (Exception ex) { SetAuthStatus($"인증 실패: {ex.Message}"); }   // 오류 표시
}

// 회원가입 버튼
private async void OnSignupClicked()
{
    if (!ValidateAuthInputs(email, pw)) return;
    try {
        await AuthManager.SignUpEmailAsync(email.Trim(), pw);          // 1) 계정 생성
        if (!string.IsNullOrWhiteSpace(nick))
            await CloudProfileRepository.SetProfileAsync(nick.Trim()); // 2) 닉네임 저장
        ShowMainPanel();
    } catch (Exception ex) { SetAuthStatus($"인증 실패: {ex.Message}"); }
}

// 로그인 상태가 바뀌면 화면(게스트↔이메일, 로그인↔로그아웃) 자동 갱신
private void RefreshAccountStatus()
{
    bool isAccount = AuthManager.IsSignedIn && !AuthManager.IsAnonymous;
    accountStatusText.text = isAccount ? AuthManager.Email : "게스트";
    SetButtonLabel(accountActionButton, isAccount ? "menu_logout" : "menu_login");
}
```

`Start`에서 `AuthManager.StateChanged += RefreshAccountStatus`를 구독해, 로그인/로그아웃이 일어나면 화면이 저절로 바뀐다.

---

## 6. 전체 동작 흐름 (시나리오로 따라가기)

### 흐름 A — 앱 시작
```
게임 실행 → MainMenuScene 로드 → GameManager 오브젝트의 FirebaseService.Start()
  → CheckAndFixDependenciesAsync (SDK 점검)
  → 로그인 안 돼 있으면 SignInAnonymouslyAsync (게스트 uid 발급)
  → IsReady = true, OnReady 발행
  → MainMenuController.RefreshAccountStatus() → "게스트" 표시
```

### 흐름 B — 회원가입
```
[로그인] 클릭 → 로그인 패널 → 이메일/비번/닉네임 입력 → [회원가입]
  → AuthManager.SignUpEmailAsync(email, pw)   (Firebase에 계정 생성, uid가 계정 uid로 바뀜)
  → CloudProfileRepository.SetProfileAsync(nick)  → users/{uid} 에 닉네임 저장
  → StateChanged 발행 → 화면이 이메일 + [로그아웃] 으로 갱신
```

### 흐름 C — 시즌 종료 (자동 업로드)
```
게임 플레이 → 시즌 종료 → SeasonEndedEvent 발생
  → HallOfFameService 가 구독 중 → UploadCurrentCareerAsync()
     1) CareerScore.ComputeTotal(state)               점수 합산
     2) CareerRepository.RecordSeasonAsync(...)        careers/{uid}/seasons 에 이번 시즌 + score 갱신
     3) CloudProfileRepository.GetProfileAsync()       닉네임 조회 (없으면 구단명)
     4) LeaderboardRepository.SubmitEntryAsync(...)    leaderboard/{uid} 갱신
```

### 흐름 D — 명예의 전당 화면
```
메인 메뉴 [명예의 전당] → HallOfFameScene 로드 → HallOfFameController.Start()
  → FirebaseService.OnReady (이미 준비됨 → 즉시)
     · LeaderboardRepository.StartListener()   서버 상위 N 실시간 구독
         → 값 변경마다 OnLeaderboardChanged (다른 스레드 가능)
         → _mainThread.Post → PopulateLeaderboard → 행 생성 (내 행 강조)
     · CareerRepository.LoadRecentSeasonsAsync()  내 시즌 1회 로드 → 행 생성
  → [뒤로] → OnDestroy → StopListener (구독 해제)
```

---

## 7. 데이터가 클라우드에 저장된 모습

```
/
├── users/{uid}          → { nickname: "gamja_hoo", createdAt: 1718...} 
├── leaderboard/{uid}    → { nickname: "gamja_hoo", clubName: "감자FC", score: 790 }
└── careers/{uid}
    ├── score: 790
    └── seasons/{pushKey} → { year:2029, clubName:"락엣 시티", position:1, points:92, timestamp:1718... }
```

자세한 스키마·필드 설명은 **`docs/firebase-rtdb.md`** 참고.

---

## 8. 알아두면 좋은 핵심 개념 정리

| 개념 | 설명 |
| --- | --- |
| **uid** | 사용자 고유 ID. 모든 데이터의 주인이자 보안 기준. 익명=기기당 1개, 계정=계정당 1개. |
| **익명 vs 계정** | 익명은 게스트(임시), 계정은 이메일/비번. 로그인하면 uid가 바뀌어 데이터가 분리됨. |
| **async/await** | 네트워크를 "기다리되 게임은 안 멈추게" 하는 비동기 문법. |
| **실시간 리스너** | DB 값이 바뀌면 자동으로 콜백 호출 → 새로고침 없이 화면 갱신. 끝나면 꼭 해제. |
| **메인 스레드 마샬링** | UI는 메인 스레드에서만 가능. 리스너 콜백을 `SynchronizationContext.Post`로 넘김. |
| **비정규화** | 조인이 없는 NoSQL DB에서, 보여줄 데이터를 미리 함께 저장. |
| **멀티패스 원자 갱신** | 여러 칸을 전부 성공 or 전부 실패로 한 번에 저장(`UpdateChildren`). |
| **멱등성** | 같은 작업을 여러 번 해도 결과가 같음(점수 중복 누적 방지). |
| **보안 규칙** | 서버가 강제하는 읽기/쓰기 권한. 클라이언트를 못 믿으니 서버가 막음. |
| **asmdef** | Unity 어셈블리 분리. FirebaseKit이 게임 코드에 의존 못 하게 막아 재사용성 보장. |

---

## 9. 작업 진행 방식 (과제 규칙 준수)

과제 규칙대로 **작업 단위마다 GitHub 이슈 → 별도 브랜치 → PR → main 머지**로 진행했다.

| 이슈 | 내용 | PR |
| --- | --- | --- |
| #542 | 명예의 전당 (익명 기반 백엔드+UI, 완료분 커버) | #541 |
| #543 | 재사용 매니저 분리 (FirebaseKit) | #547 |
| #545 | RTDB 스키마·보안규칙 문서화 | #548 |
| #544 | 이메일/비밀번호 인증 + 로그인 UI | #549 |
| #546 | 명예의 전당 통합 (계정 uid·닉네임) | (코드 변경 없이 검증 후 close) |

각 이슈는 GitHub Projects 보드(#50)에 Type/Priority/Size/날짜와 함께 등록·추적. 모든 PR이 main에 머지됨.

---

## 10. 겪은 함정 / 트러블슈팅 메모

- **입력칸에 글자가 안 떠요** → 복제한 `TMP_InputField`가 숫자 전용(`IntegerNumber`)이었음. `contentType`을 `EmailAddress`/`Standard`로 변경.
- **버튼 한글이 □로 깨져요** → MUIP 버튼의 기본 폰트가 한글 미지원. `normalText.font`를 NotoSansKR로 교체.
- **머지 후 로컬 파일이 사라졌어요** → `gh pr merge --delete-branch`가 untracked SDK 파일과 충돌해 체크아웃 중단. 이후 머지는 `--merge`만, 다음 브랜치는 `git checkout -b x origin/main`으로 분기.
- **Firebase DLL이 잠겨서 git이 안 돼요** → Unity 에디터가 네이티브 DLL을 잠금. Unity를 닫고 git 작업.
- **실시간 갱신이 가끔 에러나요** → 리스너 콜백을 메인 스레드로 마샬링했는지 확인(`_mainThread.Post`).

---

*이 문서는 Firebase 명예의 전당 과제(2026-06-23)의 전체 구현을 설명한다. 스키마 상세는 `docs/firebase-rtdb.md`, 후속 디자인 작업은 `docs/v1.0-tasks.md` Task S.7 참고.*
