// Task 13.9 (Issue #54) — 일정 / 결과 화면.
// 유저 구단이 속한 리그 전체 일정 + 결과 표시.

using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.UI
{
    public class FixtureController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [Header("목록")]
        [SerializeField]
        private Transform listParent;

        [SerializeField]
        private GameObject fixtureItemPrefab;

        private GameState _state;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            Refresh();
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

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

            foreach (Transform child in listParent)
                Destroy(child.gameObject);

            var sorted = league.schedule.OrderBy(m => m.date).ToList();
            foreach (var match in sorted)
            {
                var item = Instantiate(fixtureItemPrefab, listParent);
                item.GetComponent<FixtureItem>().Setup(match, _state, _state.userClubId);
            }
        }
    }
}
