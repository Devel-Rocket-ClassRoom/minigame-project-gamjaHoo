// Task 13.6 (Issue #51) — 선수 프로필 화면.
// PlayerPrefs("SelectedPlayerId")로 선수 ID 수신.
// V0.5 G.2 Sub-B (#300): [면담] 버튼 + InterviewDialogController 연동 (own-club 선수만 활성화).
// Stage C (V1.0, #455): 49-stat 4 카테고리 2×2 그리드 (StatRowView 행 위젯) + 색상 코딩 (C.2)
//   + 신체 조건 (C.3: height/weight/preferredFoot/weakFootAbility 별점) + 성장 동향 화살표 (C.4).
//   기존 카테고리별 TMP_Text 블록 → 카테고리 GridLayout 컨테이너 + StatRowView 인스턴스화로 교체.
// GameBalanceSO.isDebugMode 활성 시 정확한 계약 재무 정보 추가 노출 (Task 14.2 연동).

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

        [Header("능력치 그리드 (C.1 — StatRowView prefab 인스턴스화)")]
        [Tooltip("R.6 — 4 stat 그리드 공통 부모. 타팀 미정찰 선수는 비활성.")]
        [SerializeField]
        private GameObject statsRoot;

        [SerializeField]
        private GameObject statRowPrefab;

        [SerializeField]
        private Transform technicalGrid;

        [SerializeField]
        private Transform mentalGrid;

        [SerializeField]
        private Transform physicalGrid;

        [SerializeField]
        private Transform gkGrid;

        [Tooltip("GK 가 아닌 선수는 비활성화 (GK stat 무관). 미지정 시 gkGrid 부모 토글 생략.")]
        [SerializeField]
        private GameObject gkPanel;

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

        [Header("R.6 — 스카우팅 게이팅")]
        [Tooltip("타팀 미정찰 선수에게 '스카우팅되지 않음' 안내. reveal=false 시 활성.")]
        [SerializeField]
        private GameObject notScoutedNotice;

        [SerializeField]
        private TMP_Text notScoutedText;

        [Header("네비 (MUIP 버튼)")]
        [SerializeField]
        private Button backButton;

        [Header("면담 (V0.5 G.2 Sub-B)")]
        [SerializeField]
        private Button interviewButton;

        [SerializeField]
        private InterviewDialogController interviewDialog;

        [Header("재계약 (R.7 #77-2)")]
        [SerializeField]
        private Button renewButton;

        [SerializeField]
        private ContractRenewalDialogController renewDialog;

        [Header("콜업 (R.7 #77-2)")]
        [Tooltip("R.7 — 자기팀 유스 선수면 항상 노출 (콜업).")]
        [SerializeField]
        private Button promoteButton;

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
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }
            bool debugMode = GameDatabase.GameBalance != null && GameDatabase.GameBalance.isDebugMode;
            // R.6 (#77-1): 자기팀/정찰 선수만 상세(49 stat / 트레잇 / 내부 상태) 노출.
            // 타팀 미정찰은 게이팅. debugMode 는 계약 재무 정보에도 사용.
            var userClub = state.GetClub(state.userClubId);
            bool reveal = ScoutingVisibility.CanRevealDetails(userClub, player, debugMode);

            ConfigureInterviewButton(player, state);
            ConfigurePromotionButtons(player, state);
            ConfigureRenewButton(player, state);

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
            {
                // 포지션 · 나이 · 국적 한 줄 (이름 밑 부제 — FM식). 국적 단독 텍스트 폐기.
                string nat = player.info?.nationalityCode;
                string posAge = Localization.Get(
                    "player_position_age_fmt",
                    player.info?.primaryPosition,
                    age
                );
                positionAgeText.text = string.IsNullOrEmpty(nat) ? posAge : $"{posAge} · {nat}";
            }

            // 공개 정보 (스카우팅 무관 항상 표시): 계약 만료 / 커리어.
            if (contractText != null)
                contractText.text = BuildContractText(player, debugMode);

            if (careerText != null)
                careerText.text = BuildCareerText(player, state);

            // R.6 게이팅 — stat 그리드 / 트레잇 / 내부 상태는 reveal 일 때만.
            if (statsRoot != null)
                statsRoot.SetActive(reveal);
            if (notScoutedNotice != null)
                notScoutedNotice.SetActive(!reveal);

            if (reveal)
            {
                PopulateStatGrids(player);
                if (traitsText != null)
                    traitsText.text = BuildTraitsText(player);
                if (stateText != null)
                    stateText.text = BuildStateText(player, state);
            }
            else
            {
                if (notScoutedText != null)
                    notScoutedText.text = Localization.Get("profile_not_scouted");
                if (traitsText != null)
                    traitsText.text = string.Empty;
                if (stateText != null)
                    stateText.text = string.Empty;
            }
        }

        // ── 재계약 (R.7 #77-2) ─────────────────────────────────────────

        public void OnRenewClicked()
        {
            if (renewDialog == null || _currentPlayerId == -1)
                return;
            renewDialog.Show(_currentPlayerId);
        }

        private void ConfigureRenewButton(Player player, GameState state)
        {
            if (renewButton == null)
                return;
            // 자기 구단 선수만 재계약 (senior / youth 무관).
            bool isOwnClub = player.currentClubId == state.userClubId;
            renewButton.gameObject.SetActive(isOwnClub);
            renewButton.onClick.RemoveAllListeners();
            renewButton.onClick.AddListener(OnRenewClicked);
        }

        public void OnBackClicked()
        {
            string prev = PlayerPrefs.GetString(PlayerNameLinkController.PreviousSceneKey, SquadScene);
            SceneManager.LoadScene(prev);
        }

        // ── 면담 (V0.5 G.2 Sub-B) ──────────────────────────────────────

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

        // ── 콜업 / 1군 승격 (V0.5 L.5 + R.7 #77-2) ──────────────────────────

        public void OnPromoteClicked()
        {
            var state = GameManager.Instance?.State;
            if (state == null || _currentPlayerId == -1)
                return;
            YouthSystem.PromotePlayer(_currentPlayerId, state);
            promoteButton?.gameObject.SetActive(false);
        }

        private void ConfigurePromotionButtons(Player player, GameState state)
        {
            // R.7 (#77-2): [콜업] = 자기팀 유스 선수면 항상 노출 (pending 제안 한정 아님).
            bool isOwnYouth = false;
            if (player.currentClubId == state.userClubId)
            {
                var club = state.GetClub(state.userClubId);
                isOwnYouth = club != null && club.youthSquadIds.Contains(player.id);
            }
            if (promoteButton != null)
            {
                promoteButton.gameObject.SetActive(isOwnYouth);
                promoteButton.onClick.RemoveAllListeners();
                promoteButton.onClick.AddListener(OnPromoteClicked);
            }
        }

        // ── 신체 조건 (C.3) — FM식: 신체 컬럼 하단 정보 행 ──────────────────

        private void AppendPhysicalBio(Player p)
        {
            var phys = p.physical;
            if (phys == null)
                return;

            Foot foot = phys.preferredFoot;
            AddTextRow(physicalGrid, "label_height", $"{phys.height}cm");
            AddTextRow(physicalGrid, "label_weight", $"{phys.weight}kg");
            AddTextRow(physicalGrid, "label_preferred_foot", Localization.Get(FootKey(foot)));
            AddTextRow(
                physicalGrid,
                "label_weak_foot",
                StarRating(Mathf.Clamp(phys.weakFootAbility, 0, 5))
            );
        }

        private static string FootKey(Foot foot) =>
            foot switch
            {
                Foot.Left => "foot_left",
                Foot.Both => "foot_both",
                _ => "foot_right",
            };

        // 별점 (1-5) — 채워진 ★ + 빈 ☆. CJK 폰트 포함 글리프 (Sub-C 시각 검증).
        private static string StarRating(int filled)
        {
            var sb = new StringBuilder(5);
            for (int i = 0; i < 5; i++)
                sb.Append(i < filled ? '★' : '☆');
            return sb.ToString();
        }

        // ── 능력치 그리드 (C.1 + C.2 색상 + C.4 성장) ─────────────────────

        private void PopulateStatGrids(Player p)
        {
            if (p.stats == null)
                return;

            var t = p.stats.technical;
            ClearGrid(technicalGrid);
            AddRow(technicalGrid, p, "stat_passing", t.passing, "technical.passing");
            AddRow(technicalGrid, p, "stat_tackling", t.tackling, "technical.tackling");
            AddRow(technicalGrid, p, "stat_dribbling", t.dribbling, "technical.dribbling");
            AddRow(technicalGrid, p, "stat_heading", t.heading, "technical.heading");
            AddRow(technicalGrid, p, "stat_crossing", t.crossing, "technical.crossing");
            AddRow(technicalGrid, p, "stat_first_touch", t.firstTouch, "technical.firstTouch");
            AddRow(technicalGrid, p, "stat_finishing", t.finishing, "technical.finishing");
            AddRow(technicalGrid, p, "stat_long_shots", t.longShots, "technical.longShots");
            AddRow(technicalGrid, p, "stat_free_kick", t.freeKickTaking, "technical.freeKickTaking");
            AddRow(technicalGrid, p, "stat_penalty", t.penaltyTaking, "technical.penaltyTaking");
            AddRow(technicalGrid, p, "stat_corners", t.corners, "technical.corners");
            AddRow(technicalGrid, p, "stat_marking", t.marking, "technical.marking");
            AddRow(technicalGrid, p, "stat_technique", t.technique, "technical.technique");
            AddRow(technicalGrid, p, "stat_long_throws", t.longThrows, "technical.longThrows");

            var m = p.stats.mental;
            ClearGrid(mentalGrid);
            AddRow(mentalGrid, p, "stat_vision", m.vision, "mental.vision");
            AddRow(mentalGrid, p, "stat_anticipation", m.anticipation, "mental.anticipation");
            AddRow(mentalGrid, p, "stat_composure", m.composure, "mental.composure");
            AddRow(mentalGrid, p, "stat_concentration", m.concentration, "mental.concentration");
            AddRow(mentalGrid, p, "stat_decisions", m.decisions, "mental.decisions");
            AddRow(mentalGrid, p, "stat_determination", m.determination, "mental.determination");
            AddRow(mentalGrid, p, "stat_leadership", m.leadership, "mental.leadership");
            AddRow(mentalGrid, p, "stat_off_the_ball", m.offTheBall, "mental.offTheBall");
            AddRow(mentalGrid, p, "stat_positioning", m.positioning, "mental.positioning");
            AddRow(mentalGrid, p, "stat_teamwork", m.teamwork, "mental.teamwork");
            AddRow(mentalGrid, p, "stat_work_rate", m.workRate, "mental.workRate");
            AddRow(mentalGrid, p, "stat_aggression", m.aggression, "mental.aggression");
            AddRow(mentalGrid, p, "stat_bravery", m.bravery, "mental.bravery");
            AddRow(mentalGrid, p, "stat_flair", m.flair, "mental.flair");

            var ph = p.stats.physical;
            ClearGrid(physicalGrid);
            AddRow(physicalGrid, p, "stat_acceleration", ph.acceleration, "physical.acceleration");
            AddRow(physicalGrid, p, "stat_agility", ph.agility, "physical.agility");
            AddRow(physicalGrid, p, "stat_balance", ph.balance, "physical.balance");
            AddRow(physicalGrid, p, "stat_jumping", ph.jumpingReach, "physical.jumpingReach");
            AddRow(physicalGrid, p, "stat_natural_fitness", ph.naturalFitness, "physical.naturalFitness");
            AddRow(physicalGrid, p, "stat_pace", ph.pace, "physical.pace");
            AddRow(physicalGrid, p, "stat_stamina", ph.stamina, "physical.stamina");
            AddRow(physicalGrid, p, "stat_strength", ph.strength, "physical.strength");
            // 신체 bio (FM식 — 키/몸무게/주발/약발 을 신체 컬럼 하단에).
            AppendPhysicalBio(p);

            // GK stat 은 GK 포지션 선수만 (다른 포지션은 무관 → 패널 숨김).
            bool isGk = p.info?.primaryPosition == Position.GK;
            if (gkPanel != null)
                gkPanel.SetActive(isGk);
            ClearGrid(gkGrid);
            if (isGk)
            {
                var g = p.stats.gk;
                AddRow(gkGrid, p, "stat_aerial_reach", g.aerialReach, "gk.aerialReach");
                AddRow(gkGrid, p, "stat_command_of_area", g.commandOfArea, "gk.commandOfArea");
                AddRow(gkGrid, p, "stat_communication", g.communication, "gk.communication");
                AddRow(gkGrid, p, "stat_eccentricity", g.eccentricity, "gk.eccentricity");
                AddRow(gkGrid, p, "stat_handling", g.handling, "gk.handling");
                AddRow(gkGrid, p, "stat_kicking", g.kicking, "gk.kicking");
                AddRow(gkGrid, p, "stat_one_on_ones", g.oneOnOnes, "gk.oneOnOnes");
                AddRow(gkGrid, p, "stat_reflexes", g.reflexes, "gk.reflexes");
                AddRow(gkGrid, p, "stat_rushing_out", g.rushingOut, "gk.rushingOut");
                AddRow(gkGrid, p, "stat_throwing", g.throwing, "gk.throwing");
                AddRow(gkGrid, p, "stat_first_touch_gk", g.firstTouchGk, "gk.firstTouchGk");
                AddRow(gkGrid, p, "stat_passing_gk", g.passingGk, "gk.passingGk");
                AddRow(gkGrid, p, "stat_punching_tendency", g.punchingTendency, "gk.punchingTendency");
            }
        }

        private static void ClearGrid(Transform grid)
        {
            if (grid == null)
                return;
            for (int i = grid.childCount - 1; i >= 0; i--)
                Destroy(grid.GetChild(i).gameObject);
        }

        private void AddRow(Transform grid, Player p, string labelKey, int value, string fieldPath)
        {
            if (grid == null || statRowPrefab == null)
                return;
            var go = Instantiate(statRowPrefab, grid);
            int change = GrowthSystem.GetStatChange(p, fieldPath, 3);
            go.GetComponent<StatRowView>()?.Setup(labelKey, value, change);
        }

        // 문자값 정보 행 (게이지/증감 없음) — 신체 bio 등.
        private void AddTextRow(Transform grid, string labelKey, string value)
        {
            if (grid == null || statRowPrefab == null)
                return;
            var go = Instantiate(statRowPrefab, grid);
            go.GetComponent<StatRowView>()?.SetupText(labelKey, value);
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
                    Localization.Get(
                        "contract_wage_fmt",
                        FMLite.Utils.CurrencyFormatter.Format(
                            c.weeklyWage,
                            FMLite.Application.OptionsManager.Currency
                        )
                    )
                );
            sb.AppendLine(
                Localization.Get("contract_end_fmt", c.endDate.ToString("yyyy-MM-dd"))
            );
            if (c.releaseClause > 0)
                sb.Append(
                    debug
                        ? Localization.Get(
                            "contract_release_debug_fmt",
                            FMLite.Utils.CurrencyFormatter.Format(
                                c.releaseClause,
                                FMLite.Application.OptionsManager.Currency
                            )
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
