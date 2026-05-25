// PlayerState.cs
// 선수 가변 상태 (피로, 사기, 폼, 부상, 이적 리스트, 출전 횟수).

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class PlayerState
    {
        public int fatigue;
        public int morale;
        public int form;
        public int happiness = 70; // 장기 만족도 0-100 (design-decisions.md #42)
        public InjuryInfo injury;
        public bool transferListed;
        public int seasonAppearances;
        public int suspendedMatches; // 출전 정지 잔여 경기 수 (카드 누적)
    }
}
