// AuthManager.cs
// 재사용 가능한 인증 매니저 (FirebaseKit). FirebaseAuth 를 래핑해 인증 진입점을 단일화한다.
// 현재: 익명 로그인. 이메일/비밀번호 로그인·회원가입은 후속 작업에서 추가 (#544).

using System.Threading.Tasks;
using Firebase.Auth;

namespace FirebaseKit
{
    public static class AuthManager
    {
        private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

        public static FirebaseUser CurrentUser => Auth?.CurrentUser;
        public static bool IsSignedIn => CurrentUser != null;
        public static string Uid => CurrentUser?.UserId;

        public static async Task<FirebaseUser> SignInAnonymouslyAsync()
        {
            AuthResult result = await Auth.SignInAnonymouslyAsync();
            return result.User;
        }

        public static void SignOut() => Auth.SignOut();
    }
}
