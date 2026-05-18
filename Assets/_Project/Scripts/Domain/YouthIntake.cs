// YouthIntake.cs
// 유스 인스펙션 풀. Player 를 소유하지 않고 ID 만 참조 (design-decisions.md #6).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class YouthIntake
    {
        public int id;
        public int clubId;
        public DateTime intakeDate;
        public List<int> candidatePlayerIds = new List<int>();
        public List<int> signedPlayerIds = new List<int>();
        public List<int> rejectedPlayerIds = new List<int>();
        public int rerollsUsed;
    }
}
