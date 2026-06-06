// 리그 개인 리더보드 행 프리팹 컨트롤러.
// V1.0 M.1 / R.11 (#528): LeaderboardEntry 한 행 표시 — 순위 / 선수명 / 소속 / 값.
// 자기 구단 소속 선수 강조 (clubId == userClubId).

using FMLite.Application;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class LeaderboardEntryItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text rankText;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text clubText;

        [SerializeField]
        private TMP_Text valueText;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Image clubCrest; // Stage AD — 소속 구단 크레스트 (미배선/미생성 시 자동 숨김)

        [SerializeField]
        private Color userClubColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);

        [SerializeField]
        private Color defaultColor = new Color(0f, 0f, 0f, 0f);

        public void Setup(
            LeaderboardEntry entry,
            GameState state,
            int userClubId,
            LeaderboardCategory category
        )
        {
            var player = state.GetPlayer(entry.playerId);
            var club = state.GetClub(entry.clubId);

            if (rankText != null)
                rankText.text = entry.rank.ToString();

            if (nameText != null)
                nameText.text =
                    player?.info != null
                        ? $"{player.info.firstName} {player.info.lastName}"
                        : $"id={entry.playerId}";

            if (clubText != null)
                clubText.text = club?.name ?? $"id={entry.clubId}";
            CrestProvider.ApplyClubCrest(clubCrest, club?.name);

            if (valueText != null)
                valueText.text =
                    category == LeaderboardCategory.Rating
                        ? entry.value.ToString("F2")
                        : ((int)entry.value).ToString();

            if (backgroundImage != null)
                backgroundImage.color = entry.clubId == userClubId ? userClubColor : defaultColor;
        }
    }
}
