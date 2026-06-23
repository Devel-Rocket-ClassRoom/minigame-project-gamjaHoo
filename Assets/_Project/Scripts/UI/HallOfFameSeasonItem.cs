// HallOfFameSeasonItem.cs
// 명예의 전당 — 내 역대 시즌 한 행 (HallOfFameScene 전용 프리팹 컨트롤러).
// 클라우드 SeasonRecord (Persistence.Cloud) 1행 — 연도 / 구단 / 최종순위 / 승점.

using FMLite.Application;
using FMLite.Persistence.Cloud;
using TMPro;
using UnityEngine;

namespace FMLite.UI
{
    public class HallOfFameSeasonItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text yearText;

        [SerializeField]
        private TMP_Text clubText;

        [SerializeField]
        private TMP_Text positionText;

        [SerializeField]
        private TMP_Text pointsText;

        public void Setup(SeasonRecord rec)
        {
            if (yearText != null)
                yearText.text = rec.year.ToString();
            if (clubText != null)
                clubText.text = rec.clubName ?? string.Empty;
            if (positionText != null)
                positionText.text = Localization.Get("hof_season_position_fmt", rec.position);
            if (pointsText != null)
                pointsText.text = Localization.Get("hof_season_points_fmt", rec.points);
        }
    }
}
