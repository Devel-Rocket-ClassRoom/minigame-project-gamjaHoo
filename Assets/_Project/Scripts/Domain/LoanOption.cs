// LoanOption.cs
// 임대 옵션 조건 (design-decisions.md #48).

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class LoanOption
    {
        public bool mandatoryPurchaseAtEnd; // 임대 종료 시 의무 구매
        public int purchaseClause; // 구매 옵션 금액 (0 = 없음)
        public bool recallClause; // 원 구단 조기 복귀 조항
    }
}
