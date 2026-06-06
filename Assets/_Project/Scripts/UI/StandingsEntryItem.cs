// 리그 순위표 행 프리팹 컨트롤러.
// V1.0 M.1 (#528): 자기 구단 강조 + 순위 변동 표시(컨트롤러 인메모리 직전순위 diff).
// 변동 표시는 NotoSansKR SDF 에 Geometric-Shapes(▲▼) 글리프가 없어 글리프-세이프한
// "+N"(상승=녹)/"-N"(하락=빨강) 텍스트로 렌더 (TopBar ◀ □ 깨짐 선례 회피).

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
        private TMP_Text deltaText; // 순위 변동 (+N 상승 / -N 하락 / 무변동·신규 = 빈칸)

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Color userClubColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);

        [SerializeField]
        private Color defaultColor = new Color(0f, 0f, 0f, 0f);

        private static readonly Color UpColor = new Color(0.302f, 0.686f, 0.314f); // #4CAF50
        private static readonly Color DownColor = new Color(0.906f, 0.298f, 0.235f); // #E74C3C

        // rankDelta = 직전순위 - 현재순위 (양수 = 상승). hasDelta=false 면 변동 미표시(첫 진입/신규).
        public void Setup(
            int rank,
            StandingEntry entry,
            GameState state,
            int userClubId,
            int rankDelta,
            bool hasDelta
        )
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

            if (deltaText != null)
            {
                if (!hasDelta || rankDelta == 0)
                {
                    deltaText.text = string.Empty;
                }
                else if (rankDelta > 0)
                {
                    deltaText.text = "+" + rankDelta;
                    deltaText.color = UpColor;
                }
                else
                {
                    deltaText.text = rankDelta.ToString(); // 이미 '-' 포함
                    deltaText.color = DownColor;
                }
            }

            if (backgroundImage != null)
                backgroundImage.color = entry.clubId == userClubId ? userClubColor : defaultColor;
        }
    }
}
