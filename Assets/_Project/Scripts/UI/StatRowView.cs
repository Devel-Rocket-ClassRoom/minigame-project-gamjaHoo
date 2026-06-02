// StatRowView.cs
// Stage C (V1.0) — PlayerProfile 49-stat 그리드 행 위젯. 카테고리 GridLayout 아래 인스턴스화.
//   · 라벨 (stat 이름, Localization)
//   · 값 (등급 색 — StatColorCoding.GradeColor, C.2)
//   · 성장 화살표 (직전 3개월 변화량 색/글리프 — StatColorCoding.Trend*, C.4)
//   · TooltipContent (MUIP, optional) — 호버 시 등급명 표시 (§11)
// 패턴: InboxEntryView 참조 (SerializeField + Setup + Hex 표시 전용).

using FMLite.Application;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;

namespace FMLite.UI
{
    public class StatRowView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text labelText;

        [SerializeField]
        private TMP_Text valueText;

        [SerializeField]
        private TMP_Text arrowText;

        [Header("Tooltip (optional)")]
        [SerializeField]
        private TooltipContent tooltip;

        /// <summary>
        /// 행 채우기. labelKey = Localization 키 ("stat_passing" 등),
        /// value = 현재 stat 값, change = 직전 3개월 변화량 (GrowthSystem.GetStatChange).
        /// </summary>
        public void Setup(string labelKey, int value, int change)
        {
            var grade = StatColorCoding.Classify(value);

            if (labelText != null)
                labelText.text = Localization.Get(labelKey);

            if (valueText != null)
            {
                valueText.text = value.ToString();
                valueText.color = StatColorCoding.GradeColor(grade);
            }

            if (arrowText != null)
            {
                arrowText.text = StatColorCoding.TrendArrow(change);
                arrowText.color = StatColorCoding.TrendColor(change);
            }

            if (tooltip != null)
                tooltip.description = Localization.Get(StatColorCoding.GradeNameKey(grade));
        }
    }
}
