// CurrencyFormatter.cs
// V1.0 — GBP base 금액을 사용자 통화 표시 문자열로 변환 (algorithms.md V1.0-5).
// V1.0 환율 / 심볼 hardcoded (V1.x GameBalanceSO 외부화 보완 — design-decisions.md #61).
//
// 단위 자동: |converted| >= 1B → B / >= 1M → M / >= 1K → K / 그 외 정수.

using System;
using System.Collections.Generic;

namespace FMLite.Utils
{
    public static class CurrencyFormatter
    {
        /// <summary>GBP base → Currency 환율. V1.0 고정.</summary>
        public static readonly IReadOnlyDictionary<Currency, float> ExchangeRates = new Dictionary<
            Currency,
            float
        >
        {
            { Currency.GBP, 1.00f },
            { Currency.USD, 1.27f },
            { Currency.EUR, 1.16f },
            { Currency.KRW, 1700f },
        };

        public static readonly IReadOnlyDictionary<Currency, string> Symbols = new Dictionary<
            Currency,
            string
        >
        {
            { Currency.GBP, "£" },
            { Currency.USD, "$" },
            { Currency.EUR, "€" },
            { Currency.KRW, "₩" },
        };

        public static string Format(long gbpAmount, Currency currency)
        {
            float rate = ExchangeRates[currency];
            string sym = Symbols[currency];
            double converted = gbpAmount * rate;
            double abs = Math.Abs(converted);

            if (abs >= 1_000_000_000d)
                return $"{sym}{converted / 1_000_000_000d:0.0}B";
            if (abs >= 1_000_000d)
                return $"{sym}{converted / 1_000_000d:0.0}M";
            if (abs >= 1_000d)
                return $"{sym}{converted / 1_000d:0.0}K";
            return $"{sym}{converted:0}";
        }
    }
}
