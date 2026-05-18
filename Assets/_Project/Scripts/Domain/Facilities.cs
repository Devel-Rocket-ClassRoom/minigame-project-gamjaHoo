// Facilities.cs
// 구단 시설 등급 (스카우트 / 훈련 / 유스).

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class Facilities
    {
        public int scoutLevel;
        public int trainingLevel;
        public int youthLevel;
    }
}
