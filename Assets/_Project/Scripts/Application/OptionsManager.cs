// OptionsManager.cs
// V1.0 — PlayerPrefs 어댑터. 사용자 환경 설정 (사운드 / 언어 / 통화 / UI Scale / 자동 저장).
// GameState 외부 — SaveSystem 영향 0 (design-decisions.md #59).
//
// 저장 키: FMLite.Options.<항목>. 기본값은 게임 첫 실행 시 시스템 언어 감지 + 정해진 default.
//
// Stateless 원칙 (design-decisions.md #3) 의 인프라성 예외 — PlayerPrefs 어댑터.

using System;
using FMLite.Domain;
using FMLite.Utils;
using UnityEngine;

namespace FMLite.Application
{
    public static class OptionsManager
    {
        // ── PlayerPrefs 키 ───────────────────────────────────────────
        public const string MasterKey = "FMLite.Options.Master";
        public const string SfxKey = "FMLite.Options.SFX";
        public const string BgmKey = "FMLite.Options.BGM";
        public const string LanguageKey = "FMLite.Options.Language";
        public const string CurrencyKey = "FMLite.Options.Currency";
        public const string UiScaleKey = "FMLite.Options.UIScale";
        public const string AutoSaveKey = "FMLite.Options.AutoSave";

        // ── 기본값 ──────────────────────────────────────────────────
        public const float DefaultVolume = 80f; // 0-100 슬라이더 단위
        public const float DefaultUiScale = 100f; // %
        public const Currency DefaultCurrency = Utils.Currency.GBP; // £
        public const bool DefaultAutoSave = true;

        // ── 값 (Get/Set) ─────────────────────────────────────────────
        public static float MasterVolume { get; set; }
        public static float SfxVolume { get; set; }
        public static float BgmVolume { get; set; }
        public static Language Language { get; set; }
        public static Currency Currency { get; set; }
        public static float UiScale { get; set; }
        public static bool AutoSave { get; set; }

        /// <summary>게임 시작 시 1회 호출. PlayerPrefs 값 로드, 미존재 시 시스템 언어 + default.</summary>
        public static void Initialize()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterKey, DefaultVolume);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, DefaultVolume);
            BgmVolume = PlayerPrefs.GetFloat(BgmKey, DefaultVolume);

            Language = PlayerPrefs.HasKey(LanguageKey)
                ? ParseLanguage(PlayerPrefs.GetString(LanguageKey))
                : DetectSystemLanguage();

            Currency = ParseCurrency(
                PlayerPrefs.GetString(CurrencyKey, DefaultCurrency.ToString())
            );
            UiScale = PlayerPrefs.GetFloat(UiScaleKey, DefaultUiScale);
            AutoSave = PlayerPrefs.GetInt(AutoSaveKey, DefaultAutoSave ? 1 : 0) == 1;
        }

        /// <summary>현재 값들을 PlayerPrefs 에 영속화.</summary>
        public static void Save()
        {
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            PlayerPrefs.SetFloat(BgmKey, BgmVolume);
            PlayerPrefs.SetString(LanguageKey, Language.ToString());
            PlayerPrefs.SetString(CurrencyKey, Currency.ToString());
            PlayerPrefs.SetFloat(UiScaleKey, UiScale);
            PlayerPrefs.SetInt(AutoSaveKey, AutoSave ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>모든 옵션 키 PlayerPrefs 에서 제거. 테스트 / "초기화" 버튼용.</summary>
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(MasterKey);
            PlayerPrefs.DeleteKey(SfxKey);
            PlayerPrefs.DeleteKey(BgmKey);
            PlayerPrefs.DeleteKey(LanguageKey);
            PlayerPrefs.DeleteKey(CurrencyKey);
            PlayerPrefs.DeleteKey(UiScaleKey);
            PlayerPrefs.DeleteKey(AutoSaveKey);
            Initialize();
        }

        private static Language DetectSystemLanguage() =>
            UnityEngine.Application.systemLanguage == SystemLanguage.Korean
                ? Language.Korean
                : Language.English;

        private static Language ParseLanguage(string s) =>
            s == nameof(Language.Korean) ? Language.Korean : Language.English;

        private static Currency ParseCurrency(string s) =>
            Enum.TryParse<Currency>(s, ignoreCase: false, out var c) ? c : DefaultCurrency;
    }
}
