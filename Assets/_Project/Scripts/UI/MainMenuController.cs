// Task 13.1 (Issue #46) — 메인 메뉴 씬 컨트롤러.
// New Game → 시드 입력 → ClubSelectScene
// Load Game → 슬롯 선택 → DashboardScene (userClubId != -1) or ClubSelectScene

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private const string ClubSelectScene = "ClubSelectScene";
        private const string DashboardScene = "DashboardScene";

        [Header("패널")]
        [SerializeField]
        private GameObject mainPanel;

        [SerializeField]
        private GameObject newGamePanel;

        [SerializeField]
        private GameObject loadGamePanel;

        [Header("New Game")]
        [SerializeField]
        private TMP_InputField seedInput;

        [Header("Load Game")]
        [SerializeField]
        private Transform slotListParent;

        [SerializeField]
        private GameObject slotItemPrefab;

        [SerializeField]
        private TMP_Text noSlotsText;

        [Header("데이터")]
        [SerializeField]
        private LeagueConfigSO leagueConfig;

        [SerializeField]
        private GameBalanceSO balance;

        [Header("경질 팝업")]
        [SerializeField]
        private GameObject sackedPanel;

        private void Start()
        {
            ShowMainPanel();
            CheckSackedFlag();
        }

        private void CheckSackedFlag()
        {
            if (!PlayerPrefs.HasKey(FMLite.Core.GameManager.SackedKey))
                return;
            PlayerPrefs.DeleteKey(FMLite.Core.GameManager.SackedKey);
            PlayerPrefs.Save();
            if (sackedPanel != null)
                sackedPanel.SetActive(true);
        }

        public void OnSackedPanelCloseClicked()
        {
            if (sackedPanel != null)
                sackedPanel.SetActive(false);
        }

        public void OnNewGameClicked()
        {
            mainPanel.SetActive(false);
            newGamePanel.SetActive(true);
        }

        public void OnLoadGameClicked()
        {
            mainPanel.SetActive(false);
            loadGamePanel.SetActive(true);
            PopulateSlotList();
        }

        public void OnCancelClicked()
        {
            ShowMainPanel();
        }

        public void OnConfirmNewGame()
        {
            GameDatabase.LoadAll();
            LocalizationSystem.Initialize(GameDatabase.LocalizationData);
            int seed = ParseSeed(seedInput != null ? seedInput.text : string.Empty);
            var seasonStart = new DateTime(DateTime.Today.Year, 7, 1);
            var state = GameInitializer.NewGame(seed, seasonStart, leagueConfig, balance);
            GameManager.Instance.SetState(state);
            SceneManager.LoadScene(ClubSelectScene);
        }

        private void PopulateSlotList()
        {
            foreach (Transform child in slotListParent)
                Destroy(child.gameObject);

            List<SaveSlotMeta> slots = SaveSystem.ListSlots();

            if (noSlotsText != null)
                noSlotsText.gameObject.SetActive(slots.Count == 0);

            foreach (var meta in slots)
            {
                var item = Instantiate(slotItemPrefab, slotListParent);
                item.GetComponent<SaveSlotItem>().Setup(meta, LoadSlot);
            }
        }

        private void LoadSlot(string slotName)
        {
            GameDatabase.LoadAll();
            LocalizationSystem.Initialize(GameDatabase.LocalizationData);
            var state = SaveSystem.Load(slotName);
            if (state == null)
            {
                GameLog.Log(LogCategory.System, $"슬롯 로드 실패: {slotName}");
                return;
            }
            InboxRouter.Wire(state); // V1.0 #66 — 로드 후 새 state 로 핸들러 갱신
            GameManager.Instance.SetState(state);
            var target = state.userClubId == -1 ? ClubSelectScene : DashboardScene;
            SceneManager.LoadScene(target);
        }

        private void ShowMainPanel()
        {
            mainPanel.SetActive(true);
            newGamePanel.SetActive(false);
            loadGamePanel.SetActive(false);
        }

        private static int ParseSeed(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return UnityEngine.Random.Range(1, int.MaxValue);
            if (int.TryParse(text, out int n))
                return n;
            return Math.Abs(text.GetHashCode());
        }
    }
}
