// 시설 업그레이드 관련 이벤트 (event-bus-catalog.md 명세 기준).

using System;
using FMLite.Domain;

namespace FMLite.Core
{
    public class FacilityUpgradeStartedEvent
    {
        public FacilityType type;
        public int newLevel;
        public DateTime completionDate;
    }

    public class FacilityUpgradeCompletedEvent
    {
        public FacilityType type;
        public int newLevel;
    }
}
