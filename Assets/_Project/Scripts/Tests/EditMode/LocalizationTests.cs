// LocalizationTests.cs
// DoD 검증: v1.0-tasks.md Stage A / Task A.2 — Localization 시스템.
// algorithms.md #52: Get / SetLanguage / args interpolation / fallback.

using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class LocalizationTests
    {
        private LocalizationSO _so;

        [SetUp]
        public void SetUp()
        {
            _so = ScriptableObject.CreateInstance<LocalizationSO>();
            _so.entries.Add(
                new LocalizationEntry
                {
                    key = "reroll_button",
                    korean = "리롤",
                    english = "Reroll",
                }
            );
            _so.entries.Add(
                new LocalizationEntry
                {
                    key = "player_age_fmt",
                    korean = "{0}세",
                    english = "Age {0}",
                }
            );

            // 한국어 고정으로 초기화 (시스템 언어 감지 우회)
            LocalizationSystem.Initialize(_so, Language.Korean);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_so);
        }

        // Get: 한국어 텍스트 반환
        [Test]
        public void Get_Korean_ReturnsKoreanText()
        {
            Assert.AreEqual("리롤", Localization.Get("reroll_button"));
        }

        // SetLanguage: 언어 전환 후 영어 텍스트 반환
        [Test]
        public void SetLanguage_English_ReturnsEnglishText()
        {
            LocalizationSystem.SetLanguage(Language.English);

            Assert.AreEqual("Reroll", Localization.Get("reroll_button"));
        }

        // args 보간: {0} 포맷 정상 치환
        [Test]
        public void Get_WithArgs_InterpolatesFormat()
        {
            Assert.AreEqual("25세", Localization.Get("player_age_fmt", 25));

            LocalizationSystem.SetLanguage(Language.English);
            Assert.AreEqual("Age 25", Localization.Get("player_age_fmt", 25));
        }

        // 미등록 키: key 자체 반환 (폴백)
        [Test]
        public void Get_UnknownKey_ReturnsKey()
        {
            Assert.AreEqual("unknown_key", Localization.Get("unknown_key"));
        }
    }
}
