// OptionsControllerTests.cs
// Stage X Sub-B (#427) — OptionsController 순수 selector 매핑 헬퍼 검증.
// MonoBehaviour 인스턴스화 없이 static 메서드만 (씬/MUIP 의존 0).

using FMLite.Domain;
using FMLite.UI;
using FMLite.Utils;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class OptionsControllerTests
    {
        // ── UI Scale ─────────────────────────────────────────────────

        [TestCase(90, 0)]
        [TestCase(100, 1)]
        [TestCase(110, 2)]
        [TestCase(125, 3)]
        public void UiScaleValueToIndex_Maps(float value, int expected)
        {
            Assert.AreEqual(expected, OptionsController.UiScaleValueToIndex(value));
        }

        [Test]
        public void UiScaleValueToIndex_Unknown_DefaultsTo100Index()
        {
            Assert.AreEqual(1, OptionsController.UiScaleValueToIndex(80));
            Assert.AreEqual(1, OptionsController.UiScaleValueToIndex(200));
        }

        [TestCase(0, 90)]
        [TestCase(1, 100)]
        [TestCase(2, 110)]
        [TestCase(3, 125)]
        public void UiScaleIndexToValue_Maps(int index, float expected)
        {
            Assert.AreEqual(expected, OptionsController.UiScaleIndexToValue(index));
        }

        [Test]
        public void UiScaleIndexToValue_OutOfRange_Clamped()
        {
            Assert.AreEqual(90, OptionsController.UiScaleIndexToValue(-5));
            Assert.AreEqual(125, OptionsController.UiScaleIndexToValue(99));
        }

        [Test]
        public void UiScale_RoundTrip()
        {
            for (int i = 0; i < OptionsController.UiScaleValues.Length; i++)
            {
                float v = OptionsController.UiScaleIndexToValue(i);
                Assert.AreEqual(i, OptionsController.UiScaleValueToIndex(v));
            }
        }

        // ── Currency (£/$/€/₩ = GBP/USD/EUR/KRW) ─────────────────────

        [TestCase(0, Currency.GBP)]
        [TestCase(1, Currency.USD)]
        [TestCase(2, Currency.EUR)]
        [TestCase(3, Currency.KRW)]
        public void CurrencyIndexToEnum_Maps(int index, Currency expected)
        {
            Assert.AreEqual(expected, OptionsController.CurrencyIndexToEnum(index));
        }

        [Test]
        public void Currency_RoundTrip()
        {
            foreach (Currency c in System.Enum.GetValues(typeof(Currency)))
            {
                int idx = OptionsController.CurrencyEnumToIndex(c);
                Assert.AreEqual(c, OptionsController.CurrencyIndexToEnum(idx));
            }
        }

        [Test]
        public void CurrencyIndexToEnum_OutOfRange_Clamped()
        {
            Assert.AreEqual(Currency.GBP, OptionsController.CurrencyIndexToEnum(-1));
            Assert.AreEqual(Currency.KRW, OptionsController.CurrencyIndexToEnum(9));
        }

        // ── Language (KO/EN) ─────────────────────────────────────────

        [TestCase(0, Language.Korean)]
        [TestCase(1, Language.English)]
        public void LanguageIndexToEnum_Maps(int index, Language expected)
        {
            Assert.AreEqual(expected, OptionsController.LanguageIndexToEnum(index));
        }

        [Test]
        public void Language_RoundTrip()
        {
            Assert.AreEqual(0, OptionsController.LanguageEnumToIndex(Language.Korean));
            Assert.AreEqual(1, OptionsController.LanguageEnumToIndex(Language.English));
            Assert.AreEqual(
                Language.Korean,
                OptionsController.LanguageIndexToEnum(
                    OptionsController.LanguageEnumToIndex(Language.Korean)
                )
            );
        }
    }
}
