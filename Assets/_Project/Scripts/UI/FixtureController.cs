// Task 13.9 (Issue #54) — 일정 / 결과 화면.
// V1.0 M.2 (#528): MUIP 3탭 재작업 (리그 / 컵 / 통합). 컵 = Stage O 미구현 → 비활성 placeholder.
// 통합 = 현재 리그와 동일 (컵 도입 시 합산). 완료 매치 클릭 진입은 FixtureItem(AA.6) 담당.
// 탭 전환 = ButtonManager + 패널 토글 (MatchResultController 패턴, WindowManager 잔상 회피).

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
    public class FixtureController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [Header("탭 (MUIP ButtonManager)")]
        [SerializeField]
        private ButtonManager tabLeague;

        [SerializeField]
        private ButtonManager tabCup; // Stage O 미구현 → 비활성

        [SerializeField]
        private ButtonManager tabCombined;

        [Header("탭 패널")]
        [SerializeField]
        private GameObject panelLeague;

        [SerializeField]
        private GameObject panelCombined;

        [Header("목록")]
        [SerializeField]
        private Transform listParentLeague;

        [SerializeField]
        private Transform listParentCombined;

        [SerializeField]
        private GameObject fixtureItemPrefab;

        private GameState _state;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            WireTab(tabLeague, Localization.Get("schedule_tab_league"), 0);
            WireTab(tabCombined, Localization.Get("schedule_tab_combined"), 1);

            // 컵 탭 = Stage O 도입 전까지 비활성 placeholder.
            if (tabCup != null)
            {
                tabCup.buttonText = Localization.Get("schedule_tab_cup");
                tabCup.UpdateUI();
                var cupBtn = tabCup.GetComponent<Button>();
                if (cupBtn != null)
                    cupBtn.interactable = false;
            }

            Refresh();
            ShowTab(0);
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

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
            if (panelLeague != null)
                panelLeague.SetActive(index == 0);
            if (panelCombined != null)
                panelCombined.SetActive(index == 1);

            SetTabCurrent(tabLeague, index == 0);
            SetTabCurrent(tabCombined, index == 1);
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

        // ── 목록 ──────────────────────────────────────────────────────────
        private void Refresh()
        {
            var userClub = _state.GetClub(_state.userClubId);
            if (userClub == null)
                return;

            var league = _state.leagues.FirstOrDefault(l => l.clubIds.Contains(_state.userClubId));
            if (league == null)
                return;

            if (titleText != null)
            {
                var leagueConfig = GameDatabase.GetLeagueConfig(league.configSOId);
                titleText.text =
                    leagueConfig != null
                        ? Localization.Get(
                            "fixture_title_fmt",
                            leagueConfig.displayName,
                            league.seasonYear
                        )
                        : Localization.Get("fixture_title_fallback_fmt", league.seasonYear);
            }

            var sorted = league.schedule.OrderBy(m => m.date).ToList();

            // 리그 = 통합 (컵 미구현). 두 패널에 동일 일정 표시.
            Populate(listParentLeague, sorted);
            Populate(listParentCombined, sorted);
        }

        private void Populate(Transform parent, List<Match> matches)
        {
            if (parent == null || fixtureItemPrefab == null)
                return;

            foreach (Transform child in parent)
                Destroy(child.gameObject);

            foreach (var match in matches)
            {
                var item = Instantiate(fixtureItemPrefab, parent);
                item.GetComponent<FixtureItem>().Setup(match, _state, _state.userClubId);
            }

            Canvas.ForceUpdateCanvases();
            if (parent is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }
}
