// League.cs
// 리그 도메인 엔티티. class-diagram.md 명세 기준.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class League
    {
        public int id;
        public int configSOId;          // LeagueConfigSO 참조 ID
        public int seasonYear;
        public List<int> clubIds = new List<int>();
        public List<Match> schedule = new List<Match>();
        public Standings standings;
    }
}
