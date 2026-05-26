// TransferEvents.cs
// 이적 관련 이벤트. event-bus-catalog.md "Transfer Events" 섹션.
// Stage 11 Task 11.1 (#42) 에서 발행. algorithms.md #3.1 Transfer Flow.

using FMLite.Domain;

namespace FMLite.Core
{
    public class OfferSubmittedEvent
    {
        public int offerId;
    }

    public class OfferRespondedEvent
    {
        public int offerId;
        public OfferStatus newStatus;
    }

    public class TransferCompletedEvent
    {
        public int offerId;
        public int playerId;
        public int fromClubId;
        public int toClubId;
        public int amount;
    }

    // V1.0 G.1 — MoraleSystem.OnPromiseBroken 이 happiness < transferRequestThreshold 시 발행.
    // algorithms.md V1.0-6 / design-decisions.md #42 (Q9 자동 트리거 + 유저 승인 패턴).
    public class TransferRequestEvent
    {
        public int playerId;
    }

    // V1.0 H.1 — TransferSystem.RenewContract 가 선수 수락 시 발행.
    // algorithms.md V1.0-3.1 RenewContract / design-decisions.md #48.
    public class ContractRenewedEvent
    {
        public int playerId;
    }

    // V1.0 H.1 — TransferSystem.RenewContract 가 선수 거절 시 발행.
    public class ContractRenewalRejectedEvent
    {
        public int playerId;
    }
}
