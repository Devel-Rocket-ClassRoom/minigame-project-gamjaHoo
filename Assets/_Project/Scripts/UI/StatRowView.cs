// StatRowView.cs
// Stage C (V1.0) — PlayerProfile 49-stat 그리드 행 위젯. 카테고리 GridLayout 아래 인스턴스화.
//   · 라벨 (stat 이름, Localization)
//   · 값 (등급 색 — StatColorCoding.GradeColor, C.2)
//   · 성장 증감 (직전 3개월 변화량 색 + 부호付 숫자 — StatColorCoding.Trend*, C.4)
// 패턴: InboxEntryView 참조 (SerializeField + Setup + 표시 전용).
// 호버 툴팁은 49행 전부 떠 산만 → 제거 (사용자 피드백, Sub-C 플레이테스트).

using FMLite.Application;
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

        /// <summary>
        /// 행 채우기. labelKey = Localization 키 ("stat_passing" 등),
        /// value = 현재 stat 값, change = 직전 3개월 변화량 (GrowthSystem.GetStatChange).
        /// </summary>
        public void Setup(string labelKey, int value, int change)
        {
            if (labelText != null)
                labelText.text = Localization.Get(labelKey);

            if (valueText != null)
            {
                valueText.text = value.ToString();
                valueText.color = StatColorCoding.GradeColor(value);
            }

            if (arrowText != null)
            {
                arrowText.text = StatColorCoding.TrendArrow(change);
                arrowText.color = StatColorCoding.TrendColor(change);
            }
        }
    }
}
