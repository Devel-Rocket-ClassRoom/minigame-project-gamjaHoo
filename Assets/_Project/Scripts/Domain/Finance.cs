// Finance.cs
// 구단 재정.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class Finance
    {
        public int money;
        public int debt;
        public int transferBudget;
        public int wageBudget;
    }
}
