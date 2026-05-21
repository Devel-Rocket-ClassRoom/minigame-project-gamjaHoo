// 리그 순위표 행 프리팹 컨트롤러.

using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class StandingsEntryItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text rankText;

        [SerializeField]
        private TMP_Text clubText;

        [SerializeField]
        private TMP_Text playedText;

        [SerializeField]
        private TMP_Text wonText;

        [SerializeField]
        private TMP_Text drawnText;

        [SerializeField]
        private TMP_Text lostText;

        [SerializeField]
        private TMP_Text gdText;

        [SerializeField]
        private TMP_Text pointsText;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Color userClubColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);

        [SerializeField]
        private Color defaultColor = new Color(0f, 0f, 0f, 0f);

        public void Setup(int rank, StandingEntry entry, GameState state, int userClubId)
        {
            var club = state.GetClub(entry.clubId);
            int gd = entry.goalsFor - entry.goalsAgainst;

            if (rankText != null)
                rankText.text = rank.ToString();
            if (clubText != null)
                clubText.text = club?.name ?? $"id={entry.clubId}";
            if (playedText != null)
                playedText.text = entry.played.ToString();
            if (wonText != null)
                wonText.text = entry.won.ToString();
            if (drawnText != null)
                drawnText.text = entry.drawn.ToString();
            if (lostText != null)
                lostText.text = entry.lost.ToString();
            if (gdText != null)
                gdText.text = (gd >= 0 ? "+" : "") + gd;
            if (pointsText != null)
                pointsText.text = entry.points.ToString();

            if (backgroundImage != null)
                backgroundImage.color = entry.clubId == userClubId ? userClubColor : defaultColor;
        }
    }
}
