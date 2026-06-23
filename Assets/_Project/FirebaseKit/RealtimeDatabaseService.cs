// RealtimeDatabaseService.cs
// 재사용 가능한 Realtime Database 제네릭 접근 (FirebaseKit). JsonUtility 직렬화 기반 CRUD + 쿼리 + 실시간 구독.
// 도메인 무관 — 경로(string)와 타입(T)만 받는다. 프로젝트별 어댑터(리포지토리)가 이 위에 얹힌다.
//
// ⚠️ 실시간 구독 콜백(ValueChanged)은 메인스레드 보장이 없다. UI 를 만지는 호출측에서
//    메인스레드로 마샬링해야 한다 (본 서비스는 raw 콜백만 전달).

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using UnityEngine;

namespace FirebaseKit
{
    /// <summary>RTDB 한 노드 = 키 + 역직렬화된 값.</summary>
    public class DbEntry<T>
    {
        public string Key;
        public T Value;
    }

    public static class RealtimeDatabaseService
    {
        public static bool IsReady =>
            FirebaseService.Instance != null && FirebaseService.Instance.IsReady;

        /// <summary>서버 타임스탬프 센티넬 (UpdateChildren/Set 값으로 사용).</summary>
        public static object ServerTimestamp => ServerValue.Timestamp;

        private static DatabaseReference Ref(string path)
        {
            var r = FirebaseService.Instance.Database.RootReference;
            foreach (var seg in path.Split('/'))
            {
                if (!string.IsNullOrEmpty(seg))
                    r = r.Child(seg);
            }
            return r;
        }

        // ── 쓰기 ─────────────────────────────────────────────────────
        public static Task SetJsonAsync(string path, string json) =>
            Ref(path).SetRawJsonValueAsync(json);

        /// <summary>객체를 JSON 구조로 통째 저장 (최초 생성 / 전체 갱신).</summary>
        public static Task SetAsync<T>(string path, T value) =>
            SetJsonAsync(path, JsonUtility.ToJson(value));

        /// <summary>단일 값(스칼라/문자열)을 좁힌 경로에 저장 — 다른 형제 필드 보존.</summary>
        public static Task SetValueAsync(string path, object value) =>
            Ref(path).SetValueAsync(value);

        /// <summary>멀티패스 원자적 갱신 (경로 기준 상대 키 → 값 딕셔너리).</summary>
        public static Task UpdateChildrenAsync(string path, IDictionary<string, object> updates) =>
            Ref(path).UpdateChildrenAsync(updates);

        /// <summary>Push 키만 미리 생성 (멀티패스에서 같은 키를 score 등과 함께 쓸 때).</summary>
        public static string GeneratePushKey(string path) => Ref(path).Push().Key;

        // ── 읽기 ─────────────────────────────────────────────────────
        public static async Task<T> GetAsync<T>(string path)
            where T : class
        {
            DataSnapshot snap = await Ref(path).GetValueAsync();
            return snap != null && snap.Exists
                ? JsonUtility.FromJson<T>(snap.GetRawJsonValue())
                : null;
        }

        /// <summary>orderByChild 로 서버 정렬된 상위 N개(오름차순 도착). 정렬/필터는 호출측 책임.</summary>
        public static async Task<List<DbEntry<T>>> QueryListAsync<T>(
            string path,
            string orderByChild,
            int limitToLast
        )
        {
            Query query = Ref(path).OrderByChild(orderByChild).LimitToLast(limitToLast);
            DataSnapshot snap = await query.GetValueAsync();
            return Parse<T>(snap);
        }

        // ── 실시간 구독 ──────────────────────────────────────────────
        /// <summary>
        /// orderByChild + limitToLast 쿼리의 변경을 구독. 반환된 IDisposable.Dispose() 로 해제.
        /// 콜백은 Firebase 스레드일 수 있음 — UI 호출측에서 메인스레드 마샬링 필요.
        /// </summary>
        public static IDisposable SubscribeList<T>(
            string path,
            string orderByChild,
            int limitToLast,
            Action<List<DbEntry<T>>> onChanged
        )
        {
            Query query = Ref(path).OrderByChild(orderByChild).LimitToLast(limitToLast);
            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogError($"[FirebaseKit] 리스너 오류: {args.DatabaseError.Message}");
                    return;
                }
                onChanged?.Invoke(Parse<T>(args.Snapshot));
            };
            query.ValueChanged += handler;
            return new Subscription(() => query.ValueChanged -= handler);
        }

        private static List<DbEntry<T>> Parse<T>(DataSnapshot snap)
        {
            var list = new List<DbEntry<T>>();
            if (snap != null && snap.Exists)
            {
                foreach (DataSnapshot child in snap.Children)
                {
                    var value = JsonUtility.FromJson<T>(child.GetRawJsonValue());
                    if (value != null)
                        list.Add(new DbEntry<T> { Key = child.Key, Value = value });
                }
            }
            return list;
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
