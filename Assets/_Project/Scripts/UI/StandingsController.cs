// Task 13.10 (Issue #55) — 리그 순위표 화면.
// Standings.entries 승점 내림차순 표시. MatchFinishedEvent 구독으로 자동 갱신.

using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.UI
{
    public class StandingsController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [Header("목록")]
        [SerializeField]
        private Transform listParent;

        [SerializeField]
        private GameObject entryItemPrefab;

        private GameState _state;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            Refresh();
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

            foreach (Transform child in listParent)
                Destroy(child.gameObject);

            if (league.standings == null)
                return;

            var sorted = league
                .standings.entries.OrderByDescending(e => e.points)
                .ThenByDescending(e => e.goalsFor - e.goalsAgainst)
                .ThenByDescending(e => e.goalsFor)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var item = Instantiate(entryItemPrefab, listParent);
                item.GetComponent<StandingsEntryItem>()
                    .Setup(i + 1, sorted[i], _state, _state.userClubId);
            }
        }
    }
}
