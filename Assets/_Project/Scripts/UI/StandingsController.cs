// Task 13.10 (Issue #55) — 리그 순위표 화면.
// V1.0 M.1 (#528): MUIP 2탭 재작업 (순위표 / 리더보드). 순위 변동 화살표(인메모리 직전순위 diff)
// + 자기 구단 강조. 리더보드 탭은 LeaderboardController (독립) 가 담당.
// 탭 전환 = ButtonManager + 패널 토글 (MatchResultController/TacticLineup 패턴, WindowManager 잔상 회피).

using System.Collections.Generic;
using System.Linq;
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
    public class StandingsController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [Header("탭 (MUIP ButtonManager)")]
        [SerializeField]
        private ButtonManager tabStandings;

        [SerializeField]
        private ButtonManager tabLeaderboard;

        [Header("탭 패널")]
        [SerializeField]
        private GameObject panelStandings;

        [SerializeField]
        private GameObject panelLeaderboard;

        [Header("순위표 목록")]
        [SerializeField]
        private Transform listParent;

        [SerializeField]
        private GameObject entryItemPrefab;

        private GameState _state;

        // 직전 순위 스냅샷 (clubId → rank). static 이라 씬 재진입 간 유지 →
        // "마지막으로 본 시점 대비" 변동 화살표. 세션 첫 진입은 비어 있어 화살표 없음.
        private static readonly Dictionary<int, int> _prevRanks = new Dictionary<int, int>();

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            WireTab(tabStandings, Localization.Get("standings_tab_table"), 0);
            WireTab(tabLeaderboard, Localization.Get("standings_tab_leaderboard"), 1);

            Refresh();
            ShowTab(0);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MatchFinishedEvent>(OnMatchFinished);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MatchFinishedEvent>(OnMatchFinished);
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        private void OnMatchFinished(MatchFinishedEvent e) => Refresh();

        // ── 탭 전환 (MatchResultController 패턴) ──────────────────────────
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
            if (panelStandings != null)
                panelStandings.SetActive(index == 0);
            if (panelLeaderboard != null)
                panelLeaderboard.SetActive(index == 1);

            SetTabCurrent(tabStandings, index == 0);
            SetTabCurrent(tabLeaderboard, index == 1);
        }

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

        // ── 순위표 ────────────────────────────────────────────────────────
        private void Refresh()
        {
            var league = _state.leagues.FirstOrDefault(l =>
                l.clubIds.Contains(_state.userClubId)
            );
            if (league == null)
                return;

            if (titleText != null)
            {
                var config = GameDatabase.GetLeagueConfig(league.configSOId);
                titleText.text =
                    config != null
                        ? Localization.Get(
                            "standings_title_fmt",
                            config.displayName,
                            league.seasonYear
                        )
                        : Localization.Get("standings_title_fallback_fmt", league.seasonYear);
            }

            if (listParent == null || entryItemPrefab == null || league.standings == null)
                return;

            foreach (Transform child in listParent)
                Destroy(child.gameObject);

            var sorted = league
                .standings.entries.OrderByDescending(e => e.points)
                .ThenByDescending(e => e.goalsFor - e.goalsAgainst)
                .ThenByDescending(e => e.goalsFor)
                .ToList();

            // 이번 갱신의 순위 (clubId → rank). 화살표는 직전 스냅샷과의 diff.
            var currentRanks = new Dictionary<int, int>();
            for (int i = 0; i < sorted.Count; i++)
                currentRanks[sorted[i].clubId] = i + 1;

            for (int i = 0; i < sorted.Count; i++)
            {
                int rank = i + 1;
                int clubId = sorted[i].clubId;
                bool hasDelta = _prevRanks.TryGetValue(clubId, out int prevRank);
                int rankDelta = hasDelta ? prevRank - rank : 0; // 양수 = 상승

                var item = Instantiate(entryItemPrefab, listParent);
                item.GetComponent<StandingsEntryItem>()
                    .Setup(rank, sorted[i], _state, _state.userClubId, rankDelta, hasDelta);
            }

            // 다음 진입 비교용으로 스냅샷 갱신.
            _prevRanks.Clear();
            foreach (var kv in currentRanks)
                _prevRanks[kv.Key] = kv.Value;
        }
    }
}
