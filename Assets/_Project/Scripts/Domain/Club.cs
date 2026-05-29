// Club.cs
// 구단 도메인 엔티티. class-diagram.md 명세 기준.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class Club
    {
        public int id;
        public string name;
        public int foundedYear;
        public int leagueId;

        public Finance finance;
        public int reputation;
        public Facilities facilities;

        public List<int> seniorSquadIds = new List<int>();
        public List<int> youthSquadIds = new List<int>();
        public List<YouthIntake> intakeHistory = new List<YouthIntake>();

        public SeasonState season;
        public bool isActiveSimulation;

        // V0.5 신규 (design-decisions.md #45, #46)
        public Tactic tactic;
        public Dictionary<int, ScoutReport> scoutingKnowledge = new Dictionary<int, ScoutReport>();
    }
}
