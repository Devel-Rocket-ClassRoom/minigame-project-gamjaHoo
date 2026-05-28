// TacticImpact.cs
// J.4 — algorithms.md V1.0-7. Tactic (Role × Duty × Stat) 이 매치 이벤트 "주체 선택" 에 미치는 가중치.
// 호출 시점: MatchSimulator.SnapPlayer (이벤트 주체 선수 추첨) — 같은 팀 같은 라인 후보 간 상대 비교용.
// 단일 책임: Tactic + 선수 stats 입력 → 선택 가중치 산출 (Stateless, design-decisions.md #3 / #57).
//
// Mentality 와 form/morale/fatigue/mood 는 여기 미포함 (design-decisions.md #57):
//   - Mentality 는 팀 전체 곱셈 → 같은 팀 후보 간 선택에서 상수로 상쇄 + J.3 (MentalityShotMult 등) 이 zone 전이에 이미 적용 → double-counting 방지.
//   - form/morale/fatigue/mood 는 MatchSimulator.Eff() 가 성공률에 이미 적용 → double-counting 방지.
// roleId(코드 필드명) 로 PlayerRoleSO 조회. assignedPlayerId 미배정(-1) 슬롯은 role/duty=1.0 폴백 → J.5 라인업 배정 후 본격 작동.
// Duty 가중치는 GameBalanceSO 외부화 (매직넘버 금지, design-decisions.md #11).

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
    }
}
