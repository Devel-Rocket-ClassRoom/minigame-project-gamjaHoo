// SeasonState.cs
// 구단별 시즌 목표 / 보드 신뢰도. V0.1: cupTarget 기본 None (컵 시스템은 V0.5+).

using System;
using System.Collections.Generic;

namespace FMLite.Domain
{
    [Serializable]
    public class SeasonState
    {
        public int targetLeaguePosition;
        public CupTarget cupTarget;
        public int boardConfidence;

        // V0.5 신규 (design-decisions.md #50, #51)
        public int captainPlayerId = -1;
        public int viceCaptainPlayerId = -1;
        public int dressingRoomMood; // 1군 Happiness 가중 평균
        public List<MentoringGroup> mentoringGroups = new List<MentoringGroup>();
        public List<BoardPromise> boardPromises = new List<BoardPromise>();
        public List<int> pendingPromotionPlayerIds = new List<int>();
    }
}
