// ScoutReport.cs
// 스카우트 보고서 + 부분 정보 공개 타입 (design-decisions.md #46).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class CaPaEstimate
    {
        public int estimate; // 중앙값 추정
        public int margin; // ± 오차 범위
    }

    [Serializable]
    public class HiddenAttributesPartial
    {
        public int? loyalty;
        public int? ambition;
        public int? professionalism;
        public int? pressureHandling;
        public int? temperament;
        public int? injuryProneness;
        public int? consistency;
        public int? versatility;
    }

    [Serializable]
    public class ScoutReport
    {
        public int playerId;
        public int scoutLevel; // 1-100: 시설 등급 + 스카우팅 시간 누적
        public DateTime lastUpdated;
        public CaPaEstimate caEstimate;
        public CaPaEstimate paEstimate;
        public List<int> revealedTraitIds = new List<int>();
        public HiddenAttributesPartial revealedHidden;
    }
}
