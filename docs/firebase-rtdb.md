# Firebase Realtime Database — 스키마 & 보안규칙

> 명예의 전당(Firebase 학습 과제, 요구사항 2) 의 RTDB 데이터 설계 문서.
> 코드 매핑: 재사용 레이어 `FirebaseKit`(`Assets/_Project/FirebaseKit/`) + FM-Lite 어댑터 `Assets/_Project/Scripts/Persistence/Cloud/`.

## 개요

| 항목 | 값 |
| --- | --- |
| 제품 | Firebase Realtime Database (RTDB) |
| 리전 | `asia-southeast1` |
| 직렬화 | `JsonUtility` (객체 ↔ JSON 구조, `SetRawJsonValue`) |
| 인증 연계 | 모든 쓰기는 `auth.uid` 소유권 기준. 현재 익명 인증, 이후 이메일/PW 계정 (#544) |
| 키 전략 | 사용자 데이터는 **`{uid}` 를 노드 키**로 사용 (1인 1행) |

## 노드 구조

```
/
├── leaderboard
│   └── {uid}                      # 글로벌 랭킹 1행 (1인 1행, 덮어쓰기)
│       ├── nickname : string
│       ├── clubName : string
│       └── score    : int
│
├── careers
│   └── {uid}
│       ├── score    : int         # 누적 커리어 점수 (멀티패스로 시즌과 원자적 갱신)
│       └── seasons
│           └── {pushKey}          # 시즌 1건 (Push 키 = 시간순 정렬 가능 ID)
│               ├── year     : int
│               ├── clubName : string
│               ├── position : int
│               ├── points   : int
│               └── timestamp: long   # ServerValue.Timestamp (Unix ms, 서버 기록)
│
└── users
    └── {uid}                      # 감독 프로필
        ├── nickname  : string
        └── createdAt : long       # Unix ms (클라 기록)
```

## 노드별 상세

### `leaderboard/{uid}` — 글로벌 랭킹
DTO: `LeaderboardEntry` (`FMLite.Persistence.Cloud`)

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `nickname` | string | 표시용 닉네임 (프로필 닉네임 우선, 없으면 구단명 fallback) |
| `clubName` | string | 소속 구단명 |
| `score` | int | 누적 커리어 점수 (랭킹 정렬 키) |

- **1인 1행**: 노드 키가 `{uid}` 라 같은 사용자가 다시 제출하면 덮어쓰기.
- **비정규화**: `nickname`/`clubName` 을 행에 중복 저장 → 상위 N 표시 시 `users` 조인 불필요 (RTDB 는 조인이 없으므로 읽기 1회로 끝냄).
- `uid` 는 노드 키이므로 행 데이터에 저장하지 않음 (파싱 후 `LeaderboardEntry.uid` 에 채움, `[NonSerialized]`).

### `careers/{uid}` — 내 커리어
DTO: `SeasonRecord` (`seasons/{pushKey}`)

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `score` (careers/{uid}) | int | 누적 커리어 점수. `seasons` Push 와 **같은 호출로 멀티패스 원자 갱신** |
| `year` | int | 시즌 연도 |
| `clubName` | string | 해당 시즌 구단명 |
| `position` | int | 최종 순위 |
| `points` | int | 시즌 승점 |
| `timestamp` | long | `ServerValue.Timestamp` (서버가 채우는 Unix ms) — 시간순 정렬·표시용 |

- **Push 키**: `seasons` 하위는 `Push()` 로 시간 정렬 가능한 키 생성 → 추가만, 덮어쓰기 없음(이력 누적).
- **멀티패스 원자 갱신**: `RecordSeasonAsync` 는 `seasons/{key}` 전체와 `score` 를 한 번의 `UpdateChildren` 으로 갱신 → 부분 실패 없음.
- **최신순 조회**: `OrderByChild("timestamp").LimitToLast(N)` (오름차순 도착) → 클라에서 Reverse.

### `users/{uid}` — 프로필
DTO: `UserProfile`

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `nickname` | string | 감독 닉네임 (이후 회원가입 시 입력, #544) |
| `createdAt` | long | 프로필 최초 생성 시각 (Unix ms) |

- `nickname` 한 칸만 갱신할 땐 경로를 `users/{uid}/nickname` 으로 좁혀 `SetValue` → `createdAt` 등 형제 필드 보존.

## 인덱싱 (`.indexOn`)

서버측 정렬/필터 쿼리를 인덱스로 가속 (없으면 클라 정렬 경고 + 비효율):

| 경로 | 인덱스 | 사용 쿼리 |
| --- | --- | --- |
| `leaderboard` | `score` | `OrderByChild("score").LimitToLast(N)` (상위 N) |
| `careers/{uid}/seasons` | `timestamp` | `OrderByChild("timestamp").LimitToLast(N)` (최근 N) |

## 보안 규칙

> Firebase 콘솔(Realtime Database → 규칙)에 설정. 재현/리뷰를 위해 전문을 여기 기록한다.

```json
{
  "rules": {
    "leaderboard": {
      ".read": true,
      ".indexOn": ["score"],
      "$uid": {
        ".write": "auth != null && auth.uid === $uid"
      }
    },
    "careers": {
      "$uid": {
        ".read": "auth != null && auth.uid === $uid",
        ".write": "auth != null && auth.uid === $uid",
        "seasons": {
          ".indexOn": ["timestamp"]
        }
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

규칙 근거:

| 경로 | read | write | 이유 |
| --- | --- | --- | --- |
| `leaderboard` | **전체 공개** | 본인 `{uid}` 행만 | 랭킹은 모두가 봐야 함. 단 남의 점수 위조 방지 → 쓰기는 본인 행만 |
| `careers/{uid}` | 본인만 | 본인만 | 개인 커리어 이력 — 타인 비공개 |
| `users/{uid}` | 본인만 | 본인만 | 프로필 원본 — 공개 표시는 비정규화된 leaderboard 행으로 충분 |

- 미인증(`auth == null`) 은 어떤 쓰기도 불가. 익명 인증도 `auth.uid` 를 부여하므로 규칙을 통과한다.
- `leaderboard` 만 공개 읽기, 나머지는 소유자 한정 — "공개 랭킹 + 비공개 원본" 분리.

## 인증 ↔ uid ↔ 소유권

- 모든 사용자 데이터의 키이자 쓰기 권한 기준이 **`auth.uid`**.
- **익명 인증**(현재): uid 는 기기/설치당 1개로 로컬 캐싱(재실행 유지). 다른 기기 = 다른 uid, 앱 데이터 삭제 = uid 소실.
- **계정 인증**(이메일/PW, #544 예정): 같은 계정으로 로그인하면 기기 무관 동일 uid → 커리어/랭킹 이식. 회원가입 시 닉네임을 `users/{uid}` 에 기록.

## 재사용 매핑 (FirebaseKit)

도메인 무관 접근은 `FirebaseKit.RealtimeDatabaseService` 제네릭 메서드로, 경로/DTO 조립만 FM-Lite 어댑터가 담당:

| 어댑터(FM-Lite) | FirebaseKit 호출 | 경로 |
| --- | --- | --- |
| `CloudProfileRepository.SetProfileAsync` | `SetAsync<UserProfile>` | `users/{uid}` |
| `CloudProfileRepository.SetNicknameAsync` | `SetValueAsync` | `users/{uid}/nickname` |
| `LeaderboardRepository.SubmitEntryAsync` | `SetAsync<LeaderboardEntry>` | `leaderboard/{uid}` |
| `LeaderboardRepository.LoadTopAsync` | `QueryListAsync<LeaderboardEntry>` | `leaderboard` (orderBy `score`) |
| `LeaderboardRepository.StartListener` | `SubscribeList<LeaderboardEntry>` | `leaderboard` (실시간) |
| `CareerRepository.RecordSeasonAsync` | `GeneratePushKey` + `UpdateChildrenAsync` | `careers/{uid}` (멀티패스) |
| `CareerRepository.LoadRecentSeasonsAsync` | `QueryListAsync<SeasonRecord>` | `careers/{uid}/seasons` (orderBy `timestamp`) |

`uid` 는 `FirebaseKit.AuthManager.Uid`, 준비 상태는 `RealtimeDatabaseService.IsReady` 로 확인.
