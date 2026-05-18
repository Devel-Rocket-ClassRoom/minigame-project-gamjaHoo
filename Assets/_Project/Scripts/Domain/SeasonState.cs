// SeasonState.cs
// 구단별 시즌 목표 / 보드 신뢰도. V0.1: cupTarget 기본 None (컵 시스템은 V1.0+).

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class SeasonState
    {
        public int targetLeaguePosition;
        public CupTarget cupTarget;
        public int boardConfidence;
    }
}
