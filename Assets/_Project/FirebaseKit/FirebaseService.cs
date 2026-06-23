// FirebaseService.cs
// 재사용 가능한 Firebase 진입점 (FirebaseKit — 프로젝트 무관, FM-Lite 의존 0).
// SDK 의존성 체크 → Auth/Database 확보 → (옵션) 익명 로그인 보장 → OnReady 발행.
// 씬에 1개 배치, DontDestroyOnLoad. 다른 프로젝트엔 이 폴더(asmdef)만 복사해 재사용.

using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;

namespace FirebaseKit
{
    public class FirebaseService : MonoBehaviour
    {
        public static FirebaseService Instance { get; private set; }

        public bool IsReady { get; private set; }
        public FirebaseDatabase Database { get; private set; }
        public FirebaseAuth Auth { get; private set; }

        [SerializeField]
        [Tooltip("초기화 직후 익명 로그인을 보장한다 (계정 인증 도입 전 임시/게스트 신원).")]
        private bool signInAnonymouslyOnReady = true;

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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
                Debug.LogError($"[FirebaseKit] 의존성 해결 실패: {status}");
                return;
            }

            Database = FirebaseDatabase.DefaultInstance;
            Auth = FirebaseAuth.DefaultInstance;

            if (signInAnonymouslyOnReady && Auth.CurrentUser == null)
                await AuthManager.SignInAnonymouslyAsync();

            IsReady = true;
            Debug.Log($"[FirebaseKit] 준비 완료. uid={AuthManager.Uid}");
            _onReady?.Invoke();
        }
    }
}
