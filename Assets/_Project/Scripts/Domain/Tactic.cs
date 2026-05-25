// Tactic.cs
// 팀 전술 — Formation + Mentality + 슬롯별 Role/Duty (design-decisions.md #45).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    public enum Mentality
    {
        VeryDefensive,
        Defensive,
        Cautious,
        Balanced,
        Positive,
        Attacking,
        VeryAttacking,
    }

    public enum Duty
    {
        Attack,
        Support,
        Defend,
    }

    [Serializable]
    public class TacticSlot
    {
        public int slotIndex; // 0-10 (11명)
        public int roleId; // PlayerRoleSO id
        public Duty duty;
        public int assignedPlayerId; // -1 = 자동 배정
    }

    [Serializable]
    public class Tactic
    {
        public int formationId; // FormationSO id
        public Mentality mentality;
        public List<TacticSlot> slots = new List<TacticSlot>();
        public List<int> setPieceTakers = new List<int>(); // playerId (PK / FK / CK 담당)
    }
}
