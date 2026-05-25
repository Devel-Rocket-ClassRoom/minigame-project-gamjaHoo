// Task 13.2 (Issue #47) — 구단 선택 화면.
// GameState의 리그 구단 목록 표시 → 선택 시 userClubId 설정 → GachaScene 전환.

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
    public class ClubSelectController : MonoBehaviour
    {
        private const string GachaScene = "GachaScene";

        [Header("구단 목록")]
        [SerializeField]
        private Transform clubListParent;

        [SerializeField]
        private GameObject clubItemPrefab;

        [Header("선택 정보 패널")]
        [SerializeField]
        private GameObject confirmPanel;

        [SerializeField]
        private TMP_Text selectedClubNameText;

        [SerializeField]
        private TMP_Text selectedClubReputationText;

        [SerializeField]
        private Button confirmButton;

        private int selectedClubId = -1;

        private void Start()
        {
            confirmPanel.SetActive(false);
            PopulateClubList();
        }

        private void PopulateClubList()
        {
            var state = GameManager.Instance.State;
            if (state == null)
                return;

            var clubs = state
                .allClubs.Where(c => state.leagues.Any(l => l.clubIds.Contains(c.id)))
                .OrderByDescending(c => c.reputation)
                .ToList();

            foreach (var club in clubs)
            {
                var item = Instantiate(clubItemPrefab, clubListParent);
                item.GetComponent<ClubListItem>().Setup(club, OnClubSelected);
            }
        }

        private void OnClubSelected(int clubId)
        {
            selectedClubId = clubId;
            var club = GameManager.Instance.State.GetClub(clubId);
            if (club == null)
                return;

            selectedClubNameText.text = club.name;
            selectedClubReputationText.text = Localization.Get("reputation_fmt", club.reputation);
            confirmPanel.SetActive(true);
        }

        public void OnConfirmClicked()
        {
            if (selectedClubId == -1)
                return;

            GameManager.Instance.State.userClubId = selectedClubId;
            SceneManager.LoadScene(GachaScene);
        }

        public void OnCancelClicked()
        {
            selectedClubId = -1;
            confirmPanel.SetActive(false);
        }
    }
}
