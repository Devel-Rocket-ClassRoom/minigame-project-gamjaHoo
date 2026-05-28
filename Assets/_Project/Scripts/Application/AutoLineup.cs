// AutoLineup.cs
// J.5 — 자동 라인업. 각 슬롯의 포지션에 맞는 선수 중 AdjustedCA 최상위를 배정.
// 부상(injuryTypeId != -1) / 정지(suspendedMatches > 0) 제외.
// 같은 선수를 두 슬롯에 중복 배정하지 않음.
// design-decisions.md #45 / #57 (HasLineup 활성 조건).

using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class AutoLineup
    {
        // club.tactic.slots 에 best 선수를 자동 배정. GameDatabase 가 초기화돼 있어야 함.
        public static void AssignBest(Club club, GameState state)
        {
            if (club?.tactic?.slots == null || state == null)
                return;
            var formation = GameDatabase.GetFormation(club.tactic.formationId);
            if (formation?.slotPositions == null)
                return;

            var healthy = state
                .allPlayers.Where(p => p.currentClubId == club.id && IsAvailable(p))
                .ToList();

            var assigned = new HashSet<int>();

            for (int i = 0; i < club.tactic.slots.Count && i < formation.slotPositions.Length; i++)
            {
                var slot = club.tactic.slots[i];
                var pos = formation.slotPositions[i];

                var best = healthy
                    .Where(p => !assigned.Contains(p.id) && IsPositionCompatible(p, pos))
                    .OrderByDescending(AdjustedCA)
                    .FirstOrDefault();

                if (best != null)
                {
                    slot.assignedPlayerId = best.id;
                    assigned.Add(best.id);
                }
                else
                {
                    slot.assignedPlayerId = -1;
                }
            }
        }

        // 세트피스 담당자 자동 배정 — tactic.setPieceTakers[0..3]
        // 0=Penalty  1=FreeKick  2=Corner  3=ThrowIn
        // 기존 지정값이 있고 그 선수가 스쿼드에 있으면 유지, 아니면 stat 최고 선수로 교체.
        public static void AssignSetPieces(Club club, GameState state)
        {
            if (club?.tactic == null || state == null)
                return;

            EnsureSetPieceTakers(club.tactic, 4);

            var squad = state.allPlayers.Where(p => p.currentClubId == club.id).ToList();

            if (squad.Count == 0)
                return;

            // 0: Penalty — penaltyTaking
            if (!IsValidTaker(club.tactic.setPieceTakers[0], squad))
                club.tactic.setPieceTakers[0] = squad
                    .OrderByDescending(p => p.stats.technical.penaltyTaking)
                    .First()
                    .id;

            // 1: FreeKick — freeKickTaking
            if (!IsValidTaker(club.tactic.setPieceTakers[1], squad))
                club.tactic.setPieceTakers[1] = squad
                    .OrderByDescending(p => p.stats.technical.freeKickTaking)
                    .First()
                    .id;

            // 2: Corner — corners
            if (!IsValidTaker(club.tactic.setPieceTakers[2], squad))
                club.tactic.setPieceTakers[2] = squad
                    .OrderByDescending(p => p.stats.technical.corners)
                    .First()
                    .id;

            // 3: ThrowIn — longThrows
            if (!IsValidTaker(club.tactic.setPieceTakers[3], squad))
                club.tactic.setPieceTakers[3] = squad
                    .OrderByDescending(p => p.stats.technical.longThrows)
                    .First()
                    .id;
        }

        // setPieceTakers 리스트를 정확히 count 크기로 보장 (-1 = 자동).
        public static void EnsureSetPieceTakers(Tactic tactic, int count)
        {
            if (tactic.setPieceTakers == null)
                tactic.setPieceTakers = new System.Collections.Generic.List<int>();
            while (tactic.setPieceTakers.Count < count)
                tactic.setPieceTakers.Add(-1);
        }

        // 선수가 출전 가능한지 (부상·정지 없음).
        public static bool IsAvailable(Player p) =>
            p.state.injury?.injuryTypeId == -1 && p.state.suspendedMatches <= 0;

        // 포지션 호환: primaryPosition 또는 secondaryPositions 포함.
        public static bool IsPositionCompatible(Player p, Position pos) =>
            p.info.primaryPosition == pos
            || (p.info.secondaryPositions != null && p.info.secondaryPositions.Contains(pos));

        // 조정 CA = currentAbility + form×0.2 + morale×0.1
        public static float AdjustedCA(Player p) =>
            p.currentAbility + p.state.form * 0.2f + p.state.morale * 0.1f;

        private static bool IsValidTaker(int playerId, List<Player> squad) =>
            playerId >= 0 && squad.Any(p => p.id == playerId);
    }
}
