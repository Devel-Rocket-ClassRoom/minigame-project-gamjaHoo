// TacticImpact.cs
// J.4 — algorithms.md V0.5-7. Tactic (Role × Duty × Stat) 이 매치 이벤트 "주체 선택" 에 미치는 가중치.
// 호출 시점: MatchSimulator.SnapPlayer (이벤트 주체 선수 추첨) — 같은 팀 같은 라인 후보 간 상대 비교용.
// 단일 책임: Tactic + 선수 stats 입력 → 선택 가중치 산출 (Stateless, design-decisions.md #3 / #57).
//
// Mentality 와 form/morale/fatigue/mood 는 여기 미포함 (design-decisions.md #57):
//   - Mentality 는 팀 전체 곱셈 → 같은 팀 후보 간 선택에서 상수로 상쇄 + J.3 (MentalityShotMult 등) 이 zone 전이에 이미 적용 → double-counting 방지.
//   - form/morale/fatigue/mood 는 MatchSimulator.Eff() 가 성공률에 이미 적용 → double-counting 방지.
// roleId(코드 필드명) 로 PlayerRoleSO 조회. assignedPlayerId 미배정(-1) 슬롯은 role/duty=1.0 폴백 → J.5 라인업 배정 후 본격 작동.
// Duty 가중치는 GameBalanceSO 외부화 (매직넘버 금지, design-decisions.md #11).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class TacticImpact
    {
        // 이벤트 카테고리 — PlayerRoleSO.eventModifiers 의 string 키와 일치.
        public const string EventShot = "shot";
        public const string EventKeyPass = "keyPass";
        public const string EventTackle = "tackle";

        // stat 정규화 분모 — 1-100 두 stat 곱(최대 10000)을 0~1 로. 선택은 상대값이라 스케일 무관 (구조적 상수, MatchSimulator 의 /4.0·/3.0 stat 조합과 동일 정책).
        private const float StatWeightDivisor = 10000f;

        // 이벤트 주체 선택 가중치. tactic null / 슬롯 미배정 → roleWeight·dutyWeight = 1.0 (statWeight 만).
        public static float ComputeEventWeight(
            Tactic tactic,
            int playerId,
            GameState state,
            string eventType,
            GameBalanceSO balance
        )
        {
            var player = state?.GetPlayer(playerId);
            if (player == null)
                return 1f;

            float roleWeight = 1f;
            float dutyWeight = 1f;

            var slot = tactic?.slots?.FirstOrDefault(s => s.assignedPlayerId == playerId);
            if (slot != null)
            {
                roleWeight = RoleModifier(GameDatabase.GetPlayerRole(slot.roleId), eventType);
                dutyWeight = ComputeDutyWeight(slot.duty, eventType, balance);
            }

            return roleWeight * dutyWeight * ComputeStatWeight(player, eventType);
        }

        // PlayerRoleSO.eventModifiers 에서 eventType 보정값 조회 (없으면 1.0).
        private static float RoleModifier(PlayerRoleSO role, string eventType)
        {
            if (role?.eventModifiers == null)
                return 1f;
            foreach (var m in role.eventModifiers)
                if (m.eventType == eventType)
                    return m.multiplier;
            return 1f;
        }

        // Duty 보정 — Attack = 슈팅 ↑, Defend = 태클 ↑, Support = 키패스 ↑ (GameBalanceSO 외부화).
        public static float ComputeDutyWeight(Duty duty, string eventType, GameBalanceSO balance)
        {
            switch (eventType)
            {
                case EventShot:
                    return duty == Duty.Attack ? balance.tacticDutyPrimaryWeight
                        : duty == Duty.Support ? balance.tacticDutySecondaryWeight
                        : balance.tacticDutyOffWeight;
                case EventTackle:
                    return duty == Duty.Defend ? balance.tacticDutyPrimaryWeight
                        : duty == Duty.Support ? balance.tacticDutySecondaryWeight
                        : balance.tacticDutyOffWeight;
                case EventKeyPass:
                    return duty == Duty.Support
                        ? balance.tacticDutyKeyPassSupportWeight
                        : balance.tacticDutySecondaryWeight;
                default:
                    return 1f;
            }
        }

        // Stat 직접 참조 — 능력 좋은 선수일수록 해당 이벤트 주체로 더 자주 선택.
        public static float ComputeStatWeight(Player player, string eventType)
        {
            var tech = player.stats.technical;
            var mental = player.stats.mental;
            switch (eventType)
            {
                case EventShot:
                    return tech.finishing * mental.composure / StatWeightDivisor;
                case EventKeyPass:
                    return mental.vision * tech.passing / StatWeightDivisor;
                case EventTackle:
                    return tech.tackling * mental.positioning / StatWeightDivisor;
                default:
                    return 1f;
            }
        }

        // ── G.3 시너지 검출 (algorithms.md V1.0-3, #478) ──────────────────────
        // 배정된 라인업(slot.assignedPlayerId>=0)에서 활성 시너지 목록. tactic/라인업 미배정 → 빈 목록.
        public static List<SynergySO> ComputeSynergies(Tactic tactic, GameState state)
        {
            var active = new List<SynergySO>();
            if (tactic?.slots == null || state == null)
                return active;

            var assigned = tactic
                .slots.Where(s => s.assignedPlayerId >= 0)
                .Select(s => (slot: s, player: state.GetPlayer(s.assignedPlayerId)))
                .Where(x => x.player != null)
                .ToList();
            if (assigned.Count == 0)
                return active;

            foreach (var syn in GameDatabase.AllSynergies)
            {
                if (syn?.conditions == null || syn.conditions.Count == 0)
                    continue;
                bool allMet = true;
                foreach (var cond in syn.conditions)
                {
                    if (!ConditionMet(cond, assigned))
                    {
                        allMet = false;
                        break;
                    }
                }
                if (allMet)
                    active.Add(syn);
            }
            return active;
        }

        private static bool ConditionMet(
            SynergyCondition cond,
            List<(TacticSlot slot, Player player)> assigned
        )
        {
            var cands = assigned
                .Where(x =>
                    cond.positions == null
                    || cond.positions.Count == 0
                    || cond.positions.Contains(x.player.info.primaryPosition)
                )
                .ToList();

            int need = Math.Max(1, cond.minCount);

            if (cond.requireSameNationality)
            {
                int maxSameNation = cands
                    .Where(x => !string.IsNullOrEmpty(x.player.info.nationalityCode))
                    .GroupBy(x => x.player.info.nationalityCode)
                    .Select(g => g.Count())
                    .DefaultIfEmpty(0)
                    .Max();
                return maxSameNation >= need;
            }

            int matched = cands.Count(x =>
                EvalStatRequirement(x.player, cond.statRequirement)
                && EvalRoleRequirement(x.slot, cond.roleRequirement)
            );
            return matched >= need;
        }

        // "fieldPath op value" 를 '&' 로 여러 개 AND. 빈 문자열 = 무조건 true.
        private static bool EvalStatRequirement(Player player, string req)
        {
            if (string.IsNullOrWhiteSpace(req))
                return true;
            foreach (var clause in req.Split('&'))
            {
                if (!EvalClause(player, clause.Trim()))
                    return false;
            }
            return true;
        }

        private static bool EvalClause(Player player, string clause)
        {
            if (string.IsNullOrWhiteSpace(clause))
                return true;

            string op;
            int opIdx;
            if ((opIdx = clause.IndexOf(">=", StringComparison.Ordinal)) >= 0)
                op = ">=";
            else if ((opIdx = clause.IndexOf("<=", StringComparison.Ordinal)) >= 0)
                op = "<=";
            else if ((opIdx = clause.IndexOf("==", StringComparison.Ordinal)) >= 0)
                op = "==";
            else if ((opIdx = clause.IndexOf(">", StringComparison.Ordinal)) >= 0)
                op = ">";
            else if ((opIdx = clause.IndexOf("<", StringComparison.Ordinal)) >= 0)
                op = "<";
            else
                return false;

            string name = clause.Substring(0, opIdx).Trim();
            if (!int.TryParse(clause.Substring(opIdx + op.Length).Trim(), out int threshold))
                return false;

            int v = ResolveStatValue(player, name);
            switch (op)
            {
                case ">=":
                    return v >= threshold;
                case "<=":
                    return v <= threshold;
                case "==":
                    return v == threshold;
                case ">":
                    return v > threshold;
                case "<":
                    return v < threshold;
                default:
                    return false;
            }
        }

        // 특수 토큰(height/weight/weakFoot) → Player.physical, 그 외 → StatCatalog fieldPath.
        private static int ResolveStatValue(Player player, string name)
        {
            switch (name)
            {
                case "height":
                    return player.physical?.height ?? 0;
                case "weight":
                    return player.physical?.weight ?? 0;
                case "weakFoot":
                case "weakFootAbility":
                    return player.physical?.weakFootAbility ?? 0;
                default:
                    return StatCatalog.Read(player.stats, name);
            }
        }

        private static bool EvalRoleRequirement(TacticSlot slot, string roleReq)
        {
            if (string.IsNullOrWhiteSpace(roleReq))
                return true;
            var role = GameDatabase.GetPlayerRole(slot.roleId);
            return role != null
                && !string.IsNullOrEmpty(role.displayName)
                && role.displayName.IndexOf(roleReq, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
