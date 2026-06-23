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
        private const string OptionsScene = "OptionsScene";
        private const string MainMenuScene = "MainMenuScene";
        private const string HallOfFameScene = "HallOfFameScene";

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

        [Header("명예의 전당 (Firebase)")]
        [SerializeField]
        private Button hallOfFameButton;

        [Header("로컬라이즈 라벨 (런타임)")]
        [SerializeField]
        private TMP_Text newGameLabel;

        [SerializeField]
        private TMP_Text loadGameLabel;

        [SerializeField]
        private TMP_Text hallOfFameLabel;

        [SerializeField]
        private TMP_Text optionsLabel;

        [SerializeField]
        private TMP_Text quitLabel;

        [SerializeField]
        private TMP_Text seedLabel;

        [SerializeField]
        private TMP_Text seedPlaceholder;

        [SerializeField]
        private TMP_Text newGameConfirmLabel;

        [SerializeField]
        private TMP_Text newGameCancelLabel;

        [SerializeField]
        private TMP_Text loadGameCancelLabel;

        private void Start()
        {
            EnsureCoreInitialized();
            LocalizeLabels();
            ShowMainPanel();
            CheckSackedFlag();

            if (hallOfFameButton != null)
            {
                hallOfFameButton.onClick.RemoveAllListeners();
                hallOfFameButton.onClick.AddListener(OnHallOfFameClicked);
            }
        }

        // 메인 메뉴 라벨을 현재 언어로. 버튼/패널 텍스트가 씬에 정적이라 런타임 덮어씀
        // (GlobalNavController.LocalizeLabels 패턴). 브랜드명/버전은 제외.
        private void LocalizeLabels()
        {
            SetLabel(newGameLabel, "menu_new_game");
            SetLabel(loadGameLabel, "menu_load_game");
            SetLabel(hallOfFameLabel, "menu_hall_of_fame");
            SetLabel(optionsLabel, "menu_options");
            SetLabel(quitLabel, "menu_quit");
            SetLabel(seedLabel, "menu_seed_label");
            SetLabel(seedPlaceholder, "menu_seed_placeholder");
            SetLabel(newGameConfirmLabel, "menu_confirm");
            SetLabel(newGameCancelLabel, "menu_cancel");
            SetLabel(loadGameCancelLabel, "menu_cancel");
            SetLabel(noSlotsText, "menu_no_slots");
        }

        private static void SetLabel(TMP_Text label, string key)
        {
            if (label != null)
                label.text = Localization.Get(key);
        }

        // 앱 진입점(MainMenuScene = build 0)에서 Localization/Options 초기화.
        // 정적 LocalizationSystem 은 씬 전환에도 유지 → 게임 미시작 상태로 OptionsScene 진입해도
        // LocalizedText 가 키 원문이 아닌 번역 텍스트를 표시. (게임 시작 시 재초기화는 무해)
        private static void EnsureCoreInitialized()
        {
            if (GameDatabase.LocalizationData == null)
                GameDatabase.LoadAll();
            OptionsManager.EnsureInitialized();
            LocalizationSystem.Initialize(GameDatabase.LocalizationData, OptionsManager.Language);
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

        // S.2 (#77-7) — 메인 메뉴에서 옵션 진입 (복귀 = MainMenuScene).
        public void OnOptionsClicked()
        {
            PlayerPrefs.SetString(OptionsController.PreviousSceneKey, MainMenuScene);
            SceneManager.LoadScene(OptionsScene);
        }

        // 명예의 전당 (Firebase 학습 기능) — 게임 로드 불필요한 전역 화면. 복귀 = MainMenuScene.
        public void OnHallOfFameClicked()
        {
            SceneManager.LoadScene(HallOfFameScene);
        }

        public void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        public void OnConfirmNewGame()
        {
            GameDatabase.LoadAll();
            OptionsManager.EnsureInitialized();
            LocalizationSystem.Initialize(GameDatabase.LocalizationData, OptionsManager.Language);
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
            OptionsManager.EnsureInitialized();
            LocalizationSystem.Initialize(GameDatabase.LocalizationData, OptionsManager.Language);
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
