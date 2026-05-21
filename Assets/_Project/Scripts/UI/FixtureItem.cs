// 일정/결과 목록 행 프리팹 컨트롤러.

using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class FixtureItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text dateText;

        [SerializeField]
        private TMP_Text matchText;

        [SerializeField]
        private TMP_Text scoreText;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Color userClubColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);

        [SerializeField]
        private Color defaultColor = new Color(0f, 0f, 0f, 0f);

        public void Setup(Match match, GameState state, int userClubId)
        {
            bool isHome = match.homeClubId == userClubId;
            bool isAway = match.awayClubId == userClubId;

            var homeClub = state.GetClub(match.homeClubId);
            var awayClub = state.GetClub(match.awayClubId);

            if (dateText != null)
                dateText.text = match.date.ToString("MM/dd");

            if (matchText != null)
                matchText.text = $"{homeClub?.name ?? "?"} vs {awayClub?.name ?? "?"}";

            if (scoreText != null)
            {
                if (match.result != null)
                    scoreText.text = $"{match.result.homeScore} - {match.result.awayScore}";
                else
                    scoreText.text = match.date.ToString("HH:mm");
            }

            if (backgroundImage != null)
                backgroundImage.color = (isHome || isAway) ? userClubColor : defaultColor;
        }
    }
}
