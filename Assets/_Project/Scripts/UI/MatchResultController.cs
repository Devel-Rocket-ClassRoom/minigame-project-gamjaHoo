// MatchResultController.cs
// Stage AA — 매치 후 결과 대시보드 (v1.0-plan §3.23 / v1.0-tasks Stage AA).
// PlayerPrefs("SelectedMatchId") 로 매치 ID 수신 (MatchTextController 와 동일 키).
// 6탭: 개요 / 평점 / 통계 / 히트맵 / 슛맵 / 이벤트.
// 히트맵 = MatchResult.zoneOccupancy[5] color overlay (AA.4). 슛맵 = MatchResult.shotMap (AA.5).
// AA.1/AA.2 (도메인 필드 + 시뮬레이터 누적) 는 #474 에서 선당김 완료.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class MatchResultController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        // 진영 색상 (히트맵 / 슛맵 공용) — Home red / Away blue (§3.23.2).
        private static readonly Color HomeColor = new Color(0.878f, 0.400f, 0.400f); // #E06666
        private static readonly Color AwayColor = new Color(0.435f, 0.659f, 0.863f); // #6FA8DC
        private static readonly Color SavedColor = new Color(0.6f, 0.6f, 0.6f); // 회색

        [Header("Header")]
        [SerializeField]
        private TMP_Text matchupText;

        [SerializeField]
        private UnityEngine.UI.Image homeCrest; // Stage AD — 홈 구단 크레스트 (미배선/미생성 시 자동 숨김)

        [SerializeField]
        private UnityEngine.UI.Image awayCrest; // Stage AD — 원정 구단 크레스트

        [SerializeField]
        private TMP_Text scoreText;

        [SerializeField]
        private ButtonManager backButton;

        [Header("Tabs (MUIP ButtonManager)")]
        [SerializeField]
        private ButtonManager tabOverview;

        [SerializeField]
        private ButtonManager tabRatings;

        [SerializeField]
        private ButtonManager tabStats;

        [SerializeField]
        private ButtonManager tabHeatmap;

        [SerializeField]
        private ButtonManager tabShotmap;

        [SerializeField]
        private ButtonManager tabEvents;

        [Header("Tab Panels")]
        [SerializeField]
        private GameObject panelOverview;

        [SerializeField]
        private GameObject panelRatings;

        [SerializeField]
        private GameObject panelStats;

        [SerializeField]
        private GameObject panelHeatmap;

        [SerializeField]
        private GameObject panelShotmap;

        [SerializeField]
        private GameObject panelEvents;

        [Header("Tab Content")]
        [SerializeField]
        private TMP_Text overviewText;

        [SerializeField]
        private TMP_Text ratingsText;

        [SerializeField]
        private TMP_Text statsText;

        [SerializeField]
        private TMP_Text eventsText;

        [Header("Heatmap / Shotmap")]
        [SerializeField]
        private RectTransform heatmapContainer;

        [SerializeField]
        private TMP_Text heatmapLegend;

        [SerializeField]
        private RectTransform shotmapContainer;

        [SerializeField]
        private TMP_Text shotmapLegend;

        private GameState _state;
        private Match _match;
        private MatchResult _result;
        private Club _homeClub;
        private Club _awayClub;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            int matchId = PlayerPrefs.GetInt(DashboardController.SelectedMatchIdKey, -1);
            _match = FindMatch(matchId);
            if (_match?.result == null)
            {
                SceneManager.LoadScene(DashboardScene);
                return;
            }

            _result = _match.result;
            _homeClub = _state.GetClub(_match.homeClubId);
            _awayClub = _state.GetClub(_match.awayClubId);

            WireTab(tabOverview, Localization.Get("mrd_tab_overview"), 0);
            WireTab(tabRatings, Localization.Get("mrd_tab_ratings"), 1);
            WireTab(tabStats, Localization.Get("mrd_tab_stats"), 2);
            WireTab(tabHeatmap, Localization.Get("mrd_tab_heatmap"), 3);
            WireTab(tabShotmap, Localization.Get("mrd_tab_shotmap"), 4);
            WireTab(tabEvents, Localization.Get("mrd_tab_events"), 5);
            if (backButton != null)
            {
                backButton.buttonText = Localization.Get("mrd_back");
                backButton.UpdateUI();
                backButton.clickEvent.AddListener(() => SceneManager.LoadScene(DashboardScene));
            }

            BuildHeader();
            BuildOverview();
            BuildRatings();
            BuildStats();
            BuildHeatmap();
            BuildShotmap();
            BuildEvents();

            ShowTab(0);
        }

        // ── 탭 전환 (TacticLineupController 패턴 — MUIP 하이라이트 잔상 회피) ──
        private void WireTab(ButtonManager tab, string label, int index)
        {
            if (tab == null)
                return;
            tab.buttonText = label;
            tab.UpdateUI();
            tab.clickEvent.AddListener(() => ShowTab(index));
        }

        private void ShowTab(int index)
        {
            if (panelOverview != null)
                panelOverview.SetActive(index == 0);
            if (panelRatings != null)
                panelRatings.SetActive(index == 1);
            if (panelStats != null)
                panelStats.SetActive(index == 2);
            if (panelHeatmap != null)
                panelHeatmap.SetActive(index == 3);
            if (panelShotmap != null)
                panelShotmap.SetActive(index == 4);
            if (panelEvents != null)
                panelEvents.SetActive(index == 5);

            SetTabCurrent(tabOverview, index == 0);
            SetTabCurrent(tabRatings, index == 1);
            SetTabCurrent(tabStats, index == 2);
            SetTabCurrent(tabHeatmap, index == 3);
            SetTabCurrent(tabShotmap, index == 4);
            SetTabCurrent(tabEvents, index == 5);
        }

        // 현재 탭 강조 + MUIP CanvasGroup 잔상 직접 리셋 (TacticLineupController 와 동일 근거).
        private static void SetTabCurrent(ButtonManager tab, bool isCurrent)
        {
            if (tab == null)
                return;
            tab.StopAllCoroutines();
            var normalTr = tab.transform.Find("Normal");
            var highlightedTr = tab.transform.Find("Highlighted");
            if (normalTr != null && normalTr.GetComponent<CanvasGroup>() != null)
                normalTr.GetComponent<CanvasGroup>().alpha = isCurrent ? 0f : 1f;
            if (highlightedTr != null && highlightedTr.GetComponent<CanvasGroup>() != null)
                highlightedTr.GetComponent<CanvasGroup>().alpha = isCurrent ? 1f : 0f;
            var btn = tab.GetComponent<Button>();
            if (btn != null)
                btn.interactable = !isCurrent;
        }

        // ── 헤더 ──────────────────────────────────────────────────────────
        private void BuildHeader()
        {
            string home = _homeClub?.name ?? "?";
            string away = _awayClub?.name ?? "?";
            if (matchupText != null)
                matchupText.text = $"{home}  vs  {away}";
            CrestProvider.ApplyClubCrest(homeCrest, _homeClub?.name);
            CrestProvider.ApplyClubCrest(awayCrest, _awayClub?.name);
            if (scoreText != null)
            {
                string score = $"{_result.homeScore} : {_result.awayScore}";
                if (_result.decidedByPenalties)
                    score += $"  (PK {_result.penaltyHomeScore}:{_result.penaltyAwayScore})";
                scoreText.text = score;
            }
        }

        // ── 개요 탭 ────────────────────────────────────────────────────────
        private void BuildOverview()
        {
            if (overviewText == null)
                return;
            var sb = new StringBuilder();

            sb.AppendLine(
                $"<b>{_homeClub?.name ?? "?"}  {_result.homeScore} - {_result.awayScore}  {_awayClub?.name ?? "?"}</b>"
            );
            sb.AppendLine($"<size=80%>{_match.date:yyyy-MM-dd}</size>");
            sb.AppendLine();

            // 득점 (어시스트 동반)
            sb.AppendLine($"<b>{Localization.Get("mrd_goals")}</b>");
            var scorers = _match
                .events.Where(e =>
                    e.type == MatchEventType.Goal || e.type == MatchEventType.PenaltyGoal
                )
                .ToList();
            if (scorers.Count == 0)
                sb.AppendLine($"<size=85%>- {Localization.Get("mrd_none")}</size>");
            else
                foreach (var ev in scorers)
                {
                    string sideTag = ev.side == 0 ? "H" : "A";
                    string assist =
                        ev.targetPlayerId > 0 ? $" ({PlayerName(ev.targetPlayerId)})" : "";
                    string pk = ev.type == MatchEventType.PenaltyGoal ? " (PK)" : "";
                    sb.AppendLine(
                        $"<size=85%>[{sideTag}] {ev.minute}' {PlayerName(ev.actorPlayerId)}{assist}{pk}</size>"
                    );
                }
            sb.AppendLine();

            // 카드 / 부상 / 교체
            AppendKeyEvents(
                sb,
                Localization.Get("mrd_cards"),
                e =>
                    e.type == MatchEventType.YellowCard
                    || e.type == MatchEventType.RedCard
                    || e.type == MatchEventType.SecondYellow,
                e =>
                {
                    string c =
                        e.type == MatchEventType.YellowCard ? Localization.Get("mrd_card_yellow")
                        : e.type == MatchEventType.SecondYellow
                            ? Localization.Get("mrd_card_second_yellow")
                        : Localization.Get("mrd_card_red");
                    return $"{e.minute}' {c} - {PlayerName(e.actorPlayerId)}";
                }
            );
            AppendKeyEvents(
                sb,
                Localization.Get("mrd_injuries"),
                e => e.type == MatchEventType.Injury,
                e => $"{e.minute}' {Localization.Get("mrd_injury")} - {PlayerName(e.actorPlayerId)}"
            );
            AppendKeyEvents(
                sb,
                Localization.Get("mrd_subs"),
                e => e.type == MatchEventType.Substitution,
                e => $"{e.minute}' {PlayerName(e.targetPlayerId)} ▶ {PlayerName(e.actorPlayerId)}"
            );

            overviewText.text = sb.ToString();
        }

        private void AppendKeyEvents(
            StringBuilder sb,
            string title,
            System.Func<MatchEvent, bool> filter,
            System.Func<MatchEvent, string> fmt
        )
        {
            var list = _match.events.Where(filter).ToList();
            if (list.Count == 0)
                return;
            sb.AppendLine($"<b>{title}</b>");
            foreach (var ev in list)
                sb.AppendLine($"<size=85%>{fmt(ev)}</size>");
            sb.AppendLine();
        }

        // ── 평점 탭 ────────────────────────────────────────────────────────
        private void BuildRatings()
        {
            if (ratingsText == null)
                return;
            var sb = new StringBuilder();

            // Man of the Match — 양 팀 최고 평점.
            var motm = _result
                .playerStats.Where(ps => ps.minutesPlayed > 0)
                .OrderByDescending(ps => ps.rating)
                .FirstOrDefault();
            if (motm != null)
                sb.AppendLine(
                    $"<b>MOTM</b>  {PlayerName(motm.playerId)}  {RatingTag(motm.rating)}"
                );
            sb.AppendLine();

            AppendTeamRatings(sb, _homeClub, "H");
            sb.AppendLine();
            AppendTeamRatings(sb, _awayClub, "A");

            ratingsText.text = sb.ToString();
        }

        private void AppendTeamRatings(StringBuilder sb, Club club, string tag)
        {
            if (club == null)
                return;
            sb.AppendLine($"<b>[{tag}] {club.name}</b>");
            var rows = _result
                .playerStats.Where(ps =>
                    ps.minutesPlayed > 0 && IsHomePlayer(ps.playerId) == (tag == "H")
                )
                .OrderByDescending(ps => ps.rating)
                .ToList();
            foreach (var ps in rows)
            {
                var p = _state.GetPlayer(ps.playerId);
                string pos = p?.info != null ? p.info.primaryPosition.ToString() : "-";
                string goals = ps.goals > 0 ? $"  ⚽{ps.goals}" : "";
                string assists = ps.assists > 0 ? $"  A{ps.assists}" : "";
                sb.AppendLine(
                    $"<size=85%>{pos, -3} {PlayerName(ps.playerId)} {RatingTag(ps.rating)}{goals}{assists}</size>"
                );
            }
        }

        // 평점 0~10 색상 (FM 정합). StatColorCoding 은 0~100 stat 용이라 별도.
        private static string RatingTag(float rating)
        {
            string hex =
                rating >= 8.0f ? "#2ECC71"
                : rating >= 7.0f ? "#82E08A"
                : rating >= 6.0f ? "#BBBBBB"
                : rating >= 5.0f ? "#F39C12"
                : "#E74C3C";
            return $"<color={hex}>{rating:0.0}</color>";
        }

        // ── 통계 탭 (유니코드 다이버징 바) ──────────────────────────────────
        private void BuildStats()
        {
            if (statsText == null)
                return;
            var sb = new StringBuilder();

            AddStatBar(
                sb,
                Localization.Get("mrd_stat_possession"),
                _result.homePossessionPct,
                _result.awayPossessionPct,
                "%"
            );

            int hShots = SumStat(true, ps => ps.shots);
            int aShots = SumStat(false, ps => ps.shots);
            AddStatBar(sb, Localization.Get("mrd_stat_shots"), hShots, aShots);

            int hOn = SumStat(true, ps => ps.shotsOnTarget);
            int aOn = SumStat(false, ps => ps.shotsOnTarget);
            AddStatBar(sb, Localization.Get("mrd_stat_shots_on"), hOn, aOn);

            int hCorner = CountEvents(0, MatchEventType.Corner);
            int aCorner = CountEvents(1, MatchEventType.Corner);
            AddStatBar(sb, Localization.Get("mrd_stat_corners"), hCorner, aCorner);

            int hFoul = SumStat(true, ps => ps.foulsCommitted);
            int aFoul = SumStat(false, ps => ps.foulsCommitted);
            AddStatBar(sb, Localization.Get("mrd_stat_fouls"), hFoul, aFoul);

            int hTackle = SumStat(true, ps => ps.tackles);
            int aTackle = SumStat(false, ps => ps.tackles);
            AddStatBar(sb, Localization.Get("mrd_stat_tackles"), hTackle, aTackle);

            // 패스 성공률
            int hPass = SumStat(true, ps => ps.passes);
            int hPassOk = SumStat(true, ps => ps.passesCompleted);
            int aPass = SumStat(false, ps => ps.passes);
            int aPassOk = SumStat(false, ps => ps.passesCompleted);
            float hPct = hPass > 0 ? 100f * hPassOk / hPass : 0f;
            float aPct = aPass > 0 ? 100f * aPassOk / aPass : 0f;
            AddStatBar(sb, Localization.Get("mrd_stat_pass_pct"), hPct, aPct, "%");

            statsText.text = sb.ToString();
        }

        private const int BarSegments = 12;

        private void AddStatBar(
            StringBuilder sb,
            string label,
            float home,
            float away,
            string unit = ""
        )
        {
            float total = home + away;
            float hRatio = total > 0.0001f ? home / total : 0.5f;
            int hSeg = Mathf.Clamp(Mathf.RoundToInt(BarSegments * hRatio), 0, BarSegments);
            int aSeg = BarSegments - hSeg;
            string hBar = $"<color=#E06666>{new string('█', hSeg)}</color>";
            string aBar = $"<color=#6FA8DC>{new string('█', aSeg)}</color>";
            string hVal = unit == "%" ? $"{home:0}%" : $"{home:0}";
            string aVal = unit == "%" ? $"{away:0}%" : $"{away:0}";
            sb.AppendLine($"<align=center><size=85%>{label}</size></align>");
            sb.AppendLine($"{hVal}  {hBar}{aBar}  {aVal}");
            sb.AppendLine();
        }

        private int SumStat(bool home, System.Func<PlayerMatchStat, int> sel)
        {
            return _result.playerStats.Where(ps => IsHomePlayer(ps.playerId) == home).Sum(sel);
        }

        private int CountEvents(int side, MatchEventType type)
        {
            return _match.events.Count(e => e.side == side && e.type == type);
        }

        // ── 히트맵 탭 (AA.4) ───────────────────────────────────────────────
        // zoneOccupancy[0..4] = HomeBox..AwayBox. 좌(홈 골문)→우(원정 골문).
        // 히트 강도: 볼이 오래 머문 지역일수록 뜨겁게(어두운 청록→주황→빨강). 각 밴드 위에 % 라벨.
        private void BuildHeatmap()
        {
            if (heatmapContainer == null)
                return;
            ClearChildren(heatmapContainer);

            var occ = _result.zoneOccupancy ?? new int[5];
            int max = Mathf.Max(1, occ.Length > 0 ? occ.Max() : 1);
            int total = Mathf.Max(1, occ.Sum());
            string[] labels =
            {
                Localization.Get("mrd_zone_home_box"),
                Localization.Get("mrd_zone_home_third"),
                Localization.Get("mrd_zone_midfield"),
                Localization.Get("mrd_zone_away_third"),
                Localization.Get("mrd_zone_away_box"),
            };
            var labelFont = heatmapLegend != null ? heatmapLegend.font : null;

            for (int i = 0; i < 5; i++)
            {
                int occ_i = i < occ.Length ? occ[i] : 0;
                float t = occ_i / (float)max;

                var band = new GameObject($"Band{i}", typeof(Image));
                var rt = band.GetComponent<RectTransform>();
                rt.SetParent(heatmapContainer, false);
                rt.anchorMin = new Vector2(i / 5f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 5f, 1f);
                rt.offsetMin = new Vector2(1.5f, 0f); // 밴드 사이 얇은 간격
                rt.offsetMax = new Vector2(-1.5f, 0f);
                band.GetComponent<Image>().color = HeatColor(t);

                // 밴드 중앙에 % + 구역명 라벨 (직관성 — 점3 피드백).
                var lblGo = new GameObject("Label", typeof(TextMeshProUGUI));
                var lrt = lblGo.GetComponent<RectTransform>();
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var lbl = lblGo.GetComponent<TextMeshProUGUI>();
                if (labelFont != null)
                    lbl.font = labelFont;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontSize = 28;
                lbl.color = Color.white;
                lbl.richText = true;
                float pct = 100f * occ_i / total;
                lbl.text = $"<size=160%><b>{pct:0}%</b></size>\n<size=90%>{labels[i]}</size>";
            }

            if (heatmapLegend != null)
                heatmapLegend.text = Localization.Get("mrd_heatmap_legend");
        }

        // 점유율 t(0~1) → 히트 색 (낮음=어두운 청록 → 중간=주황 → 높음=빨강).
        private static Color HeatColor(float t)
        {
            var low = new Color(0.13f, 0.22f, 0.30f);
            var mid = new Color(0.92f, 0.62f, 0.20f);
            var hi = new Color(0.85f, 0.22f, 0.18f);
            return t < 0.5f
                ? Color.Lerp(low, mid, t / 0.5f)
                : Color.Lerp(mid, hi, (t - 0.5f) / 0.5f);
        }

        // ── 슛맵 탭 (AA.5) ─────────────────────────────────────────────────
        // ShotPin.x = 슈터의 공격 골대=1. Home 우측 공격(nx=x), Away 좌측 공격(nx=1-x).
        // Goal=큰 점 / Saved=회색 / Off=작은 점. 진영 색 구분.
        private void BuildShotmap()
        {
            if (shotmapContainer == null)
                return;
            ClearChildren(shotmapContainer);

            // 피치 마킹 — 중앙선 / 페널티 박스 / 골 마크 (가독성: 진짜 피치처럼).
            var lineCol = new Color(1f, 1f, 1f, 0.30f);
            AddVLine(0.5f, 0f, 1f, 2f, lineCol); // 중앙선
            DrawBox(0f, 0.16f, 0.22f, 0.78f, lineCol); // 좌(홈 골문측) 박스
            DrawBox(0.84f, 1f, 0.22f, 0.78f, lineCol); // 우(원정 골문측) 박스
            var goalCol = new Color(1f, 1f, 1f, 0.65f);
            AddVLine(0.004f, 0.44f, 0.56f, 5f, goalCol); // 좌 골
            AddVLine(0.996f, 0.44f, 0.56f, 5f, goalCol); // 우 골

            // 팀 라벨 — 좌 절반=원정 슛, 우 절반=홈 슛 (방향 직관).
            AddCornerLabel(
                new Vector2(0.5f, 1f),
                new Vector2(1f, 1f),
                Localization.Get(
                    "mrd_team_home_dir",
                    _homeClub?.name ?? Localization.Get("mrd_home_short")
                ),
                HomeColor,
                TextAlignmentOptions.Right
            );
            AddCornerLabel(
                new Vector2(0f, 1f),
                new Vector2(0.5f, 1f),
                Localization.Get(
                    "mrd_team_away_dir",
                    _awayClub?.name ?? Localization.Get("mrd_away_short")
                ),
                AwayColor,
                TextAlignmentOptions.Left
            );

            var circle = GetCircleSprite();
            var shots = _result.shotMap ?? new List<ShotPin>();
            foreach (var pin in shots)
            {
                bool home = pin.side == 0;
                float nx = home ? pin.x : 1f - pin.x;
                float ny = Mathf.Clamp01(pin.y);

                float size =
                    pin.outcome == ShotOutcome.Goal ? 22f
                    : pin.outcome == ShotOutcome.Saved ? 13f
                    : 10f;
                Color col =
                    pin.outcome == ShotOutcome.Saved ? SavedColor : (home ? HomeColor : AwayColor);
                if (pin.outcome == ShotOutcome.Off)
                    col.a = 0.65f;

                var dot = new GameObject("Shot", typeof(Image));
                var drt = dot.GetComponent<RectTransform>();
                drt.SetParent(shotmapContainer, false);
                drt.anchorMin = new Vector2(nx, ny);
                drt.anchorMax = new Vector2(nx, ny);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.anchoredPosition = Vector2.zero;
                drt.sizeDelta = new Vector2(size, size);
                var img = dot.GetComponent<Image>();
                img.color = col;
                if (circle != null)
                    img.sprite = circle;

                // 골은 흰 테두리 헤일로로 강조.
                if (pin.outcome == ShotOutcome.Goal)
                {
                    var ring = new GameObject("Ring", typeof(Image));
                    var rrt = ring.GetComponent<RectTransform>();
                    rrt.SetParent(drt, false);
                    rrt.anchorMin = Vector2.zero;
                    rrt.anchorMax = Vector2.one;
                    rrt.offsetMin = new Vector2(-4f, -4f);
                    rrt.offsetMax = new Vector2(4f, 4f);
                    var rimg = ring.GetComponent<Image>();
                    rimg.color = new Color(1f, 1f, 1f, 0.9f);
                    rimg.raycastTarget = false; // 핀만 호버 받도록
                    if (circle != null)
                        rimg.sprite = circle;
                    rrt.SetAsFirstSibling(); // 점 뒤 → 테두리처럼
                }

                // 호버 툴팁 — 슈터 (+ 골은 어시스트).
                AttachShotTooltip(dot, pin);
            }

            if (shotmapLegend != null)
                shotmapLegend.text = Localization.Get("mrd_shotmap_legend");
        }

        // 슛맵 피치 라인/라벨 헬퍼.
        private void AddLine(
            float xMin,
            float yMin,
            float xMax,
            float yMax,
            float thickness,
            Color col
        )
        {
            var go = new GameObject("Line", typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(shotmapContainer, false);
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(
                Mathf.Approximately(xMin, xMax) ? thickness : 0f,
                Mathf.Approximately(yMin, yMax) ? thickness : 0f
            );
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = false; // 핀 호버 방해 금지
        }

        private void AddVLine(float x, float y0, float y1, float th, Color c) =>
            AddLine(x, y0, x, y1, th, c);

        private void AddHLine(float y, float x0, float x1, float th, Color c) =>
            AddLine(x0, y, x1, y, th, c);

        // 골라인(바깥) 변은 피치 경계 → 생략. 안쪽 수직 + 위/아래 수평만.
        private void DrawBox(float x0, float x1, float y0, float y1, Color c)
        {
            bool leftBox = x0 <= 0.0001f;
            float innerX = leftBox ? x1 : x0;
            AddVLine(innerX, y0, y1, 2f, c);
            AddHLine(y0, x0, x1, 2f, c);
            AddHLine(y1, x0, x1, 2f, c);
        }

        private void AddCornerLabel(
            Vector2 aMin,
            Vector2 aMax,
            string text,
            Color col,
            TextAlignmentOptions align
        )
        {
            var go = new GameObject("TeamLabel", typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(shotmapContainer, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(10f, -36f);
            rt.offsetMax = new Vector2(-10f, -6f);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (shotmapLegend != null && shotmapLegend.font != null)
                t.font = shotmapLegend.font;
            t.fontSize = 20;
            t.color = col;
            t.richText = true;
            t.alignment = align;
            t.raycastTarget = false; // 핀 호버 방해 금지
            t.text = text;
        }

        // 슛 핀 호버 툴팁 — 슈터 · 결과 (+ 골은 어시스트). 핀 위에 비활성 패널로 부착.
        private void AttachShotTooltip(GameObject dot, ShotPin pin)
        {
            string shooter = pin.shooterId > 0 ? PlayerName(pin.shooterId) : "?";
            string outcome =
                pin.outcome == ShotOutcome.Goal ? Localization.Get("mrd_shot_goal")
                : pin.outcome == ShotOutcome.Saved ? Localization.Get("mrd_shot_saved")
                : Localization.Get("mrd_shot_off");
            bool hasAssist = pin.outcome == ShotOutcome.Goal && pin.assistId > 0;

            var tip = new GameObject("Tip", typeof(Image));
            var trt = tip.GetComponent<RectTransform>();
            trt.SetParent(dot.transform, false);
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 8f);
            trt.sizeDelta = new Vector2(230f, hasAssist ? 58f : 34f);
            var bg = tip.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            bg.raycastTarget = false;

            var txtGo = new GameObject("Text", typeof(TextMeshProUGUI));
            var xrt = txtGo.GetComponent<RectTransform>();
            xrt.SetParent(trt, false);
            xrt.anchorMin = Vector2.zero;
            xrt.anchorMax = Vector2.one;
            xrt.offsetMin = new Vector2(8f, 4f);
            xrt.offsetMax = new Vector2(-8f, -4f);
            var txt = txtGo.GetComponent<TextMeshProUGUI>();
            if (shotmapLegend != null && shotmapLegend.font != null)
                txt.font = shotmapLegend.font;
            txt.fontSize = 16;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.richText = true;
            txt.raycastTarget = false;
            txt.text = hasAssist
                ? $"{shooter} · {outcome}\n{Localization.Get("mrd_assist")}: {PlayerName(pin.assistId)}"
                : $"{shooter} · {outcome}";

            tip.SetActive(false);
            dot.AddComponent<ShotPinTooltip>().Init(tip);
        }

        // ── 이벤트 탭 ──────────────────────────────────────────────────────
        private void BuildEvents()
        {
            if (eventsText == null)
                return;
            var sb = new StringBuilder();
            foreach (var ev in _match.events)
            {
                if (!MatchEventDisplay.ShouldShowText(ev.type) || string.IsNullOrEmpty(ev.textKey))
                    continue;
                sb.AppendLine($"<size=90%>{ev.minute}'  {FormatEvent(ev)}</size>");
            }
            if (sb.Length == 0)
                sb.AppendLine(Localization.Get("mrd_no_events"));
            eventsText.text = sb.ToString();
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────
        private bool IsHomePlayer(int playerId)
        {
            if (_result.homeStarting11 != null && _result.homeStarting11.Contains(playerId))
                return true;
            if (_result.awayStarting11 != null && _result.awayStarting11.Contains(playerId))
                return false;
            if (_homeClub?.seniorSquadIds != null && _homeClub.seniorSquadIds.Contains(playerId))
                return true;
            return false; // 미확인 시 원정으로 간주 (회귀 영향 미미).
        }

        private string PlayerName(int playerId)
        {
            var p = _state?.GetPlayer(playerId);
            return p?.info != null ? $"{p.info.firstName} {p.info.lastName}" : $"#{playerId}";
        }

        private Match FindMatch(int matchId)
        {
            if (_state == null)
                return null;
            foreach (var league in _state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                    if (m.id == matchId)
                        return m;
            }
            return null;
        }

        private static string FormatEvent(MatchEvent ev)
        {
            if (string.IsNullOrEmpty(ev.textKey))
                return ev.type.ToString();
            string text = Localization.Get(ev.textKey);
            if (ev.textArgs != null)
                foreach (var kv in ev.textArgs)
                    text = text.Replace("{" + kv.Key + "}", kv.Value);
            return text;
        }

        private static void ClearChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static Sprite _circleSprite;

        // 절차적 원형 스프라이트 (내장 Knob.psd 로드 실패 대응 — 사각 핀 회피).
        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null)
                return _circleSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            var center = new Vector2(r, r);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(r - d); // 가장자리 1px 안티에일리어싱
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }
    }
}
