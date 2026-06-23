// Task 13.1 (Issue #46) — 메인 메뉴 씬 컨트롤러.
// New Game → 시드 입력 → ClubSelectScene
// Load Game → 슬롯 선택 → DashboardScene (userClubId != -1) or ClubSelectScene

using System;
using System.Collections.Generic;
using FirebaseKit;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Persistence;
using FMLite.Persistence.Cloud;
using Michsky.UI.ModernUIPack;
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

        [Header("계정 (Firebase 인증)")]
        [SerializeField]
        private Button accountActionButton; // 메인 패널: 게스트=로그인 열기 / 계정=로그아웃 (MUIP)

        [SerializeField]
        private TMP_Text accountStatusText; // "게스트" 또는 이메일

        [SerializeField]
        private GameObject loginPanel;

        [SerializeField]
        private TMP_InputField emailInput;

        [SerializeField]
        private TMP_InputField passwordInput;

        [SerializeField]
        private TMP_InputField nicknameInput; // 회원가입 시 닉네임

        [SerializeField]
        private Button loginButton;

        [SerializeField]
        private Button signupButton;

        [SerializeField]
        private Button authCancelButton;

        [SerializeField]
        private TMP_Text authStatusText; // 처리 중 / 오류

        [Header("로그인 패널 로컬라이즈 라벨")]
        [SerializeField]
        private TMP_Text loginTitleLabel;

        [SerializeField]
        private TMP_Text emailPlaceholder;

        [SerializeField]
        private TMP_Text passwordPlaceholder;

        [SerializeField]
        private TMP_Text nicknamePlaceholder;

        private void Start()
        {
            EnsureCoreInitialized();
            LocalizeLabels();
            ShowMainPanel();
            CheckSackedFlag();

            WireButton(hallOfFameButton, OnHallOfFameClicked);
            WireButton(accountActionButton, OnAccountActionClicked);
            WireButton(loginButton, OnLoginClicked);
            WireButton(signupButton, OnSignupClicked);
            WireButton(authCancelButton, CloseAuthPanel);

            AuthManager.StateChanged += RefreshAccountStatus;
            RefreshAccountStatus();
        }

        private void OnDestroy()
        {
            AuthManager.StateChanged -= RefreshAccountStatus;
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        // 메인 메뉴 라벨을 현재 언어로. 버튼/패널 텍스트가 씬에 정적이라 런타임 덮어씀
        // (GlobalNavController.LocalizeLabels 패턴). 브랜드명/버전은 제외.
        private void LocalizeLabels()
        {
            // 기존 공용 버튼(레거시) — 자식 TMP 라벨 직접 세팅. (전역 MUIP 재디자인은 별도 task)
            SetLabel(newGameLabel, "menu_new_game");
            SetLabel(loadGameLabel, "menu_load_game");
            SetLabel(optionsLabel, "menu_options");
            SetLabel(quitLabel, "menu_quit");
            SetLabel(seedLabel, "menu_seed_label");
            SetLabel(seedPlaceholder, "menu_seed_placeholder");
            SetLabel(newGameConfirmLabel, "menu_confirm");
            SetLabel(newGameCancelLabel, "menu_cancel");
            SetLabel(loadGameCancelLabel, "menu_cancel");
            SetLabel(noSlotsText, "menu_no_slots");
            SetLabel(loginTitleLabel, "menu_login_title");
            SetLabel(emailPlaceholder, "menu_email");
            SetLabel(passwordPlaceholder, "menu_password");
            SetLabel(nicknamePlaceholder, "menu_nickname");

            // MUIP 버튼 — ButtonManagerBasic.buttonText 로 세팅 (#544 신규 버튼).
            SetButtonLabel(hallOfFameButton, "menu_hall_of_fame");
            SetButtonLabel(loginButton, "menu_login");
            SetButtonLabel(signupButton, "menu_signup");
            SetButtonLabel(authCancelButton, "menu_cancel");
        }

        private static void SetLabel(TMP_Text label, string key)
        {
            if (label != null)
                label.text = Localization.Get(key);
        }

        // MUIP ButtonManagerBasic 면 buttonText(소스)를 바꿔야 런타임 UpdateUI 가 덮어써도 유지됨.
        // 아니면 자식 TMP 직접 세팅. (GlobalNavController.SetLabel 패턴)
        private static void SetButtonLabel(Button button, string key)
        {
            if (button == null)
                return;
            string text = Localization.Get(key);
            var bm = button.GetComponent<ButtonManagerBasic>();
            if (bm != null)
            {
                bm.buttonText = text;
                if (bm.normalText != null)
                    bm.normalText.text = text;
                return;
            }
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
                tmp.text = text;
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

        // ── 계정 (Firebase 인증, #544) ───────────────────────────────
        // 게스트(익명)면 로그인 패널 열기, 계정이면 로그아웃 → 게스트 복귀.
        private void OnAccountActionClicked()
        {
            if (AuthManager.IsSignedIn && !AuthManager.IsAnonymous)
                LogoutToGuest();
            else
                ShowAuthPanel();
        }

        private void ShowAuthPanel()
        {
            mainPanel.SetActive(false);
            if (loginPanel != null)
                loginPanel.SetActive(true);
            SetAuthStatus(string.Empty);
        }

        private void CloseAuthPanel() => ShowMainPanel();

        private async void OnLoginClicked()
        {
            string email = emailInput != null ? emailInput.text : string.Empty;
            string pw = passwordInput != null ? passwordInput.text : string.Empty;
            if (!ValidateAuthInputs(email, pw))
                return;

            SetAuthStatus(Localization.Get("menu_auth_working"));
            try
            {
                await AuthManager.SignInEmailAsync(email.Trim(), pw);
                ShowMainPanel();
            }
            catch (Exception ex)
            {
                SetAuthStatus(Localization.Get("menu_auth_failed", FirstLine(ex.Message)));
            }
        }

        private async void OnSignupClicked()
        {
            string email = emailInput != null ? emailInput.text : string.Empty;
            string pw = passwordInput != null ? passwordInput.text : string.Empty;
            string nick = nicknameInput != null ? nicknameInput.text : string.Empty;
            if (!ValidateAuthInputs(email, pw))
                return;

            SetAuthStatus(Localization.Get("menu_auth_working"));
            try
            {
                await AuthManager.SignUpEmailAsync(email.Trim(), pw);
                if (!string.IsNullOrWhiteSpace(nick))
                    await CloudProfileRepository.SetProfileAsync(nick.Trim());
                ShowMainPanel();
            }
            catch (Exception ex)
            {
                SetAuthStatus(Localization.Get("menu_auth_failed", FirstLine(ex.Message)));
            }
        }

        private async void LogoutToGuest()
        {
            try
            {
                await AuthManager.SignOutToGuestAsync();
            }
            catch (Exception ex)
            {
                GameLog.Log(LogCategory.System, $"로그아웃 실패: {ex.Message}");
            }
        }

        // 현재 계정/게스트 상태를 메인 패널에 반영 (AuthManager.StateChanged 구독).
        private void RefreshAccountStatus()
        {
            bool isAccount = AuthManager.IsSignedIn && !AuthManager.IsAnonymous;
            if (accountStatusText != null)
                accountStatusText.text = isAccount
                    ? AuthManager.Email
                    : Localization.Get("menu_account_guest");
            SetButtonLabel(accountActionButton, isAccount ? "menu_logout" : "menu_login");
        }

        private bool ValidateAuthInputs(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SetAuthStatus(Localization.Get("menu_auth_need_input"));
                return false;
            }
            return true;
        }

        private void SetAuthStatus(string text)
        {
            if (authStatusText == null)
                return;
            authStatusText.text = text;
            authStatusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private static string FirstLine(string s) =>
            string.IsNullOrEmpty(s) ? s : s.Split('\n')[0];

        private void ShowMainPanel()
        {
            mainPanel.SetActive(true);
            newGamePanel.SetActive(false);
            loadGamePanel.SetActive(false);
            if (loginPanel != null)
                loginPanel.SetActive(false);
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
