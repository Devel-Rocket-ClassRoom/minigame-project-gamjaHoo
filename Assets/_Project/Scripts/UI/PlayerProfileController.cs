// Task 13.6 (Issue #51) — 선수 프로필 화면.
// PlayerPrefs("SelectedPlayerId")로 선수 ID 수신.
// 능력치는 5단계 티어로 표시 (design-decisions #14).
// GameBalanceSO.isDebugMode 활성 시 정확한 수치 추가 노출 (Task 14.2 연동).
// V1.0 G.2 Sub-B (#300): [면담] 버튼 + InterviewDialogController 연동 (own-club 선수만 활성화).

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
    public class PlayerProfileController : MonoBehaviour
    {
        private const string SquadScene = "SquadScene";

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

        [Header("면담 (V1.0 G.2 Sub-B)")]
        [SerializeField]
        private Button interviewButton;

        [SerializeField]
        private InterviewDialogController interviewDialog;

        [Header("1군 승격 (V1.0 L.5)")]
        [SerializeField]
        private Button promoteButton;

        [SerializeField]
        private Button declinePromotionButton;

        private int _currentPlayerId = -1;

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
            _currentPlayerId = playerId;
            ConfigureInterviewButton(player, state);
            ConfigurePromotionButtons(player, state);

            bool debugMode = GameDatabase.GameBalance != null && GameDatabase.GameBalance.isDebugMode;
            // stats는 항상 정확 수치 노출 (B.5); debugMode는 계약 재무 정보에만 사용
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
                technicalText.text = BuildTechnicalText(player);

            if (mentalText != null)
                mentalText.text = BuildMentalText(player);

            if (physicalText != null)
                physicalText.text = BuildPhysicalText(player);

            if (gkText != null)
                gkText.text = BuildGkText(player);

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
            string prev = PlayerPrefs.GetString(PlayerNameLinkController.PreviousSceneKey, SquadScene);
            SceneManager.LoadScene(prev);
        }

        // ── 면담 (V1.0 G.2 Sub-B) ──────────────────────────────────────

        public void OnInterviewClicked()
        {
            if (interviewDialog == null || _currentPlayerId == -1)
                return;
            interviewDialog.Show(_currentPlayerId);
        }

        private void ConfigureInterviewButton(Player player, GameState state)
        {
            if (interviewButton == null)
                return;
            // 자기 구단 선수에게만 면담 허용 (타 구단 / 무소속 X).
            bool isOwnClub = player.currentClubId == state.userClubId;
            interviewButton.gameObject.SetActive(isOwnClub);
            interviewButton.onClick.RemoveAllListeners();
            interviewButton.onClick.AddListener(OnInterviewClicked);
        }

        // ── 1군 승격 (V1.0 L.5) ────────────────────────────────────────────

        public void OnPromoteClicked()
        {
            var state = GameManager.Instance?.State;
            if (state == null || _currentPlayerId == -1)
                return;
            YouthSystem.PromotePlayer(_currentPlayerId, state);
            promoteButton?.gameObject.SetActive(false);
            declinePromotionButton?.gameObject.SetActive(false);
        }

        public void OnDeclinePromotionClicked()
        {
            var state = GameManager.Instance?.State;
            if (state == null || _currentPlayerId == -1)
                return;
            YouthSystem.DeclinePromotion(_currentPlayerId, state);
            promoteButton?.gameObject.SetActive(false);
            declinePromotionButton?.gameObject.SetActive(false);
        }

        private void ConfigurePromotionButtons(Player player, GameState state)
        {
            bool showPromotion = false;
            if (player.currentClubId == state.userClubId)
            {
                var club = state.GetClub(state.userClubId);
                showPromotion = club != null
                    && club.youthSquadIds.Contains(player.id)
                    && club.season.pendingPromotionPlayerIds.Contains(player.id);
            }
            if (promoteButton != null)
            {
                promoteButton.gameObject.SetActive(showPromotion);
                promoteButton.onClick.RemoveAllListeners();
                promoteButton.onClick.AddListener(OnPromoteClicked);
            }
            if (declinePromotionButton != null)
            {
                declinePromotionButton.gameObject.SetActive(showPromotion);
                declinePromotionButton.onClick.RemoveAllListeners();
                declinePromotionButton.onClick.AddListener(OnDeclinePromotionClicked);
            }
        }

        // ── 능력치 ──────────────────────────────────────────────────────────

        private static string StatLine(string label, int value) => $"{label}: {value}";

        private string BuildTechnicalText(Player p)
        {
            if (p.stats == null)
                return Localization.Get("no_stats_tech");
            var t = p.stats.technical;
            var sb = new StringBuilder(Localization.Get("section_tech") + "\n");
            sb.AppendLine(StatLine(Localization.Get("stat_passing"), t.passing));
            sb.AppendLine(StatLine(Localization.Get("stat_tackling"), t.tackling));
            sb.AppendLine(StatLine(Localization.Get("stat_dribbling"), t.dribbling));
            sb.AppendLine(StatLine(Localization.Get("stat_heading"), t.heading));
            sb.AppendLine(StatLine(Localization.Get("stat_crossing"), t.crossing));
            sb.AppendLine(StatLine(Localization.Get("stat_first_touch"), t.firstTouch));
            sb.AppendLine(StatLine(Localization.Get("stat_finishing"), t.finishing));
            sb.AppendLine(StatLine(Localization.Get("stat_long_shots"), t.longShots));
            sb.AppendLine(StatLine(Localization.Get("stat_free_kick"), t.freeKickTaking));
            sb.AppendLine(StatLine(Localization.Get("stat_penalty"), t.penaltyTaking));
            sb.AppendLine(StatLine(Localization.Get("stat_corners"), t.corners));
            sb.AppendLine(StatLine(Localization.Get("stat_marking"), t.marking));
            sb.AppendLine(StatLine(Localization.Get("stat_technique"), t.technique));
            sb.Append(StatLine(Localization.Get("stat_long_throws"), t.longThrows));
            return sb.ToString();
        }

        private string BuildMentalText(Player p)
        {
            if (p.stats == null)
                return Localization.Get("no_stats_mental");
            var m = p.stats.mental;
            var sb = new StringBuilder(Localization.Get("section_mental") + "\n");
            sb.AppendLine(StatLine(Localization.Get("stat_vision"), m.vision));
            sb.AppendLine(StatLine(Localization.Get("stat_anticipation"), m.anticipation));
            sb.AppendLine(StatLine(Localization.Get("stat_composure"), m.composure));
            sb.AppendLine(StatLine(Localization.Get("stat_concentration"), m.concentration));
            sb.AppendLine(StatLine(Localization.Get("stat_decisions"), m.decisions));
            sb.AppendLine(StatLine(Localization.Get("stat_determination"), m.determination));
            sb.AppendLine(StatLine(Localization.Get("stat_leadership"), m.leadership));
            sb.AppendLine(StatLine(Localization.Get("stat_off_the_ball"), m.offTheBall));
            sb.AppendLine(StatLine(Localization.Get("stat_positioning"), m.positioning));
            sb.AppendLine(StatLine(Localization.Get("stat_teamwork"), m.teamwork));
            sb.AppendLine(StatLine(Localization.Get("stat_work_rate"), m.workRate));
            sb.AppendLine(StatLine(Localization.Get("stat_aggression"), m.aggression));
            sb.AppendLine(StatLine(Localization.Get("stat_bravery"), m.bravery));
            sb.Append(StatLine(Localization.Get("stat_flair"), m.flair));
            return sb.ToString();
        }

        private string BuildPhysicalText(Player p)
        {
            if (p.stats == null)
                return Localization.Get("no_stats_physical");
            var ph = p.stats.physical;
            var sb = new StringBuilder(Localization.Get("section_physical") + "\n");
            sb.AppendLine(StatLine(Localization.Get("stat_acceleration"), ph.acceleration));
            sb.AppendLine(StatLine(Localization.Get("stat_agility"), ph.agility));
            sb.AppendLine(StatLine(Localization.Get("stat_balance"), ph.balance));
            sb.AppendLine(StatLine(Localization.Get("stat_jumping"), ph.jumpingReach));
            sb.AppendLine(StatLine(Localization.Get("stat_natural_fitness"), ph.naturalFitness));
            sb.AppendLine(StatLine(Localization.Get("stat_pace"), ph.pace));
            sb.AppendLine(StatLine(Localization.Get("stat_stamina"), ph.stamina));
            sb.Append(StatLine(Localization.Get("stat_strength"), ph.strength));
            return sb.ToString();
        }

        private string BuildGkText(Player p)
        {
            if (p.stats == null || p.info?.primaryPosition != Position.GK)
                return string.Empty;
            var g = p.stats.gk;
            var sb = new StringBuilder(Localization.Get("section_gk") + "\n");
            sb.AppendLine(StatLine(Localization.Get("stat_aerial_reach"), g.aerialReach));
            sb.AppendLine(StatLine(Localization.Get("stat_command_of_area"), g.commandOfArea));
            sb.AppendLine(StatLine(Localization.Get("stat_communication"), g.communication));
            sb.AppendLine(StatLine(Localization.Get("stat_eccentricity"), g.eccentricity));
            sb.AppendLine(StatLine(Localization.Get("stat_handling"), g.handling));
            sb.AppendLine(StatLine(Localization.Get("stat_kicking"), g.kicking));
            sb.AppendLine(StatLine(Localization.Get("stat_one_on_ones"), g.oneOnOnes));
            sb.AppendLine(StatLine(Localization.Get("stat_reflexes"), g.reflexes));
            sb.AppendLine(StatLine(Localization.Get("stat_rushing_out"), g.rushingOut));
            sb.AppendLine(StatLine(Localization.Get("stat_throwing"), g.throwing));
            sb.AppendLine(StatLine(Localization.Get("stat_first_touch_gk"), g.firstTouchGk));
            sb.AppendLine(StatLine(Localization.Get("stat_passing_gk"), g.passingGk));
            sb.Append(StatLine(Localization.Get("stat_punching_tendency"), g.punchingTendency));
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
