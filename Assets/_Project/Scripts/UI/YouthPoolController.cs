// Task 13.7 (Issue #52) — 유스 풀 화면.
// YouthIntakeAvailableEvent 로 DashboardController 가 자동 전환하거나
// 사이드 메뉴 "유스" 버튼으로 직접 진입.
// 후보 목록 표시 + 리롤(토큰 소진 시 비활성) + 영입 확정.

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class YouthPoolController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text tokenText;

        [SerializeField]
        private TMP_Text facilityText;

        [Header("후보 목록")]
        [SerializeField]
        private Transform candidateListParent;

        [SerializeField]
        private GameObject candidateItemPrefab;

        [Header("버튼")]
        [SerializeField]
        private Button rerollButton;

        [SerializeField]
        private Button signButton;

        private GameState _state;
        private Club _userClub;
        private YouthIntake _currentIntake;
        private readonly HashSet<int> _selectedIds = new();

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            _userClub = GameManager.Instance.UserClub;
            if (_userClub == null)
                return;

            // 가장 최근 intake (아직 candidatePlayerIds 있는 것)
            _currentIntake = _userClub.intakeHistory.LastOrDefault(i =>
                i.candidatePlayerIds.Count > 0
            );

            if (_currentIntake == null)
            {
                if (titleText != null)
                    titleText.text = Localization.Get("no_current_inspection");
                if (rerollButton != null)
                    rerollButton.interactable = false;
                if (signButton != null)
                    signButton.interactable = false;
                return;
            }

            EventBus.Subscribe<YouthRerolledEvent>(OnYouthRerolled);
            Refresh();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<YouthRerolledEvent>(OnYouthRerolled);
        }

        public void OnRerollClicked()
        {
            if (_state == null || _currentIntake == null)
                return;
            if (_state.rerollTokens <= 0)
                return;

            var leagueConfig = GetLeagueConfig();
            if (leagueConfig == null)
            {
                Debug.LogError("[YouthPoolController] LeagueConfigSO not found");
                return;
            }

            _selectedIds.Clear();
            YouthSystem.UseRerollToken(
                _currentIntake,
                _userClub,
                _state,
                GameDatabase.GameBalance,
                leagueConfig
            );
            // Refresh는 YouthRerolledEvent 핸들러에서
        }

        public void OnSignClicked()
        {
            if (_state == null || _currentIntake == null)
                return;

            YouthSystem.SignPlayers(_currentIntake, _selectedIds.ToList(), _userClub, _state);
            SceneManager.LoadScene(DashboardScene);
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        private void OnYouthRerolled(YouthRerolledEvent e)
        {
            if (e.intakeId != _currentIntake.id)
                return;
            Refresh();
        }

        private void Refresh()
        {
            if (titleText != null)
                titleText.text = Localization.Get(
                    "youth_inspection_title_fmt",
                    _currentIntake.intakeDate.ToString("yyyy-MM-dd")
                );

            if (tokenText != null)
                tokenText.text = Localization.Get("reroll_token_fmt", _state.rerollTokens);

            var facility = GameDatabase.GetFacilityLevel(
                FacilityType.Youth,
                _userClub.facilities.youthLevel
            );
            if (facilityText != null)
                facilityText.text =
                    facility != null
                        ? Localization.Get(
                            "youth_facility_full_fmt",
                            _userClub.facilities.youthLevel,
                            facility.youthPoolSize
                        )
                        : Localization.Get("youth_facility_fmt", _userClub.facilities.youthLevel);

            PopulateCandidates();

            if (rerollButton != null)
                rerollButton.interactable = _state.rerollTokens > 0;

            if (signButton != null)
                signButton.interactable = true;
        }

        private void PopulateCandidates()
        {
            foreach (Transform child in candidateListParent)
                Destroy(child.gameObject);

            _selectedIds.Clear();

            foreach (var id in _currentIntake.candidatePlayerIds)
            {
                var player = _state.GetPlayer(id);
                if (player == null)
                    continue;

                var item = Instantiate(candidateItemPrefab, candidateListParent);
                item.GetComponent<YouthCandidateItem>().Setup(player, _state, OnCandidateToggled);
            }
        }

        private void OnCandidateToggled(int playerId, bool selected)
        {
            if (selected)
                _selectedIds.Add(playerId);
            else
                _selectedIds.Remove(playerId);
        }

        private LeagueConfigSO GetLeagueConfig()
        {
            var league = _state.leagues.FirstOrDefault(l => l.clubIds.Contains(_userClub.id));
            return league != null ? GameDatabase.GetLeagueConfig(league.configSOId) : null;
        }
    }
}
