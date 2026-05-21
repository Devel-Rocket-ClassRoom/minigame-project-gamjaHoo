// Task 13.5 (Issue #50) — 스쿼드 화면.
// 유저 구단 1군 / 유스 명단 탭 표시. 선수 클릭 → PlayerProfileScene.
// 선택 선수 ID는 PlayerPrefs("SelectedPlayerId")로 씬 간 전달.

using System.Collections.Generic;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class SquadController : MonoBehaviour
    {
        internal const string SelectedPlayerIdKey = "SelectedPlayerId";
        private const string PlayerProfileScene = "PlayerProfileScene";
        private const string DashboardScene = "DashboardScene";

        [Header("탭 패널")]
        [SerializeField]
        private GameObject seniorPanel;

        [SerializeField]
        private GameObject youthPanel;

        [Header("선수 목록")]
        [SerializeField]
        private Transform seniorListParent;

        [SerializeField]
        private Transform youthListParent;

        [SerializeField]
        private GameObject playerItemPrefab;

        [Header("헤더")]
        [SerializeField]
        private TMP_Text titleText;

        private void Start()
        {
            var state = GameManager.Instance.State;
            var club = GameManager.Instance.UserClub;
            if (state == null || club == null)
                return;

            if (titleText != null)
                titleText.text = club.name;

            PopulateList(seniorListParent, club.seniorSquadIds, state);
            PopulateList(youthListParent, club.youthSquadIds, state);

            ShowSenior();
        }

        public void OnSeniorTabClicked() => ShowSenior();

        public void OnYouthTabClicked() => ShowYouth();

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        private void ShowSenior()
        {
            seniorPanel.SetActive(true);
            youthPanel.SetActive(false);
        }

        private void ShowYouth()
        {
            seniorPanel.SetActive(false);
            youthPanel.SetActive(true);
        }

        private void PopulateList(Transform parent, List<int> ids, GameState state)
        {
            foreach (Transform child in parent)
                Destroy(child.gameObject);

            foreach (var id in ids)
            {
                var player = state.GetPlayer(id);
                if (player == null)
                    continue;

                var item = Instantiate(playerItemPrefab, parent);
                item.GetComponent<PlayerListItem>().Setup(player, state, OnPlayerSelected);
            }
        }

        private void OnPlayerSelected(int playerId)
        {
            PlayerPrefs.SetInt(SelectedPlayerIdKey, playerId);
            SceneManager.LoadScene(PlayerProfileScene);
        }
    }
}
