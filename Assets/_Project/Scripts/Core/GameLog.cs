// GameLog.cs
// 카테고리별 로그 출력 래퍼. 디버그 모드 토글로 일반 로그 억제 가능.
// V0.1: 임시 static IsDebugMode 보유. Stage 5 GameBalanceSO 도입 후 그 값을 복사해서 사용.
//
// 이름 주의: UnityEngine.Logger 와의 ambiguous 충돌 회피 위해 Logger 대신 GameLog 사용.

using UnityEngine;

namespace FMLite.Core
{
    public enum LogCategory
    {
        System,
        Match,
        Transfer,
        Youth,
        Season,
    }

    public static class GameLog
    {
        public static bool IsDebugMode { get; set; } = true;

        public static void Log(LogCategory category, string message)
        {
            if (!IsDebugMode)
                return;
            Debug.Log($"[{category}] {message}");
        }

        public static void Warn(LogCategory category, string message)
        {
            Debug.LogWarning($"[{category}] {message}");
        }

        public static void Error(LogCategory category, string message)
        {
            Debug.LogError($"[{category}] {message}");
        }
    }
}
