// DailyProcessor.cs
// data-flows.md #2 [2] 매일 백그라운드 처리.
// Stateless 시스템 (design-decisions.md #3) — state 입력받아 변경.
// V0.1 책임: fatigue 회복 + 부상 회복 카운트다운 + TransferSystem.ProcessOffers (Stage 11).
// V0.5 D.4: 매일 InjurySystem.ProcessRecovery (부상 회복 + 이벤트) + 매월 1일 GrowthSystem.Tick.
// V0.5 E.2: 매주 월요일 ScoutingSystem.UpdateKnowledge.
// V0.5 F.1+F.2: 매주 월요일 CpuTransferAi.Run (ScoutingSystem 다음 — 명단 활용).
// V0.5 G.1: 매일 MoraleSystem.Tick (사기 50 수렴 + Hidden professionalism 보정).
// V0.5 G.2: 매주 월요일 PromiseSystem.CheckProgress (deadline 도래 시 status 확정 + 사기 변동).
// V0.5 L.4: 매월 1일 MentoringSystem.RunMentoring (Hidden Attrs 수렴).
// V0.5 L.5: 매주 월요일 YouthSystem.CheckPromotionCandidates (1군 콜업 트리거).
// V0.5 추가 예정: 계약 만료 ContractExpiringEvent.

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
                // TODO V0.5: ContractExpiring 알림 — currentDate vs contract.endDate
                //              (임계점: 6개월 전 / 1개월 전 / 만료일)
            }

            // 부상 회복 (V0.5 D.4 — V0.1 CheckInjuryRecovery 교체 + 이벤트 발행)
            // algorithms.md V0.5-11 ProcessRecovery
            InjurySystem.ProcessRecovery(state, balance);

            // 사기 일일 회복 — 50 으로 수렴 (Hidden professionalism 보정).
            // algorithms.md V0.5-6 Tick / design-decisions.md #42.
            MoraleSystem.Tick(state, balance);

            // 이적 오퍼 처리 — Pending → AI 응답 / Accepted → 활성화 기간 시 자동 체결
            // (algorithms.md #3.1 ProcessOffers)
            TransferSystem.ProcessOffers(state, balance);

            // 임대 종료 자동 복귀 (algorithms.md V0.5-3.1 DailyProcessor 임대 복귀 처리)
            TransferSystem.ProcessLoanReturns(state);

            FacilitySystem.ProcessUpgrades(state);

            // 매월 1일 — stat 성장 + Mentoring Hidden Attr 수렴 + 월간 어워드 (M.3) + 보드 평가 (M.4)
            if (state.currentDate.Day == 1)
            {
                GrowthSystem.Tick(state, balance);
                MentoringSystem.RunMentoring(state, balance);
                SeasonAwardSystem.ComputeMonthlyAwards(state, balance);
                BoardSystem.EvaluateMonthly(state, balance);
            }

            // 매주 월요일 — 스카우트 명단 갱신 + AI 영입 + Promise 진행 체크
            // (F.1 + F.2 / design-decisions.md #47 + G.2 / design-decisions.md #43)
            if (state.currentDate.DayOfWeek == DayOfWeek.Monday)
            {
                ScoutingSystem.UpdateKnowledge(state, balance);
                CpuTransferAi.Run(state, balance);
                PromiseSystem.CheckProgress(state, balance);
                YouthSystem.CheckPromotionCandidates(state, balance);
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
