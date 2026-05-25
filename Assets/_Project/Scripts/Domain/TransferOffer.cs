// TransferOffer.cs
// 이적 오퍼 도메인 엔티티. class-diagram.md 명세 기준.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class TransferOffer
    {
        public int id;
        public int playerId;
        public int fromClubId;
        public int toClubId;
        public int amount;
        public Contract proposed;
        public OfferStatus status;

        // V1.0 임대 필드 (design-decisions.md #48)
        public bool isLoan;
        public int loanFee;
        public float loanWageShare; // 0.0-1.0 (임차 구단이 부담하는 주급 비율)
        public DateTime loanEndDate;
        public LoanOption loanOption;

        // V1.0 협상 필드
        public int counterAmount; // 판매 구단 역제안 금액
        public int negotiationRound; // 협상 라운드 횟수
        public bool releaseClauseActivated; // release clause 발동 여부
    }
}
