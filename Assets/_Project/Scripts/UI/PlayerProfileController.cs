// Task 13.6 (Issue #51) — 선수 프로필 화면.
// PlayerPrefs("SelectedPlayerId")로 선수 ID 수신.
// 능력치는 5단계 티어로 표시 (design-decisions #14).
// GameBalanceSO.isDebugMode 활성 시 정확한 수치 추가 노출 (Task 14.2 연동).

using System.Text;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.UI
{
    public class PlayerProfileController : MonoBehaviour
    {
        private const string SquadScene = "SquadScene";

        // 개별 스탯 티어 컷오프 (스탯 스케일 1-20)
        private const int StatElite = 17;
        private const int StatStrong = 13;
        private const int StatAverage = 9;
        private const int StatWeak = 5;

        [Header("헤더")]
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text positionAgeText;

        [SerializeField]
        private TMP_Text nationalityText;

        [SerializeField]
        private TMP_Text footText;

        [Header("능력치")]
        [SerializeField]
        private TMP_Text technicalText;

        [SerializeField]
        private TMP_Text mentalText;

        [SerializeField]
        private TMP_Text physicalText;

        [SerializeField]
        private TMP_Text gkText;

        [Header("트레잇 / 계약 / 상태")]
        [SerializeField]
        private TMP_Text traitsText;

        [SerializeField]
        private TMP_Text contractText;

        [SerializeField]
        private TMP_Text stateText;

        [Header("커리어")]
        [SerializeField]
        private TMP_Text careerText;

        private void Start()
        {
            var state = GameManager.Instance?.State;
            if (state == null)
                return;

            int playerId = PlayerPrefs.GetInt(SquadController.SelectedPlayerIdKey, -1);
            var player = state.GetPlayer(playerId);
            if (player == null)
            {
                if (nameText != null)
                    nameText.text = Localization.Get("player_not_found_fmt", playerId);
                return;
            }

            bool debugMode = GameDatabase.GameBalance != null && GameDatabase.GameBalance.isDebugMode;
            int age =
                player.info != null
                    ? (int)((state.currentDate - player.info.birthDate).TotalDays / 365.25)
                    : 0;

            if (nameText != null)
                nameText.text =
                    player.info != null
                        ? $"{player.info.firstName} {player.info.lastName}"
                        : $"id={player.id}";

            if (positionAgeText != null)
                positionAgeText.text = Localization.Get(
                    "player_position_age_fmt",
                    player.info?.primaryPosition,
                    age
                );

            if (nationalityText != null)
                nationalityText.text = player.info?.nationalityCode ?? "-";

            if (footText != null)
                footText.text = player.info?.preferredFoot.ToString() ?? "-";

            if (technicalText != null)
                technicalText.text = BuildTechnicalText(player, debugMode);

            if (mentalText != null)
                mentalText.text = BuildMentalText(player, debugMode);

            if (physicalText != null)
                physicalText.text = BuildPhysicalText(player, debugMode);

            if (gkText != null)
                gkText.text = BuildGkText(player, debugMode);

            if (traitsText != null)
                traitsText.text = BuildTraitsText(player);

            if (contractText != null)
                contractText.text = BuildContractText(player, debugMode);

            if (stateText != null)
                stateText.text = BuildStateText(player, state);

            if (careerText != null)
                careerText.text = BuildCareerText(player, state);
        }

        public void OnBackClicked()
        {
            Debug.Log("[PlayerProfileController] Back button clicked. Loading SquadScene...");
            SceneManager.LoadScene(SquadScene);
        }

        // ── 능력치 ──────────────────────────────────────────────────────────

        private static string StatTier(int value)
        {
            if (value >= StatElite)
                return "Elite";
            if (value >= StatStrong)
                return "Strong";
            if (value >= StatAverage)
                return "Average";
            if (value >= StatWeak)
                return "Weak";
            return "Poor";
        }

        private static string StatLine(string label, int value, bool debug) =>
            debug ? $"{label}: {StatTier(value)} ({value})" : $"{label}: {StatTier(value)}";

        private string BuildTechnicalText(Player p, bool debug)
        {
            if (p.stats == null)
                return Localization.Get("no_stats_tech");
            var t = p.stats.technical;
            var sb = new StringBuilder(Localization.Get("section_tech") + "\n");
            sb.AppendLine(StatLine(Localization.Get("stat_passing"), t.passing, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_shooting"), t.shooting, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_tackling"), t.tackling, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_dribbling"), t.dribbling, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_heading"), t.heading, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_crossing"), t.crossing, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_first_touch"), t.firstTouch, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_finishing"), t.finishing, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_long_shots"), t.longShots, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_free_kick"), t.freeKickAccuracy, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_penalty"), t.penaltyTaking, debug));
            sb.Append(StatLine(Localization.Get("stat_corners"), t.corners, debug));
            return sb.ToString();
        }

        private string BuildMentalText(Player p, bool debug)
        {
            if (p.stats == null)
                return Localization.Get("no_stats_mental");
            var m = p.stats.mental;
            var sb = new StringBuilder(Localization.Get("section_mental") + "\n");
            sb.AppendLine(StatLine(Localization.Get("stat_vision"), m.vision, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_anticipation"), m.anticipation, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_composure"), m.composure, debug));
            sb.AppendLine(
                StatLine(Localization.Get("stat_concentration"), m.concentration, debug)
            );
            sb.AppendLine(StatLine(Localization.Get("stat_decisions"), m.decisions, debug));
            sb.AppendLine(
                StatLine(Localization.Get("stat_determination"), m.determination, debug)
            );
            sb.AppendLine(StatLine(Localization.Get("stat_leadership"), m.leadership, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_off_the_ball"), m.offTheBall, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_positioning"), m.positioning, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_teamwork"), m.teamwork, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_work_rate"), m.workRate, debug));
            sb.Append(StatLine(Localization.Get("stat_aggression"), m.aggression, debug));
            return sb.ToString();
        }

        private string BuildPhysicalText(Player p, bool debug)
        {
            if (p.stats == null)
                return Localization.Get("no_stats_physical");
            var ph = p.stats.physical;
            var sb = new StringBuilder(Localization.Get("section_physical") + "\n");
            sb.AppendLine(
                StatLine(Localization.Get("stat_acceleration"), ph.acceleration, debug)
            );
            sb.AppendLine(StatLine(Localization.Get("stat_agility"), ph.agility, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_balance"), ph.balance, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_jumping"), ph.jumping, debug));
            sb.AppendLine(
                StatLine(Localization.Get("stat_natural_fitness"), ph.naturalFitness, debug)
            );
            sb.AppendLine(StatLine(Localization.Get("stat_pace"), ph.pace, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_stamina"), ph.stamina, debug));
            sb.Append(StatLine(Localization.Get("stat_strength"), ph.strength, debug));
            return sb.ToString();
        }

        private string BuildGkText(Player p, bool debug)
        {
            if (p.stats == null || p.info?.primaryPosition != Position.GK)
                return string.Empty;
            var g = p.stats.gk;
            var sb = new StringBuilder(Localization.Get("section_gk") + "\n");
            sb.AppendLine(StatLine(Localization.Get("stat_aerial_reach"), g.aerialReach, debug));
            sb.AppendLine(
                StatLine(Localization.Get("stat_command_of_area"), g.commandOfArea, debug)
            );
            sb.AppendLine(
                StatLine(Localization.Get("stat_communication"), g.communication, debug)
            );
            sb.AppendLine(
                StatLine(Localization.Get("stat_eccentricity"), g.eccentricity, debug)
            );
            sb.AppendLine(StatLine(Localization.Get("stat_handling"), g.handling, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_kicking"), g.kicking, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_one_on_ones"), g.oneOnOnes, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_reflexes"), g.reflexes, debug));
            sb.AppendLine(StatLine(Localization.Get("stat_rushing_out"), g.rushingOut, debug));
            sb.Append(StatLine(Localization.Get("stat_throwing"), g.throwing, debug));
            return sb.ToString();
        }

        // ── 트레잇 / 계약 / 상태 / 커리어 ─────────────────────────────────

        private static string BuildTraitsText(Player p)
        {
            if (p.traitIds == null || p.traitIds.Count == 0)
                return Localization.Get("section_traits") + "\n" + Localization.Get("no_traits");
            var sb = new StringBuilder(Localization.Get("section_traits") + "\n");
            foreach (var id in p.traitIds)
            {
                var trait = GameDatabase.GetTrait(id);
                sb.AppendLine(trait != null ? trait.displayName : $"id={id}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildContractText(Player p, bool debug)
        {
            if (p.contract == null)
                return Localization.Get("section_contract") + "\n" + Localization.Get("no_info");
            var c = p.contract;
            var sb = new StringBuilder(Localization.Get("section_contract") + "\n");
            if (debug)
                sb.AppendLine(
                    Localization.Get("contract_wage_fmt", c.weeklyWage.ToString("N0"))
                );
            sb.AppendLine(
                Localization.Get("contract_end_fmt", c.endDate.ToString("yyyy-MM-dd"))
            );
            if (c.releaseClause > 0)
                sb.Append(
                    debug
                        ? Localization.Get(
                            "contract_release_debug_fmt",
                            c.releaseClause.ToString("N0")
                        )
                        : Localization.Get("contract_release")
                );
            return sb.ToString().TrimEnd();
        }

        private static string BuildStateText(Player p, GameState state)
        {
            if (p.state == null)
                return Localization.Get("section_state") + "\n" + Localization.Get("no_info");
            var s = p.state;
            var sb = new StringBuilder(Localization.Get("section_state") + "\n");
            sb.AppendLine(Localization.Get("state_fatigue_fmt", s.fatigue));
            sb.AppendLine(Localization.Get("state_morale_fmt", s.morale));
            sb.AppendLine(Localization.Get("state_form_fmt", s.form));
            sb.AppendLine(Localization.Get("state_appearances_fmt", s.seasonAppearances));
            if (s.injury != null)
                sb.AppendLine(
                    Localization.Get(
                        "state_injury_return_fmt",
                        s.injury.expectedReturn.ToString("MM-dd")
                    )
                );
            else
                sb.AppendLine(Localization.Get("state_no_injury"));
            sb.Append(s.transferListed ? Localization.Get("state_transfer_listed") : "");
            return sb.ToString().TrimEnd();
        }

        private static string BuildCareerText(Player p, GameState state)
        {
            if (p.career == null || p.career.Count == 0)
                return Localization.Get("section_career") + "\n" + Localization.Get("no_career");
            var sb = new StringBuilder(Localization.Get("section_career") + "\n");
            foreach (var s in p.career)
            {
                var club = state.GetClub(s.clubId);
                string clubName = club?.name ?? $"id={s.clubId}";
                sb.AppendLine(
                    Localization.Get(
                        "career_entry_fmt",
                        s.seasonYear,
                        s.seasonYear + 1,
                        clubName,
                        s.appearances,
                        s.goals,
                        s.assists
                    )
                );
            }
            return sb.ToString().TrimEnd();
        }
    }
}
