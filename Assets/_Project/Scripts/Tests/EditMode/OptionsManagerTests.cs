// OptionsManagerTests.cs
// Task A.6 DoD:
//   T1  Default 값 — PlayerPrefs 미존재 시 기본값 로드
//   T2  Save → Initialize 라운드트립 (Volume / UI Scale)
//   T3  Save → Initialize 라운드트립 (Language enum / Currency string)
//   T4  Save → Initialize 라운드트립 (AutoSave bool)
//   T5  키 충돌 없음 — 모든 PlayerPrefs 키 unique
//   T6  ResetToDefaults — Save 후 호출 시 기본값 복원

using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class OptionsManagerTests
    {
        [SetUp]
        public void Setup() => OptionsManager.ResetToDefaults();

        [TearDown]
        public void TearDown() => OptionsManager.ResetToDefaults();

        // ── T1. Default 값 ───────────────────────────────────────────

        [Test]
        public void T1_Default_Values_LoadCorrectly()
        {
            OptionsManager.Initialize();

            Assert.AreEqual(
                OptionsManager.DefaultVolume,
                OptionsManager.MasterVolume,
                "T1: Master"
            );
            Assert.AreEqual(OptionsManager.DefaultVolume, OptionsManager.SfxVolume, "T1: SFX");
            Assert.AreEqual(OptionsManager.DefaultVolume, OptionsManager.BgmVolume, "T1: BGM");
            Assert.AreEqual(OptionsManager.DefaultUiScale, OptionsManager.UiScale, "T1: UI Scale");
            Assert.AreEqual(
                OptionsManager.DefaultCurrency,
                OptionsManager.Currency,
                "T1: Currency"
            );
            Assert.AreEqual(
                OptionsManager.DefaultAutoSave,
                OptionsManager.AutoSave,
                "T1: AutoSave"
            );
        }

        // ── T2. Volume / UI Scale 라운드트립 ─────────────────────────

        [Test]
        public void T2_Volume_UiScale_Roundtrip()
        {
            OptionsManager.Initialize();
            OptionsManager.MasterVolume = 55f;
            OptionsManager.SfxVolume = 30f;
            OptionsManager.BgmVolume = 75f;
            OptionsManager.UiScale = 125f;
            OptionsManager.Save();

            // 새 세션 시뮬레이션
            OptionsManager.MasterVolume = 0f;
            OptionsManager.SfxVolume = 0f;
            OptionsManager.BgmVolume = 0f;
            OptionsManager.UiScale = 0f;
            OptionsManager.Initialize();

            Assert.AreEqual(55f, OptionsManager.MasterVolume, "T2: Master");
            Assert.AreEqual(30f, OptionsManager.SfxVolume, "T2: SFX");
            Assert.AreEqual(75f, OptionsManager.BgmVolume, "T2: BGM");
            Assert.AreEqual(125f, OptionsManager.UiScale, "T2: UI Scale");
        }

        // ── T3. Language / Currency 라운드트립 ──────────────────────

        [Test]
        public void T3_Language_Currency_Roundtrip()
        {
            OptionsManager.Initialize();
            OptionsManager.Language = Language.Korean;
            OptionsManager.Currency = "KRW";
            OptionsManager.Save();

            OptionsManager.Language = Language.English;
            OptionsManager.Currency = "GBP";
            OptionsManager.Initialize();

            Assert.AreEqual(Language.Korean, OptionsManager.Language, "T3: Language");
            Assert.AreEqual("KRW", OptionsManager.Currency, "T3: Currency");
        }

        // ── T4. AutoSave 라운드트립 (bool) ───────────────────────────

        [Test]
        public void T4_AutoSave_Roundtrip()
        {
            OptionsManager.Initialize();
            OptionsManager.AutoSave = false;
            OptionsManager.Save();

            OptionsManager.AutoSave = true;
            OptionsManager.Initialize();

            Assert.IsFalse(OptionsManager.AutoSave, "T4: AutoSave false 보존");
        }

        // ── T5. 키 충돌 없음 ────────────────────────────────────────

        [Test]
        public void T5_PlayerPrefsKeys_AllUnique()
        {
            var keys = new[]
            {
                OptionsManager.MasterKey,
                OptionsManager.SfxKey,
                OptionsManager.BgmKey,
                OptionsManager.LanguageKey,
                OptionsManager.CurrencyKey,
                OptionsManager.UiScaleKey,
                OptionsManager.AutoSaveKey,
            };
            var distinct = new System.Collections.Generic.HashSet<string>(keys);
            Assert.AreEqual(keys.Length, distinct.Count, "T5: 키 unique");
        }

        // ── T6. ResetToDefaults ─────────────────────────────────────

        [Test]
        public void T6_ResetToDefaults_RestoresDefaults()
        {
            OptionsManager.Initialize();
            OptionsManager.MasterVolume = 10f;
            OptionsManager.Currency = "KRW";
            OptionsManager.AutoSave = false;
            OptionsManager.Save();

            OptionsManager.ResetToDefaults();

            Assert.AreEqual(
                OptionsManager.DefaultVolume,
                OptionsManager.MasterVolume,
                "T6: Volume reset"
            );
            Assert.AreEqual(
                OptionsManager.DefaultCurrency,
                OptionsManager.Currency,
                "T6: Currency reset"
            );
            Assert.AreEqual(
                OptionsManager.DefaultAutoSave,
                OptionsManager.AutoSave,
                "T6: AutoSave reset"
            );
        }
    }
}
