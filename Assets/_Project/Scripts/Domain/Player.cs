// Player.cs
// 선수 도메인 엔티티. class-diagram.md 명세 기준.

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class Player
    {
        public int id;
        public PersonalInfo info;
        public Stats stats;
        public int currentAbility;
        public int potentialAbility;
        public List<int> traitIds = new List<int>();

        public int currentClubId;
        public int youthClubId; // 유스 데뷔 구단 (-1 = 외부)
        public PlayerOrigin origin;

        public Contract contract;
        public PlayerState state;
        public List<SeasonStat> career = new List<SeasonStat>();
        public int faceSeed;

        // V0.5 신규 (design-decisions.md #40, #48)
        public HiddenAttributes hiddenAttrs;
        public int parentClubId = -1; // 임대 시 원 소속 구단 (-1 = 임대 아님)
        public DateTime? loanEndDate; // 임대 종료일 (null = 임대 아님)
    }
}
