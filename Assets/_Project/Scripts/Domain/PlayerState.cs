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
        public InjuryInfo injury;
        public bool transferListed;
        public int seasonAppearances;
    }
}
