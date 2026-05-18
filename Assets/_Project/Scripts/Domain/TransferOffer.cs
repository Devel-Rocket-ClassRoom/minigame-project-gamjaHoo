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
    }
}
