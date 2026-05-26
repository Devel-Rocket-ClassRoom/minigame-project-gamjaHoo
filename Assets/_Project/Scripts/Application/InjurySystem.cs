// InjurySystem.cs
// V1.0 D.4 Sub-B — Injury Recovery + Rate (algorithms.md V1.0-11).
// Stateless 시스템 (design-decisions.md #3).
//
// 책임:
//   - ComputeRecoveryDays: 매치 엔진 (Stage I.3) 이 부상 발생 시 expectedReturn 계산에 사용.
//   - ComputeInjuryRate: 매 분 부상 발생 확률 계산 (V1.0-2 분 단위 이벤트 시퀀스에서 호출).
//   - ProcessRecovery: 매일 DailyProcessor 호출 — expectedReturn 도래 시 부상 해제 + 이벤트.
//
// 시설 보정:
//   - 회복 일수 = base / (1 + Medical × 0.05 + Gym × 0.02)
//   - 발생률 = baseRate × max(0.5, 1 - Medical × 0.05)  ← floor 0.5
//
// 결정성: 부상 발생 시점에 expectedReturn 고정 (시드 derived in match engine).
// 임대 (Stage K.3): 현재 소속 (currentClubId) 시설 영향. 회복 도중 임대 이동 시 expectedReturn 고정.

using System;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class InjurySystem
    {
        // Stage I.3 매치 엔진에서 부상 발생 시 호출.
        // baseDays = InjuryTypeSO.recoveryDays (Sprained Ankle ~14, ACL ~180 등).
        // Lv0 가능 (이론, 실제는 Lv1 부터) — 보정 = 1.0.
        public static int ComputeRecoveryDays(
            int baseDays,
            int medicalLevel,
            int gymLevel,
            GameBalanceSO balance
        )
        {
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            float medicalReduction = 1f + medicalLevel * balance.injuryMedicalRecoveryCoeff;
            float gymReduction = 1f + gymLevel * balance.injuryGymRecoveryCoeff;

            int actualDays = (int)Math.Round(baseDays / (medicalReduction * gymReduction));
            return Math.Max(1, actualDays);
        }

        // Stage I.3 매치 엔진에서 매 분 부상 발생 확률 곱셈 계수.
        // 실제 발생 확률 = baseRate × ComputeInjuryRate(...) × player.hiddenAttrs.injuryProneness / 50.
        // floor 0.5 — Medical Lv10 도 부상 완전 차단 불가.
        public static float ComputeInjuryRate(int medicalLevel, GameBalanceSO balance)
        {
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            float factor = 1f - medicalLevel * balance.injuryMedicalRateCoeff;
            return Math.Max(0.5f, factor);
        }

        // DailyProcessor 매일 호출. expectedReturn 도래 시 부상 해제.
        // V0.1 DailyProcessor.CheckInjuryRecovery 대체 (+ 이벤트 발행).
        public static void ProcessRecovery(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            DateTime today = state.currentDate;

            foreach (var player in state.allPlayers)
            {
                var injury = player.state?.injury;
                if (injury == null || injury.injuryTypeId == -1)
                    continue;
                if (today < injury.expectedReturn)
                    continue;

                // 회복 — sentinel 로 리셋
                injury.injuryTypeId = -1;
                injury.isCareerThreatening = false;
                EventBus.Publish(new PlayerInjuryRecoveredEvent { playerId = player.id });
            }
        }
    }
}
