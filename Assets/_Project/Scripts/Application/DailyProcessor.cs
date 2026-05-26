// DailyProcessor.cs
// data-flows.md #2 [2] 매일 백그라운드 처리.
// Stateless 시스템 (design-decisions.md #3) — state 입력받아 변경.
// V0.1 책임: fatigue 회복 + 부상 회복 카운트다운 + TransferSystem.ProcessOffers (Stage 11).
// V1.0 D.4: 매일 InjurySystem.ProcessRecovery (부상 회복 + 이벤트) + 매월 1일 GrowthSystem.Tick.
// V1.0 E.2: 매주 월요일 ScoutingSystem.UpdateKnowledge.
// V1.0 F.1+F.2: 매주 월요일 CpuTransferAi.Run (ScoutingSystem 다음 — 명단 활용).
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
                // TODO V1.0: ContractExpiring 알림 — currentDate vs contract.endDate
                //              (임계점: 6개월 전 / 1개월 전 / 만료일)
            }

            // 부상 회복 (V1.0 D.4 — V0.1 CheckInjuryRecovery 교체 + 이벤트 발행)
            // algorithms.md V1.0-11 ProcessRecovery
            InjurySystem.ProcessRecovery(state, balance);

            // 이적 오퍼 처리 — Pending → AI 응답 / Accepted → 활성화 기간 시 자동 체결
            // (algorithms.md #3.1 ProcessOffers)
            TransferSystem.ProcessOffers(state, balance);

            FacilitySystem.ProcessUpgrades(state);

            // 매월 1일 — 1군 선수 stat 성장 (algorithms.md V1.0-10)
            if (state.currentDate.Day == 1)
            {
                GrowthSystem.Tick(state, balance);
            }

            // 매주 월요일 — 스카우트 명단 갱신 + AI 영입 (F.1 + F.2 / design-decisions.md #47)
            if (state.currentDate.DayOfWeek == DayOfWeek.Monday)
            {
                ScoutingSystem.UpdateKnowledge(state, balance);
                CpuTransferAi.Run(state, balance);
            }
        }

        private static void RecoverFatigue(Player p, GameBalanceSO b)
        {
            if (p.state == null)
                return; // 방어 (PlayerGen 산출은 항상 non-null)
            p.state.fatigue = Math.Max(0, p.state.fatigue - b.fatigueRecoveryPerDay);
        }
    }
}
