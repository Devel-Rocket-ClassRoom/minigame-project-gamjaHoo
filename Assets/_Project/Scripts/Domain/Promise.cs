// Promise.cs
// 매니저-선수 약속 도메인 엔티티 4종 (design-decisions.md #43).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    public enum PromiseType
    {
        PlaytimeAgreement, // 출전 시간 보장
        TransferIn, // 시즌 중 특정 포지션 영입 약속
        Renewal, // 재계약 약속
        TransferOut, // 이적 허용 약속
    }

    public enum PromiseStatus
    {
        Active,
        Fulfilled,
        Broken,
    }

    [Serializable]
    public class Promise
    {
        public int id;
        public int playerId;
        public PromiseType type;
        public DateTime madeAt;
        public DateTime deadline;
        public PromiseStatus status;
        public Dictionary<string, int> targets = new Dictionary<string, int>();
    }
}
