// StatRowView.cs
// Stage C (V1.0) — PlayerProfile 49-stat 그리드 행 위젯. 카테고리 RowsContainer 아래 인스턴스화.
//   · 라벨 (stat 이름, Localization)
//   · 값 (등급 색 — StatColorCoding.GradeColor, C.2)
//   · 성장 증감 (직전 3개월 변화량 색 + 부호付 숫자 — StatColorCoding.Trend*, C.4)
// SetupText: 신체 bio 등 문자값 정보 행 (증감 숨김).
// 패턴: InboxEntryView 참조 (SerializeField + Setup + 표시 전용).

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
        /// stat 행. labelKey = Localization 키, value = 현재 값(0-100),
        /// change = 직전 3개월 변화량 (GrowthSystem.GetStatChange).
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
                arrowText.gameObject.SetActive(true);
                arrowText.text = StatColorCoding.TrendArrow(change);
                arrowText.color = StatColorCoding.TrendColor(change);
            }
        }

        /// <summary>정보 행 (문자값) — 신체 bio (키/몸무게/주발/약발) 등. 증감 숨김.</summary>
        public void SetupText(string labelKey, string value)
        {
            if (labelText != null)
                labelText.text = Localization.Get(labelKey);

            if (valueText != null)
            {
                valueText.text = value;
                valueText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            }

            if (arrowText != null)
                arrowText.gameObject.SetActive(false);
        }
    }
}
