// MenteeProgressRow.cs
// V1.0 I.2 — 멘티 1명의 Hidden Attr(전문성/야망/충성도) 수렴 진행률 + 월별 변화량.
// MentoringGroupItem 이 그룹 멘티마다 인스턴스화.
// 진행률 = MentoringSystem.ConvergencePercent (mentee→mentor 근접도).
// 변화량 = MentoringSystem.ProjectedMonthlyStep (이번 달 수렴 스텝, rate 제한).

using FMLite.Application;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;

namespace FMLite.UI
{
    public class MenteeProgressRow : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text nameLabel;

        [Header("전문성 (professionalism)")]
        [SerializeField]
        private ProgressBar professionalismBar;

        [SerializeField]
        private TMP_Text professionalismDelta;

        [Header("야망 (ambition)")]
        [SerializeField]
        private ProgressBar ambitionBar;

        [SerializeField]
        private TMP_Text ambitionDelta;

        [Header("충성도 (loyalty)")]
        [SerializeField]
        private ProgressBar loyaltyBar;

        [SerializeField]
        private TMP_Text loyaltyDelta;

        public void Setup(
            string menteeName,
            HiddenAttributes mentor,
            HiddenAttributes mentee,
            int rateCap,
            float fraction
        )
        {
            if (nameLabel != null)
                nameLabel.text = menteeName;

            if (mentor == null || mentee == null)
                return;

            Apply(
                professionalismBar,
                professionalismDelta,
                mentor.professionalism,
                mentee.professionalism,
                rateCap,
                fraction
            );
            Apply(ambitionBar, ambitionDelta, mentor.ambition, mentee.ambition, rateCap, fraction);
            Apply(loyaltyBar, loyaltyDelta, mentor.loyalty, mentee.loyalty, rateCap, fraction);
        }

        private static void Apply(
            ProgressBar bar,
            TMP_Text deltaLabel,
            int mentorVal,
            int menteeVal,
            int rateCap,
            float fraction
        )
        {
            if (bar != null)
            {
                float pct = MentoringSystem.ConvergencePercent(mentorVal, menteeVal);
                bar.currentPercent = pct;
                bar.UpdateUI();
                // 게이지 상승 애니메이션 제거 — 수렴 상태를 즉시 표시 (의미 없는 0→값 lerp 방지).
                if (bar.loadingBar != null && bar.maxValue > 0)
                    bar.loadingBar.fillAmount = pct / bar.maxValue;
            }

            if (deltaLabel != null)
            {
                int step = MentoringSystem.ProjectedMonthlyStep(
                    mentorVal,
                    menteeVal,
                    rateCap,
                    fraction
                );
                deltaLabel.text =
                    step == 0
                        ? Localization.Get("mentoring_step_done")
                        : Localization.Get("mentoring_step_fmt", (step > 0 ? "+" : "") + step);
            }
        }
    }
}
