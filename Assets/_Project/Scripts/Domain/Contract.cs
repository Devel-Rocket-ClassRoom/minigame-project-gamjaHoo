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

        // V1.0 신규 보너스 조항 (design-decisions.md #48)
        public int signingBonus; // 계약 서명 보너스
        public int loyaltyBonus; // 만기 잔류 보너스
        public int appearanceBonus; // 시즌당 출전 수 달성 보너스
        public int goalBonus; // 시즌당 득점 수 달성 보너스
}
