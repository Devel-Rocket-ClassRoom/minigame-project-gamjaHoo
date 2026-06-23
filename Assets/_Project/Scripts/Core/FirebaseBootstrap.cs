// FirebaseBootstrap.cs
// Firebase 초기화 + 익명 로그인 진입점. Core Layer (인프라 성격 — GameManager 와 같은 레이어).
// 의존성 체크 → 익명 로그인 → IsReady/OnReady 통지. 씬에 1개 배치, DontDestroyOnLoad.
//
// 익명 인증: uid 는 기기/설치당 1개로 로컬 캐싱되어 재실행해도 유지된다.
// 다른 기기 = 다른 uid, 앱 데이터 삭제 = uid 소실 (명예의 전당 학습 범위에선 허용).

using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;

namespace FMLite.Core
{
    public class FirebaseBootstrap : MonoBehaviour
    {
        public static FirebaseBootstrap Instance { get; private set; }

        public bool IsReady { get; private set; }
        public string UserId { get; private set; }
        public FirebaseDatabase Database { get; private set; }

        // 초기화 완료 시 1회 발행. 이미 Ready 인 뒤 구독하면 즉시 호출된다.
        private event Action _onReady;
        public event Action OnReady
        {
            add
            {
                _onReady += value;
                if (IsReady)
                    value?.Invoke();
            }
            remove { _onReady -= value; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Unity 메인스레드에서 await 한 연속은 UnitySynchronizationContext 로 메인스레드에 복귀한다.
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                Debug.LogError($"[Firebase] 의존성 해결 실패: {status}");
                return;
            }

            Database = FirebaseDatabase.DefaultInstance;

            var auth = FirebaseAuth.DefaultInstance;
            AuthResult result = await auth.SignInAnonymouslyAsync();
            UserId = result.User.UserId;

            IsReady = true;
            Debug.Log($"[Firebase] 익명 로그인 성공. uid={UserId}");
            _onReady?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
