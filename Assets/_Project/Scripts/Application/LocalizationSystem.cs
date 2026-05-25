// LocalizationSystem.cs
// 키 기반 다국어 조회 시스템. design-decisions.md #52.
//
// 사용법:
//   LocalizationSystem.Initialize(so);          // 게임 시작 시 (GameInitializer 등)
//   Localization.Get("reroll_button")            // 현재 언어 텍스트 반환
//   Localization.Get("player_age_fmt", age)      // args 보간 (string.Format 위임)
//   LocalizationSystem.SetLanguage(Language.English);
//
// SO 미등록 또는 키 없으면 key 자체 반환 (폴백).

using FMLite.Domain;
using UnityEngine;

namespace FMLite.Application
{
    public static class LocalizationSystem
    {
        public static Language CurrentLanguage { get; private set; } = Language.Korean;

        private static LocalizationSO _so;

        public static void Initialize(LocalizationSO so, Language? overrideLanguage = null)
        {
            _so = so;
            CurrentLanguage = overrideLanguage ?? DetectSystemLanguage();
        }

        public static void SetLanguage(Language lang) => CurrentLanguage = lang;

        public static string Get(string key, params object[] args)
        {
            if (_so == null || !_so.TryGetEntry(key, out var entry))
                return key;

            var text = CurrentLanguage == Language.Korean ? entry.korean : entry.english;
            if (string.IsNullOrEmpty(text))
                text = key;

            return args != null && args.Length > 0 ? string.Format(text, args) : text;
        }

        private static Language DetectSystemLanguage() =>
            UnityEngine.Application.systemLanguage == SystemLanguage.Korean
                ? Language.Korean
                : Language.English;
    }

    // Localization.Get(key) 단축 접근자 (design-decisions.md #52 API 명세).
    public static class Localization
    {
        public static string Get(string key, params object[] args) =>
            LocalizationSystem.Get(key, args);
    }
}
