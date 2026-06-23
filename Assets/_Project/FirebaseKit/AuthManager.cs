// AuthManager.cs
// 재사용 가능한 인증 매니저 (FirebaseKit). FirebaseAuth 를 래핑해 인증 진입점을 단일화한다.
// 익명 + 이메일/비밀번호 로그인·회원가입. 계정 인증은 익명 세션을 대체(다른 uid)한다.

using System;
using System.Threading.Tasks;
using Firebase.Auth;

namespace FirebaseKit
{
    public static class AuthManager
    {
        private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

        public static FirebaseUser CurrentUser => Auth?.CurrentUser;
        public static bool IsSignedIn => CurrentUser != null;
        public static bool IsAnonymous => CurrentUser?.IsAnonymous ?? false;
        public static string Uid => CurrentUser?.UserId;
        public static string Email => CurrentUser?.Email;

        /// <summary>인증 상태 변경(로그인/로그아웃/계정 전환) 시 발행. CurrentUser 갱신 후 호출.</summary>
        public static event Action StateChanged;

        public static async Task<FirebaseUser> SignInAnonymouslyAsync()
        {
            AuthResult result = await Auth.SignInAnonymouslyAsync();
            StateChanged?.Invoke();
            return result.User;
        }

        public static async Task<FirebaseUser> SignUpEmailAsync(string email, string password)
        {
            AuthResult result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            StateChanged?.Invoke();
            return result.User;
        }

        public static async Task<FirebaseUser> SignInEmailAsync(string email, string password)
        {
            AuthResult result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
            StateChanged?.Invoke();
            return result.User;
        }

        /// <summary>로그아웃 후 게스트(익명)로 복귀. 미인증 상태로 두면 DB 접근이 막히므로 재익명 로그인.</summary>
        public static async Task SignOutToGuestAsync()
        {
            Auth.SignOut();
            await SignInAnonymouslyAsync(); // 내부에서 StateChanged 발행
        }

        public static void SignOut()
        {
            Auth.SignOut();
            StateChanged?.Invoke();
        }
    }
}
