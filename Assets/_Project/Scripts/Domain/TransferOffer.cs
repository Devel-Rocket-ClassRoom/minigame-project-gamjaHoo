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

        // V0.5 임대 필드 (design-decisions.md #48)
        public bool isLoan;
        public int loanFee;
        public float loanWageShare; // 0.0-1.0 (임차 구단이 부담하는 주급 비율)
        public DateTime loanEndDate;
        public LoanOption loanOption;

        // V0.5 협상 필드
        public int counterAmount; // 판매 구단 역제안 금액
        public int negotiationRound; // 이적료 협상 라운드 횟수 (구단 역제안)
        public int personalNegotiationRound; // 선수 개인 협상 라운드 횟수 (V1.0 #469, Negotiating 단계)
        public bool releaseClauseActivated; // release clause 발동 여부
        public bool includesPlaytimeAgreement; // 출전시간 약속 포함 여부 (K.2 선수 협상 +0.2)

        // AI 응답 도착 일자 — EventScheduler 가 stopRequested 트리거하기 위한 표식 (#384).
        // AiRespondToOffer 가 설정. 같은 날 EventScheduler.Run 이 user 클럽 오퍼면 Continue 정지.
        public DateTime? lastResponseDate;
    }
}
