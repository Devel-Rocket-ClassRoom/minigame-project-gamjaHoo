// InjuryInfo.cs
// 부상 정보. V0.5 부상 시스템에서 본격 활용 예정.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class InjuryInfo
    {
        public int injuryTypeId;
        public DateTime startDate;
        public DateTime expectedReturn;
        public bool isCareerThreatening;
    }
}
