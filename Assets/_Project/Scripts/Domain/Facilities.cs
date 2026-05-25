using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class Facilities
    {
        // 8 시설 등급 (1~10, 기본값 1)
        public int scoutLevel = 1;
        public int trainingLevel = 1;
        public int youthCoachLevel = 1;
        public int youthRecruitmentLevel = 1;
        public int youthFacilityLevel = 1;
        public int medicalLevel = 1;
        public int stadiumLevel = 1;
        public int gymLevel = 1;

        // 진행 중 업그레이드 목록 (D.3 병렬 업그레이드)
        public List<FacilityUpgrade> activeUpgrades = new List<FacilityUpgrade>();
    }
}
