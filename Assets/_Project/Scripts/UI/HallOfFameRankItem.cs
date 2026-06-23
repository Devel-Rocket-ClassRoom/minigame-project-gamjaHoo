// HallOfFameRankItem.cs
// 명예의 전당 글로벌 리더보드 한 행 (HallOfFameScene 전용 프리팹 컨트롤러).
// 클라우드 LeaderboardEntry (Persistence.Cloud) 1행 — 순위 / 닉네임 / 구단 / 점수.
// 내 uid 와 일치하면 강조 배경. (리그 개인 순위표용 LeaderboardEntryItem 과 별개.)

using FMLite.Application;
using FMLite.Persistence.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LeaderboardEntry = FMLite.Persistence.Cloud.LeaderboardEntry;

namespace FMLite.UI
{
    public class HallOfFameRankItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text rankText;

        [SerializeField]
        private TMP_Text nicknameText;

        [SerializeField]
        private TMP_Text clubText;

        [SerializeField]
        private TMP_Text scoreText;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Color myEntryColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);

        [SerializeField]
        private Color defaultColor = new Color(0f, 0f, 0f, 0f);

        public void Setup(int rank, LeaderboardEntry entry, string myUid)
        {
            if (rankText != null)
                rankText.text = rank.ToString();
            if (nicknameText != null)
                nicknameText.text = string.IsNullOrEmpty(entry.nickname)
                    ? Localization.Get("hof_anonymous")
                    : entry.nickname;
            if (clubText != null)
                clubText.text = entry.clubName ?? string.Empty;
            if (scoreText != null)
                scoreText.text = entry.score.ToString("N0");

            bool isMine = !string.IsNullOrEmpty(myUid) && entry.uid == myUid;
            if (backgroundImage != null)
                backgroundImage.color = isMine ? myEntryColor : defaultColor;
        }
    }
}
