// YouthEvents.cs
// 유스 인스펙션 관련 이벤트. event-bus-catalog.md "Youth Events" 섹션.
// Stage 10 Task 10.1/10.2/10.3 (issues #39/#40/#41) 에서 발행.

using System.Collections.Generic;

namespace FMLite.Core
{
    public class YouthIntakeAvailableEvent
    {
        public int intakeId;
        public int clubId;
    }

    public class YouthRerolledEvent
    {
        public int intakeId;
        public int remainingTokens;
    }

    public class YouthSignedEvent
    {
        public int intakeId;
        public List<int> signedPlayerIds;
    }

    public class YouthPromotionSuggestedEvent
    {
        public int playerId;
        public int clubId;
    }
}
