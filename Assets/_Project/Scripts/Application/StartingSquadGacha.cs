// StartingSquadGacha.cs
// algorithms.md #6 Starting Squad Gacha 4단계 + Reroll 정책 구현.
// 호출 시점: GameInitializer 가 구단 선택 후 / 유저 Reroll 버튼.
// data-flows.md #1 [4].

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FMLite.Domain;
using Random = System.Random;

namespace FMLite.Application
{
    public static class StartingSquadGacha
    {
        // ── 4라인 분류 (algorithms.md #6 1단계, V0.1 하드코딩) ──────────

        private static readonly HashSet<Position> GkPositions =
            new HashSet<Position> { Position.GK };
        private static readonly HashSet<Position> DfPositions =
            new HashSet<Position> { Position.CB, Position.LB, Position.RB, Position.WB };
        private static readonly HashSet<Position> MfPositions =
            new HashSet<Position> { Position.DM, Position.CM, Position.AM, Position.LM, Position.RM };
        private static readonly HashSet<Position> AtPositions =
            new HashSet<Position> { Position.LW, Position.RW, Position.ST, Position.CF };

        public static Line LineOf(Position pos)
        {
            if (GkPositions.Contains(pos)) return Line.GK;
            if (DfPositions.Contains(pos)) return Line.DF;
            if (MfPositions.Contains(pos)) return Line.MF;
            if (AtPositions.Contains(pos)) return Line.AT;
            // 비-등록 포지션은 MF 폴백 (현재 enum 14개 모두 위 4 세트 합집합)
            return Line.MF;
        }

        // ── EvaluateSquad ─────────────────────────────────────────────

        public static SquadEvaluation EvaluateSquad(Club club, GameState state, GameBalanceSO balance)
        {
            if (club == null)    throw new ArgumentNullException(nameof(club));
            if (state == null)   throw new ArgumentNullException(nameof(state));
            if (balance == null) throw new ArgumentNullException(nameof(balance));

            // 2단계: 라인별 평균 CA
            var byLine = new Dictionary<Line, List<int>>
            {
                [Line.GK] = new List<int>(),
                [Line.DF] = new List<int>(),
                [Line.MF] = new List<int>(),
                [Line.AT] = new List<int>(),
            };

            Player acePlayer = null;
            foreach (var id in club.seniorSquadIds)
            {
                var p = state.GetPlayer(id);
                if (p == null) continue;
                byLine[LineOf(p.info.primaryPosition)].Add(p.currentAbility);

                // 4단계: ACE 마커 (전체 최고 CA, 동률 시 첫 매치 유지)
                if (acePlayer == null || p.currentAbility > acePlayer.currentAbility)
                    acePlayer = p;
            }

            // 3단계: 명성 대비 비율 → 5단계 티어
            double expectedMean =
                balance.caRepBase + (double)balance.caRepCoeff * club.reputation;
            if (expectedMean < 1.0) expectedMean = 1.0;        // 0 나누기 방어

            return new SquadEvaluation
            {
                gk = GradeLine(byLine[Line.GK], expectedMean, balance),
                df = GradeLine(byLine[Line.DF], expectedMean, balance),
                mf = GradeLine(byLine[Line.MF], expectedMean, balance),
                at = GradeLine(byLine[Line.AT], expectedMean, balance),
                acePosition = acePlayer != null ? LineOf(acePlayer.info.primaryPosition) : Line.GK,
                aceLineCA   = acePlayer?.currentAbility ?? 0,
            };
        }

        // 부동소수점 비교 epsilon — GameBalanceSO 의 float 컷 (1.20f 등)
        // 이 정확히 표현 불가능 (0.8f×50=40.0000006 같은 오차). 경계선 케이스
        // (예: ratio 가 의도상 1.20 인데 실제 1.1999999...) 가 의도와 다른 티어로
        // 떨어지지 않게 epsilon 흡수. 1e-6 은 float 정밀도 한참 위.
        private const double TierEpsilon = 1e-6;

        private static TierGrade GradeLine(List<int> abilities, double expectedMean, GameBalanceSO b)
        {
            if (abilities.Count == 0)
            {
                Debug.LogWarning("[StartingSquadGacha] line 에 선수 0명 — Poor 폴백");
                return TierGrade.Poor;
            }
            double avg   = abilities.Average();
            double ratio = avg / expectedMean;

            if (ratio >= b.tierEliteRatio   - TierEpsilon) return TierGrade.Elite;
            if (ratio >= b.tierStrongRatio  - TierEpsilon) return TierGrade.Strong;
            if (ratio >= b.tierAverageRatio - TierEpsilon) return TierGrade.Average;
            if (ratio >= b.tierWeakRatio    - TierEpsilon) return TierGrade.Weak;
            return TierGrade.Poor;
        }

        // ── RerollSquad (algorithms.md #6 Reroll 정책) ─────────────────

        public static SquadEvaluation RerollSquad(
            Club club,
            GameState state,
            LeagueConfigSO leagueConfig,
            GameBalanceSO balance,
            DateTime currentDate,
            Random rng)
        {
            if (state.rerollTokens <= 0)
                throw new InvalidOperationException(
                    "RerollSquad: state.rerollTokens 가 0 이하 — 호출자 (UI) 가 버튼 비활성화로 차단해야 함");

            state.rerollTokens -= 1;

            // 기존 25명 제거 (design-decisions.md #31)
            foreach (var id in club.seniorSquadIds.ToList())
                state.RemovePlayer(id);
            club.seniorSquadIds.Clear();

            // 새 스쿼드 생성 — ClubGen 위임, state.nextPlayerId 사용
            var newPlayers = ClubGenerator.RegenerateSquad(
                rng, club, leagueConfig, balance, currentDate, state.nextPlayerId);

            foreach (var p in newPlayers)
            {
                state.AddPlayer(p);
                club.seniorSquadIds.Add(p.id);
            }
            state.nextPlayerId += newPlayers.Count;

            return EvaluateSquad(club, state, balance);
        }
    }

    // ── 값 객체 / enum ───────────────────────────────────────────────

    public class SquadEvaluation
    {
        public TierGrade gk;
        public TierGrade df;
        public TierGrade mf;
        public TierGrade at;
        public Line      acePosition;
        public int       aceLineCA;       // 디버그용 — UI 표시는 라인만 (design-decisions.md #14)
    }

    public enum TierGrade { Poor, Weak, Average, Strong, Elite }
    public enum Line      { GK, DF, MF, AT }
}
