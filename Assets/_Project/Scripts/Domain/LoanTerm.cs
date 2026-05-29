// LoanTerm.cs
// 임대 조건 DTO — TransferSystem.SubmitLoanOffer 파라미터 번들.
// design-decisions.md #48 / algorithms.md V0.5-3.1.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class LoanTerm
    {
        public int loanFee; // 임대료 (원 구단 수령)
        public float loanWageShare; // 임차 구단 wage 분담 비율 (0.0~1.0)
        public DateTime loanEndDate; // 임대 종료일
        public Contract proposed; // 임대 기간 적용 계약 (주급 등)
        public LoanOption option; // 구매 옵션 / 조기 복귀 조항
    }
}
