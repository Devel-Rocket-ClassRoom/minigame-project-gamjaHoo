// Currency.cs
// V1.0 — 통화 enum (design-decisions.md #61).
// 도메인 저장값은 항상 GBP base, 표시 시점에 변환.

namespace FMLite.Utils
{
    public enum Currency
    {
        GBP = 0, // £ (base)
        USD = 1, // $
        EUR = 2, // €
        KRW = 3, // ₩
    }
}
