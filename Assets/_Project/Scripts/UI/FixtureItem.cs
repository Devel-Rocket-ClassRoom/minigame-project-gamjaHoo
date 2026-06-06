// 일정/결과 목록 행 프리팹 컨트롤러.
// V1.0 M.2 / AA.6 (#528): 완료 매치(result != null) 행 클릭 → MatchResultDashboard 진입.

using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class FixtureItem : MonoBehaviour
    {
        private const string MatchResultDashboard = "MatchResultDashboard";

        [SerializeField]
        private TMP_Text dateText;

        [SerializeField]
        private TMP_Text matchText;

        [SerializeField]
        private TMP_Text scoreText;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Image homeCrest; // Stage AD — 홈 구단 크레스트 (미배선/미생성 시 자동 숨김)

        [SerializeField]
        private Image awayCrest; // Stage AD — 원정 구단 크레스트

        [SerializeField]
        private Button rowButton; // 완료 매치 클릭 진입 (AA.6)

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
            CrestProvider.ApplyClubCrest(homeCrest, homeClub?.name);
            CrestProvider.ApplyClubCrest(awayCrest, awayClub?.name);

            bool completed = match.result != null;

            if (scoreText != null)
            {
                if (completed)
                    scoreText.text = $"{match.result.homeScore} - {match.result.awayScore}";
                else
                    scoreText.text = match.date.ToString("HH:mm");
            }

            if (backgroundImage != null)
                backgroundImage.color = (isHome || isAway) ? userClubColor : defaultColor;

            // AA.6: 완료 매치만 클릭 진입. 미완료 매치는 버튼 비활성.
            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                rowButton.interactable = completed;
                if (completed)
                {
                    int matchId = match.id;
                    rowButton.onClick.AddListener(() => OpenResult(matchId));
                }
            }
        }

        private static void OpenResult(int matchId)
        {
            PlayerPrefs.SetInt(DashboardController.SelectedMatchIdKey, matchId);
            SceneManager.LoadScene(MatchResultDashboard);
        }
    }
}
