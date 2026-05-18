// Contract.cs
// 선수 계약 정보.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class Contract
    {
        public int weeklyWage;
        public DateTime startDate;
        public DateTime endDate;
        public int releaseClause;
    }
}
