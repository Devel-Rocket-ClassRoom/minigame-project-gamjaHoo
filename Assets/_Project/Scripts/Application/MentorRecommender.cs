// MentorRecommender.cs
// V1.0 I.3 — 멘토 자동 추천. Stateless (design-decisions.md #3).
// 점수 = leadership + min(age, cap)*ageW + max(계약잔여,0)*contractW + Hidden평균*hiddenW
// CaptainSystem.Score 패턴 (leadership + 나이 + 계약) 에 멘토링 본질(전수 대상 Hidden Attr 품질) 가산.
// 대상 Hidden Attrs: professionalism / ambition / loyalty (MentoringSystem 수렴 대상과 동일).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class MentorRecommender
    {
        // 후보 중 최고 점수 멘토 id. 후보 없으면 -1.
        public static int RecommendMentor(
            IEnumerable<int> candidateIds,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (candidateIds == null || state == null || balance == null)
                return -1;

            int best = -1;
            float bestScore = float.NegativeInfinity;
            foreach (var id in candidateIds)
            {
                var p = state.GetPlayer(id);
                if (p == null)
                    continue;
                float s = Score(p, state.currentDate, balance);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = id;
                }
            }
            return best;
        }

        public static float Score(Player p, DateTime currentDate, GameBalanceSO balance)
        {
            if (p?.info == null || p.contract == null || p.stats?.mental == null)
                return 0f;

            float age = (float)((currentDate - p.info.birthDate).TotalDays / 365.25);
            float yearsLeft = (float)((p.contract.endDate - currentDate).TotalDays / 365.25);

            float leadership = p.stats.mental.leadership;
            float hiddenMean = HiddenMean(p.hiddenAttrs);

            return leadership
                + Math.Min(age, balance.mentorAgeCap) * balance.mentorAgeWeight
                + Math.Max(yearsLeft, 0f) * balance.mentorContractWeight
                + hiddenMean * balance.mentorHiddenWeight;
        }

        // 전수 대상 3종 평균 (MentoringSystem.ConvergeAttrs 와 동일 대상).
        public static float HiddenMean(HiddenAttributes h)
        {
            if (h == null)
                return 0f;
            return (h.professionalism + h.ambition + h.loyalty) / 3f;
        }
    }
}
