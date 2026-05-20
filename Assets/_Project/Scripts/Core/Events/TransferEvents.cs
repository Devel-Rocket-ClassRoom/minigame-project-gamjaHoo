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
}
