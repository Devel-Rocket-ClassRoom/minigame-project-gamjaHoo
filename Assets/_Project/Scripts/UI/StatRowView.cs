// StatRowView.cs
// Stage C (V1.0) — PlayerProfile 49-stat 그리드 행 위젯. 카테고리 RowsContainer 아래 인스턴스화.
//   · 라벨 (stat 이름, Localization)
//   · 게이지 바 (값/100, 등급 색 — FM식 시각화)
//   · 값 (등급 색 — StatColorCoding.GradeColor, C.2)
//   · 성장 증감 (직전 3개월 변화량 색 + 부호付 숫자 — StatColorCoding.Trend*, C.4)
// SetupText: 신체 bio 등 문자값 정보 행 (게이지/증감 없음).
// 패턴: InboxEntryView 참조 (SerializeField + Setup + 표시 전용).

using FMLite.Application;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Tooltip("FM식 게이지 — Image type=Filled (Horizontal). 값/100 fill, 등급 색.")]
        [SerializeField]
        private Image valueBar;

        /// <summary>
        /// stat 행. labelKey = Localization 키, value = 현재 값(0-100),
        /// change = 직전 3개월 변화량 (GrowthSystem.GetStatChange).
        /// </summary>
        public void Setup(string labelKey, int value, int change)
        {
            var color = StatColorCoding.GradeColor(value);

            if (labelText != null)
                labelText.text = Localization.Get(labelKey);

            if (valueText != null)
            {
                valueText.text = value.ToString();
                valueText.color = color;
            }

            if (valueBar != null)
            {
                valueBar.gameObject.SetActive(true);
                valueBar.color = color;
                valueBar.fillAmount = Mathf.Clamp01(value / 100f);
            }

            if (arrowText != null)
            {
                arrowText.gameObject.SetActive(true);
                arrowText.text = StatColorCoding.TrendArrow(change);
                arrowText.color = StatColorCoding.TrendColor(change);
            }
        }

        /// <summary>
        /// 정보 행 (문자값) — 신체 bio (키/몸무게/주발/약발) 등. 게이지·증감 숨김.
        /// </summary>
        public void SetupText(string labelKey, string value)
        {
            if (labelText != null)
                labelText.text = Localization.Get(labelKey);

            if (valueText != null)
            {
                valueText.text = value;
                valueText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            }

            if (valueBar != null)
                valueBar.gameObject.SetActive(false);

            if (arrowText != null)
                arrowText.gameObject.SetActive(false);
        }
    }
}
