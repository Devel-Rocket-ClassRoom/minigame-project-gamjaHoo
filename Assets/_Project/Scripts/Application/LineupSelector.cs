// LineupSelector.cs
// Stage H.6 — 포메이션 기반 선발 11 선정 (algorithms.md V1.0-14 / design-decisions #71, #483).
// 구 "포지션 무시 CA top-11 폴백" 폐기. 유저 배정 슬롯 우선 + 빈 슬롯은 포메이션 슬롯 포지션에
// 적합한 선수(주>보조>같은라인) 를 CA + 노이즈로 자동 배치 (AI = 전 슬롯 자동, 항상 최적 X).
// Stateless (design-decisions #3): club/state/seed/balance 입력 → XI 출력. 영속 상태 변경 없음.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;
using Random = System.Random;

namespace FMLite.Application
{
    public static class LineupSelector
    {
        // 포지션 적합 점수 — 주 포지션 > 보조 > 같은 라인 > 그 외.
        private const double FitPrimary = 1000;
        private const double FitSecondary = 500;
        private const double FitSameLine = 200;

        // 선발 11 선정. 유저 배정(assignedPlayerId>=0) 우선, 빈 슬롯 포메이션 정합 자동 채움.
        // AI 는 슬롯 전부 미배정 → 전체 자동 (formation slotPositions 기준 + 노이즈).
        public static List<int> SelectStartingEleven(
            Club club,
            GameState state,
            int seed,
            GameBalanceSO balance
        )
        {
            var result = new List<int>(11);
            if (club == null || state == null)
                return result;

            var available = club.seniorSquadIds.Select(state.GetPlayer).Where(IsAvailable).ToList();

            var formation = GameDatabase.GetFormation(club.tactic?.formationId ?? 1);
            var slotPos = formation?.slotPositions;
            var tacticSlots = club.tactic?.slots;
            var rng = new Random(seed);
            double sigma = balance?.lineupNoiseSigma ?? 5.0;
            var used = new HashSet<int>();

            // 포메이션 슬롯 포지션이 있을 때만 정합 배치. 미로드 시 위치 편향 없이 CA 폴백(아래).
            int slotCount = slotPos?.Length ?? 0;
            for (int i = 0; i < slotCount && result.Count < 11; i++)
            {
                // 1) 유저 배정 우선
                int assigned =
                    tacticSlots != null && i < tacticSlots.Count
                        ? tacticSlots[i].assignedPlayerId
                        : -1;
                if (assigned >= 0 && !used.Contains(assigned))
                {
                    var ap = state.GetPlayer(assigned);
                    if (IsAvailable(ap))
                    {
                        result.Add(assigned);
                        used.Add(assigned);
                        continue;
                    }
                }

                // 2) 자동 채움 — 슬롯 포지션 적합 + CA + 노이즈
                var best = PickBestFit(available, used, slotPos[i], rng, sigma);
                if (best != null)
                {
                    result.Add(best.id);
                    used.Add(best.id);
                }
            }

            // 3) 슬롯 부족 / formation 미로드 → 남은 가용 CA 최상위로 보충 (포지션 편향 없음)
            if (result.Count < 11)
            {
                foreach (
                    var p in available
                        .Where(p => !used.Contains(p.id))
                        .OrderByDescending(p => p.currentAbility)
                        .Take(11 - result.Count)
                )
                {
                    result.Add(p.id);
                    used.Add(p.id);
                }
            }
            return result;
        }

        // AI 자동 라인업 (UI '자동 선택' 버튼 / 매치 폴백). 슬롯 무시하고 포메이션 포지션 기준 전체 자동.
        public static List<int> AiAutoLineup(
            Club club,
            GameState state,
            int seed,
            GameBalanceSO balance
        )
        {
            // tactic.slots 배정을 무시하고 자동 — 임시로 미배정 tactic 으로 SelectStartingEleven 호출.
            var tmp = new Tactic
            {
                formationId = club?.tactic?.formationId ?? 1,
                mentality = club?.tactic?.mentality ?? Mentality.Balanced,
                slots = new List<TacticSlot>(),
            };
            var saved = club.tactic;
            try
            {
                club.tactic = tmp;
                return SelectStartingEleven(club, state, seed, balance);
            }
            finally
            {
                club.tactic = saved;
            }
        }

        // 유저 라인업 유효성 — 11 슬롯 배정 + 부상/정지 아님 + 중복 없음 (강제 게이트용, H.4 MatchPreviewScene).
        public static bool HasValidLineup(Club club, GameState state)
        {
            var slots = club?.tactic?.slots;
            if (slots == null || slots.Count < 11 || state == null)
                return false;
            var seen = new HashSet<int>();
            int valid = 0;
            foreach (var slot in slots)
            {
                int pid = slot.assignedPlayerId;
                if (pid < 0 || !seen.Add(pid))
                    continue;
                if (IsAvailable(state.GetPlayer(pid)))
                    valid++;
            }
            return valid >= 11;
        }

        private static bool IsAvailable(Player p) =>
            p != null && p.state?.injury?.injuryTypeId == -1 && p.state.suspendedMatches <= 0;

        private static Player PickBestFit(
            List<Player> available,
            HashSet<int> used,
            Position pos,
            Random rng,
            double sigma
        )
        {
            Player best = null;
            double bestScore = double.NegativeInfinity;
            foreach (var p in available)
            {
                if (used.Contains(p.id))
                    continue;
                double score =
                    PositionFit(p, pos) + p.currentAbility + (rng.NextDouble() * 2.0 - 1.0) * sigma; // 균등 ±sigma 노이즈
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        private static double PositionFit(Player p, Position slot)
        {
            var primary = p.info.primaryPosition;
            if (primary == slot)
                return FitPrimary;
            if (p.info.secondaryPositions != null && p.info.secondaryPositions.Contains(slot))
                return FitSecondary;
            if (StartingSquadGacha.LineOf(primary) == StartingSquadGacha.LineOf(slot))
                return FitSameLine;
            return 0;
        }
    }
}
