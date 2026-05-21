// DailyProcessor.cs
// data-flows.md #2 [2] 매일 백그라운드 처리.
// Stateless 시스템 (design-decisions.md #3) — state 입력받아 변경.
// V0.1 책임: fatigue 회복 + 부상 회복 카운트다운 + TransferSystem.ProcessOffers (Stage 11).
// V1.0 추가 예정: 사기/모랄 일일 변동, 계약 만료 ContractExpiringEvent.

using System;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class DailyProcessor
    {
        public static void Run(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            foreach (var player in state.allPlayers)
            {
                RecoverFatigue(player, balance);
                CheckInjuryRecovery(player, state.currentDate);
                // TODO V1.0: ContractExpiring 알림 — currentDate vs contract.endDate
                //              (임계점: 6개월 전 / 1개월 전 / 만료일)
            }

            // 이적 오퍼 처리 — Pending → AI 응답 / Accepted → 활성화 기간 시 자동 체결
            // (algorithms.md #3.1 ProcessOffers)
            TransferSystem.ProcessOffers(state, balance);

            FacilitySystem.ProcessUpgrades(state);
        }

        private static void RecoverFatigue(Player p, GameBalanceSO b)
        {
            if (p.state == null)
                return; // 방어 (PlayerGen 산출은 항상 non-null)
            p.state.fatigue = Math.Max(0, p.state.fatigue - b.fatigueRecoveryPerDay);
        }

        private static void CheckInjuryRecovery(Player p, DateTime today)
        {
            var injury = p.state?.injury;
            if (injury == null)
                return;
            if (injury.injuryTypeId == -1)
                return; // 부상 없음 sentinel
            if (today < injury.expectedReturn)
                return; // 아직 회복 안 됨

            // 회복 — sentinel 로 리셋
            injury.injuryTypeId = -1;
            injury.isCareerThreatening = false;
            // startDate / expectedReturn 은 그대로 둠 (디버그/로그 가치).
        }
    }
}
