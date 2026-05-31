// CurrencyFormatterTests.cs
// Task A.8 DoD:
//   T1 GBP — 12.5M base
//   T2 USD — × 1.27 (~15.9M)
//   T3 EUR — × 1.16 (~14.5M)
//   T4 KRW — × 1700 (~21.3B, B 단위)
//   T5 K 단위 (≥ 1K)
//   T6 단위 미만 (< 1K) — 정수 표시
//   T7 음수 금액 처리
//   T8 ExchangeRates / Symbols 4 통화 모두 등록 검증

using FMLite.Utils;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class CurrencyFormatterTests
    {
        // ── T1. GBP 12.5M ────────────────────────────────────────────

        [Test]
        public void T1_GBP_125M()
        {
            Assert.AreEqual("£12.5M", CurrencyFormatter.Format(12_500_000L, Currency.GBP));
        }

        // ── T2. USD 12.5M × 1.27 ≈ 15.9M ─────────────────────────────

        [Test]
        public void T2_USD_125M()
        {
            // 12_500_000 × 1.27 = 15_875_000 → "$15.9M"
            Assert.AreEqual("$15.9M", CurrencyFormatter.Format(12_500_000L, Currency.USD));
        }

        // ── T3. EUR 12.5M × 1.16 ≈ 14.5M ─────────────────────────────

        [Test]
        public void T3_EUR_125M()
        {
            // 12_500_000 × 1.16 = 14_500_000 → "€14.5M"
            Assert.AreEqual("€14.5M", CurrencyFormatter.Format(12_500_000L, Currency.EUR));
        }

        // ── T4. KRW 12.5M × 1700 = 21.25B ───────────────────────────

        [Test]
        public void T4_KRW_BillionUnit()
        {
            // 12_500_000 × 1700 = 21_250_000_000 → "₩21.3B"
            Assert.AreEqual("₩21.3B", CurrencyFormatter.Format(12_500_000L, Currency.KRW));
        }

        // ── T5. K 단위 ───────────────────────────────────────────────

        [Test]
        public void T5_GBP_K_Unit()
        {
            // 5_000 GBP → "£5.0K"
            Assert.AreEqual("£5.0K", CurrencyFormatter.Format(5_000L, Currency.GBP));
        }

        // ── T6. 단위 미만 (정수) ─────────────────────────────────────

        [Test]
        public void T6_GBP_Small_NoUnit()
        {
            // 500 GBP → "£500"
            Assert.AreEqual("£500", CurrencyFormatter.Format(500L, Currency.GBP));
        }

        // ── T7. 음수 처리 ───────────────────────────────────────────

        [Test]
        public void T7_NegativeAmount_GBP()
        {
            // -1_500_000 → "£-1.5M"
            var result = CurrencyFormatter.Format(-1_500_000L, Currency.GBP);
            StringAssert.Contains("-1.5M", result, "T7: 음수 M 단위 표시");
            StringAssert.StartsWith("£", result, "T7: 심볼");
        }

        // ── T8. ExchangeRates / Symbols 4 통화 ──────────────────────

        [Test]
        public void T8_AllCurrencies_HaveRateAndSymbol()
        {
            foreach (Currency c in System.Enum.GetValues(typeof(Currency)))
            {
                Assert.IsTrue(
                    CurrencyFormatter.ExchangeRates.ContainsKey(c),
                    $"T8: '{c}' ExchangeRate 누락"
                );
                Assert.IsTrue(CurrencyFormatter.Symbols.ContainsKey(c), $"T8: '{c}' Symbol 누락");
            }

            Assert.AreEqual(
                1.00f,
                CurrencyFormatter.ExchangeRates[Currency.GBP],
                "T8: GBP base 1.00"
            );
        }
    }
}
