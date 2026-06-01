// SeasonSummaryController.cs
// V0.5 M.9 — 시즌 종료 요약 화면.
// 5/15 SeasonEndedEvent 정지 후 DashboardController 가 이 씬으로 전환.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class SeasonSummaryController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [Header("순위 섹션")]
        [SerializeField]
        private TMP_Text positionText;

        [Header("수상 섹션")]
        [SerializeField]
        private TMP_Text awardsText;

        [Header("재정 섹션")]
        [SerializeField]
        private TMP_Text financeText;

        [Header("보드 섹션")]
        [SerializeField]
        private TMP_Text boardText;

        [Header("다음 시즌 버튼")]
        [SerializeField]
        private Button nextSeasonButton;

        private void Start()
        {
            Refresh();
            if (nextSeasonButton != null)
                nextSeasonButton.GetComponentInChildren<TMP_Text>().text = Localization.Get(
                    "season_summary_next_season_btn"
                );
        }

        private void Refresh()
        {
            var state = GameManager.Instance?.State;
            if (state == null)
                return;

            var userClub = state.GetClub(state.userClubId);
            var league = state.leagues.FirstOrDefault(l =>
                l?.clubIds != null && l.clubIds.Contains(state.userClubId)
            );

            // 헤더
            int seasonYear = league?.seasonYear ?? 0;
            if (titleText != null)
                titleText.text = Localization.Get("season_summary_title", seasonYear);

            // 순위
            if (positionText != null)
                positionText.text = BuildPositionText(state, userClub, league);

            // 수상
            if (awardsText != null)
                awardsText.text = BuildAwardsText(state, seasonYear);

            // 재정
            if (financeText != null)
                financeText.text = BuildFinanceText(userClub);

            // 보드
            if (boardText != null)
                boardText.text = BuildBoardText(state, userClub);
        }

        private static string BuildPositionText(GameState state, Club club, League league)
        {
            if (club == null || league?.standings?.entries == null)
                return "";

            var sorted = league.standings.entries.OrderByDescending(e => e.points).ToList();
            int actual = sorted.FindIndex(e => e.clubId == state.userClubId) + 1;
            int target = club.season?.targetLeaguePosition ?? 0;

            return Localization.Get("season_summary_position_fmt", actual, target);
        }

        private static string BuildAwardsText(GameState state, int seasonYear)
        {
            var userClub = state.GetClub(state.userClubId);
            var playerIds = userClub?.seniorSquadIds ?? new List<int>();

            var seasonAwards = state
                .activeAwards.Where(a =>
                    a.seasonYear == seasonYear
                    && a.type != AwardType.MonthlyManagerOfMonth
                    && a.type != AwardType.MonthlyPlayerOfMonth
                )
                .ToList();

            var lines = new List<string>();

            foreach (var award in seasonAwards)
            {
                string typeKey = AwardTypeKey(award.type);
                string typeName = Localization.Get(typeKey);

                if (award.type == AwardType.ManagerOfSeason)
                {
                    // playerIds 비어있음 — 매니저 수상
                    lines.Add(Localization.Get("season_summary_award_manager_fmt", typeName));
                }
                else if (award.type == AwardType.BestEleven)
                {
                    // 유저 클럽 소속 선수 수
                    int userCount = award.playerIds.Count(pid => playerIds.Contains(pid));
                    if (userCount > 0)
                        lines.Add(
                            Localization.Get("season_summary_award_best11_fmt", typeName, userCount)
                        );
                }
                else
                {
                    int pid = award.playerIds.FirstOrDefault();
                    if (!playerIds.Contains(pid))
                        continue;
                    var player = state.GetPlayer(pid);
                    string playerName =
                        player?.info != null
                            ? $"{player.info.firstName} {player.info.lastName}"
                            : $"id={pid}";
                    lines.Add(Localization.Get("season_summary_award_fmt", typeName, playerName));
                }
            }

            return lines.Count > 0
                ? string.Join("\n", lines)
                : Localization.Get("season_summary_no_awards");
        }

        private static string BuildFinanceText(Club club)
        {
            if (club?.finance == null)
                return "";

            string money = FormatMoney(club.finance.money);
            string transfer = FormatMoney(club.finance.transferBudget);
            string wage = FormatMoney(club.finance.wageBudget);
            return Localization.Get("season_summary_finance_fmt", money, transfer, wage);
        }

        private static string BuildBoardText(GameState state, Club club)
        {
            int confidence = club?.season?.boardConfidence ?? 0;
            int rep = state.managerReputation;
            return Localization.Get("season_summary_board_fmt", confidence, rep);
        }

        private static string FormatMoney(int amount) =>
            FMLite.Utils.CurrencyFormatter.Format(
                amount,
                FMLite.Application.OptionsManager.Currency
            );

        private static string AwardTypeKey(AwardType type) =>
            type switch
            {
                AwardType.LeagueMVP => "award_type_mvp",
                AwardType.TopScorer => "award_type_top_scorer",
                AwardType.TopAssist => "award_type_top_assist",
                AwardType.YoungPlayer => "award_type_young_player",
                AwardType.BestEleven => "award_type_best_eleven",
                AwardType.GoldenGlove => "award_type_golden_glove",
                AwardType.ManagerOfSeason => "award_type_manager_of_season",
                _ => "award_type_mvp",
            };

        public void OnNextSeasonClicked()
        {
            SceneManager.LoadScene(DashboardScene);
        }
    }
}
