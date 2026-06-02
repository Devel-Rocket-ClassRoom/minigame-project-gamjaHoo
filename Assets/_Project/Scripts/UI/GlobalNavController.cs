// GlobalNavController.cs
// Stage W (V1.0) — 모든 컨텐츠 씬 공통 글로벌 네비게이션 (TopBar + SideBar).
// 씬별 인스턴스 모델 (DontDestroyOnLoad 아님 — design-decisions.md #58 / v1.0-plan §3.19.5-6).
//   · Awake 에서 Instance set, OnDestroy 에서 clear. 씬당 1개라 중복 guard 불필요.
//   · 상태 비보유 — 매 씬 Start 에서 GameManager.State 읽어 TopBar 갱신 + DayAdvancedEvent 구독.
//   · 제외 씬 (MainMenu / Gacha / ClubSelect) 은 prefab 미포함 → 정리 로직 0.
// prefab / 인스펙터 와이어링은 Sub-C (Unity MCP). 본 파일은 코드 골격 (W.2 / W.4 / W.5 코드부).

using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class GlobalNavController : MonoBehaviour
    {
        // ── 씬 이름 상수 ─────────────────────────────────────────────
        public const string DashboardScene = "DashboardScene";
        public const string SquadScene = "SquadScene";
        public const string TacticScene = "TacticScene";
        public const string LineupScene = "LineupScene";
        public const string TransferScene = "TransferScene";
        public const string ScheduleScene = "ScheduleScene";
        public const string StandingsScene = "StandingsScene";
        public const string FacilityScene = "FacilityScene";
        public const string YouthScene = "YouthScene";
        public const string YouthManagementScene = "YouthManagementScene"; // Stage E (#461)
        public const string MentoringScene = "MentoringScene";
        public const string MainMenuScene = "MainMenuScene";
        public const string OptionsScene = "OptionsScene";

        internal const string PreviousSceneKey = "PreviousScene";

        // SideBar 메뉴 순서 (§3.19.2). index 가 sideBarButtons 와 1:1.
        // §3.19.2 "9개" 는 miscount — 명시 리스트 10개 채택.
        public static readonly string[] SideBarScenes =
        {
            DashboardScene,
            SquadScene,
            TacticScene,
            LineupScene,
            TransferScene,
            ScheduleScene,
            StandingsScene,
            FacilityScene,
            YouthScene,
            MentoringScene,
        };

        /// <summary>현재 씬의 nav 인스턴스. 씬 로드마다 교체 (DDOL 아님).</summary>
        public static GlobalNavController Instance { get; private set; }

        [System.Serializable]
        public class SideBarButton
        {
            public Button button;
            public Image background; // 강조 색 토글용 (선택)
        }

        [Header("TopBar — 텍스트")]
        [SerializeField]
        private TMP_Text dateText;

        [SerializeField]
        private TMP_Text moneyText;

        [SerializeField]
        private TMP_Text tokenText;

        [Header("TopBar — 인박스 배지")]
        [SerializeField]
        private GameObject inboxBadgeRoot; // 안 읽음 0 이면 비활성

        [SerializeField]
        private TMP_Text inboxBadgeText;

        [Header("TopBar — 버튼")]
        [SerializeField]
        private Button backButton;

        [SerializeField]
        private Button inboxButton;

        [SerializeField]
        private Button optionsButton;

        [SerializeField]
        private Button saveButton;

        [SerializeField]
        private Button homeButton;

        [Header("SideBar (index = SideBarScenes 순서)")]
        [SerializeField]
        private SideBarButton[] sideBarButtons;

        [Header("강조 색 (§3.19.2 — 활성 #4A90D9 / 흰 글자)")]
        [SerializeField]
        private Color activeColor = new Color(0.290f, 0.565f, 0.851f); // #4A90D9

        [SerializeField]
        private Color inactiveColor = new Color(0.165f, 0.165f, 0.243f); // #2A2A3E

        [Header("패널")]
        [SerializeField]
        private GameObject inboxPanel; // 우측 슬라이드 (V1.0 정적, 비활성 시작)

        [SerializeField]
        private GlobalSavePanel savePanel; // 저장 모달 (W.5)

        [Header("홈 확인 모달")]
        [SerializeField]
        private GameObject homeConfirmModal;

        [SerializeField]
        private Button homeConfirmYesButton;

        [SerializeField]
        private Button homeConfirmNoButton;

        private void Awake()
        {
            // 씬당 1개 — DontDestroyOnLoad 안 함. 다른 씬에서 온 영구 인스턴스가 없으므로 중복 guard 불필요.
            Instance = this;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            WireButtons();
            if (inboxPanel != null)
                inboxPanel.SetActive(false);
            if (homeConfirmModal != null)
                homeConfirmModal.SetActive(false);

            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            RefreshFromState();
            HighlightCurrentScene();
        }

        private void WireButtons()
        {
            WireOnce(backButton, OnBackClicked);
            WireOnce(inboxButton, ToggleInboxPanel);
            WireOnce(optionsButton, OnOptionsClicked);
            WireOnce(saveButton, OnSaveClicked);
            WireOnce(homeButton, OnHomeClicked);
            WireOnce(homeConfirmYesButton, ConfirmGoHome);
            WireOnce(homeConfirmNoButton, CloseHomeConfirm);

            if (sideBarButtons != null)
            {
                for (int i = 0; i < sideBarButtons.Length && i < SideBarScenes.Length; i++)
                {
                    string target = SideBarScenes[i];
                    WireOnce(sideBarButtons[i]?.button, () => NavigateTo(target));
                }
            }
        }

        private static void WireOnce(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(handler);
        }

        // ── TopBar 갱신 ──────────────────────────────────────────────

        private void OnDayAdvanced(DayAdvancedEvent e) => RefreshFromState();

        /// <summary>날짜 / 자금 / 토큰 / 인박스 배지 전체 갱신.</summary>
        public void RefreshFromState()
        {
            var state = GameManager.Instance?.State;
            if (state == null)
                return;

            if (dateText != null)
                dateText.text = state.currentDate.ToString("yyyy-MM-dd");
            if (tokenText != null)
                tokenText.text = Localization.Get("reroll_token_fmt", state.rerollTokens);

            RefreshMoney();
            RefreshInboxBadge();
        }

        /// <summary>통화 변경 시 자금 표시 재계산 (Stage X.4 에서 호출).</summary>
        public void RefreshMoney()
        {
            if (moneyText == null)
                return;
            var state = GameManager.Instance?.State;
            var club = state?.GetClub(state.userClubId);
            if (club == null)
            {
                moneyText.text = string.Empty;
                return;
            }
            string money = CurrencyFormatter.Format(club.finance.money, OptionsManager.Currency);
            moneyText.text = $"{club.name}  {money}";
        }

        private void RefreshInboxBadge()
        {
            var state = GameManager.Instance?.State;
            int unread = state?.inbox?.Count(i => i != null && !i.isRead) ?? 0;
            if (inboxBadgeText != null)
                inboxBadgeText.text = unread.ToString();
            if (inboxBadgeRoot != null)
                inboxBadgeRoot.SetActive(unread > 0);
        }

        // ── SideBar 강조 (W.4) ───────────────────────────────────────

        /// <summary>활성 씬에 해당하는 SideBar 메뉴 인덱스. SideBar 씬이 아니면 -1.</summary>
        public static int GetMenuIndex(string sceneName)
        {
            for (int i = 0; i < SideBarScenes.Length; i++)
            {
                if (SideBarScenes[i] == sceneName)
                    return i;
            }
            return -1;
        }

        /// <summary>현재 씬 버튼 = 강조 색 + 클릭 무동작 (interactable false).</summary>
        public void HighlightCurrentScene()
        {
            if (sideBarButtons == null)
                return;
            int active = GetMenuIndex(SceneManager.GetActiveScene().name);
            for (int i = 0; i < sideBarButtons.Length; i++)
            {
                var entry = sideBarButtons[i];
                if (entry == null)
                    continue;
                bool isActive = i == active;
                if (entry.background != null)
                    entry.background.color = isActive ? activeColor : inactiveColor;
                if (entry.button != null)
                    entry.button.interactable = !isActive; // 현재 씬 클릭 무동작
            }
        }

        // ── 버튼 핸들러 ──────────────────────────────────────────────

        private void NavigateTo(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;
            // Stage E (#461) E.4: 유스 = 대기 인스펙션 풀 있으면 YouthScene, 평시는 YouthManagementScene.
            if (sceneName == YouthScene)
                sceneName = ResolveYouthScene();
            if (sceneName == SceneManager.GetActiveScene().name)
                return; // 현재 씬 = 무동작
            PlayerPrefs.SetString(PreviousSceneKey, SceneManager.GetActiveScene().name);
            SceneManager.LoadScene(sceneName);
        }

        // 미결정(미서명·미거절) 후보가 남은 인텍이 있으면 인스펙션 화면, 없으면 평시 관리 화면.
        internal static string ResolveYouthScene()
        {
            var club = GameManager.Instance?.UserClub;
            if (club?.intakeHistory != null)
            {
                foreach (var intake in club.intakeHistory)
                {
                    if (intake?.candidatePlayerIds == null)
                        continue;
                    foreach (var cid in intake.candidatePlayerIds)
                    {
                        bool signed = intake.signedPlayerIds?.Contains(cid) ?? false;
                        bool rejected = intake.rejectedPlayerIds?.Contains(cid) ?? false;
                        if (!signed && !rejected)
                            return YouthScene;
                    }
                }
            }
            return YouthManagementScene;
        }

        private void OnBackClicked()
        {
            string prev = PlayerPrefs.GetString(PreviousSceneKey, DashboardScene);
            if (string.IsNullOrEmpty(prev) || prev == SceneManager.GetActiveScene().name)
                prev = DashboardScene;
            SceneManager.LoadScene(prev);
        }

        /// <summary>인박스 우측 슬라이드 패널 토글 (V1.0 정적, 즉시 표시). 열 때 Refresh.</summary>
        public void ToggleInboxPanel()
        {
            if (inboxPanel == null)
                return;
            bool show = !inboxPanel.activeSelf;
            inboxPanel.SetActive(show);
            if (show)
                inboxPanel.GetComponent<InboxPanelController>()?.Refresh();
        }

        private void OnOptionsClicked()
        {
            // OptionsScene 스택 진입 — 직전 씬을 PreviousScene 으로 (Stage X 에서 [뒤로] 활용).
            PlayerPrefs.SetString(PreviousSceneKey, SceneManager.GetActiveScene().name);
            SceneManager.LoadScene(OptionsScene);
        }

        private void OnSaveClicked()
        {
            if (savePanel != null)
                savePanel.Show();
        }

        private void OnHomeClicked()
        {
            if (homeConfirmModal != null)
                homeConfirmModal.SetActive(true);
        }

        private void ConfirmGoHome() => SceneManager.LoadScene(MainMenuScene);

        private void CloseHomeConfirm()
        {
            if (homeConfirmModal != null)
                homeConfirmModal.SetActive(false);
        }
    }
}
