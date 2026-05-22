// Task 13.4 (Issue #49) — 메인 대시보드.
// 현재 날짜 / 다음 경기 / Continue + 사이드 메뉴.
// Continue → GameLoop.ContinueUntilStop → 다음 정지 이벤트까지 진행.
// DayAdvancedEvent 구독으로 날짜 실시간 갱신.
// Issue #165: 저장 슬롯 리스트 + 메인 메뉴 복귀 버튼 추가
// (Save→MainMenu→LoadGame V0.1 테스트 흐름 활성화).

using System.Linq;
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
    public class DashboardController : MonoBehaviour
    {
        private const string SquadScene = "SquadScene";
        private const string YouthScene = "YouthScene";
        private const string TransferScene = "TransferScene";
        private const string ScheduleScene = "ScheduleScene";
        private const string FacilityScene = "FacilityScene";
        private const string StandingsScene = "StandingsScene";
        private const string MainMenuScene = "MainMenuScene";

        [Header("요약 정보")]
        [SerializeField]
        private TMP_Text dateText;

        [SerializeField]
        private TMP_Text nextMatchText;

        [SerializeField]
        private TMP_Text tokenText;

        [Header("Continue")]
        [SerializeField]
        private Button continueButton;

        [Header("저장 패널")]
        [SerializeField]
        private GameObject savePanel;

        [SerializeField]
        private Transform saveSlotListParent;

        [SerializeField]
        private GameObject saveSlotItemPrefab;

        [SerializeField]
        private TMP_Text noSaveSlotsText;

        [Header("데이터")]
        [SerializeField]
        private GameBalanceSO balance;

        private void OnEnable()
        {
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Subscribe<YouthIntakeAvailableEvent>(OnYouthIntakeAvailable);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Unsubscribe<YouthIntakeAvailableEvent>(OnYouthIntakeAvailable);
        }

        private void Start()
        {
            if (savePanel != null)
                savePanel.SetActive(false);
            RefreshInfo();
        }

        public void OnContinueClicked()
        {
            continueButton.interactable = false;
            var state = GameManager.Instance.State;
            GameLoop.ContinueUntilStop(state, balance);
            RefreshInfo();
            continueButton.interactable = true;
        }

        public void OnSquadClicked() => SceneManager.LoadScene(SquadScene);

        public void OnYouthClicked() => SceneManager.LoadScene(YouthScene);

        public void OnTransferClicked() => SceneManager.LoadScene(TransferScene);

        public void OnScheduleClicked() => SceneManager.LoadScene(ScheduleScene);

        public void OnStandingsClicked() => SceneManager.LoadScene(StandingsScene);

        public void OnFacilityClicked() => SceneManager.LoadScene(FacilityScene);

        public void OnMainMenuClicked() => SceneManager.LoadScene(MainMenuScene);

        public void OnSaveClicked()
        {
            if (savePanel == null)
                return;
            savePanel.SetActive(true);
            PopulateSaveSlotList();
        }

        public void OnCloseSavePanelClicked()
        {
            if (savePanel != null)
                savePanel.SetActive(false);
        }

        public void OnNewSlotClicked()
        {
            SaveToSlot(GenerateAutoSlotName());
        }

        private void PopulateSaveSlotList()
        {
            if (saveSlotListParent == null || saveSlotItemPrefab == null)
                return;

            foreach (Transform child in saveSlotListParent)
                Destroy(child.gameObject);

            var slots = SaveSystem.ListSlots();

            if (noSaveSlotsText != null)
                noSaveSlotsText.gameObject.SetActive(slots.Count == 0);

            foreach (var meta in slots)
            {
                var item = Instantiate(saveSlotItemPrefab, saveSlotListParent);
                item.GetComponent<SaveSlotItem>().Setup(meta, SaveToSlot);
            }
        }

        private void SaveToSlot(string slotName)
        {
            var state = GameManager.Instance?.State;
            if (state == null)
                return;

            SaveSystem.Save(state, slotName);
            GameLog.Log(LogCategory.System, $"슬롯 저장: {slotName}");

            if (savePanel != null)
                savePanel.SetActive(false);
        }

        private string GenerateAutoSlotName()
        {
            var state = GameManager.Instance?.State;
            var clubName = state?.GetClub(state.userClubId)?.name ?? "user";
            var safeClubName = SanitizeSlotName(clubName);
            var timestamp = System.DateTime.Now.ToString("yyMMdd_HHmm");
            return $"slot_{safeClubName}_{timestamp}";
        }

        private static string SanitizeSlotName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        private void OnDayAdvanced(DayAdvancedEvent e)
        {
            RefreshInfo();
        }

        private void OnYouthIntakeAvailable(YouthIntakeAvailableEvent e)
        {
            if (e.clubId == GameManager.Instance?.State?.userClubId)
                SceneManager.LoadScene(YouthScene);
        }

        private void RefreshInfo()
        {
            var state = GameManager.Instance.State;
            if (state == null)
                return;

            dateText.text = state.currentDate.ToString("yyyy-MM-dd");
            tokenText.text = $"리롤 토큰  {state.rerollTokens}";
            nextMatchText.text = GetNextMatchText(state);
        }

        private string GetNextMatchText(GameState state)
        {
            var nextMatch = state
                .leagues.SelectMany(l => l.schedule)
                .Where(m =>
                    m.result == null
                    && (m.homeClubId == state.userClubId || m.awayClubId == state.userClubId)
                    && m.date >= state.currentDate
                )
                .OrderBy(m => m.date)
                .FirstOrDefault();

            if (nextMatch == null)
                return "다음 경기 없음";

            bool isHome = nextMatch.homeClubId == state.userClubId;
            var opponentId = isHome ? nextMatch.awayClubId : nextMatch.homeClubId;
            var opponent = state.GetClub(opponentId);
            var homeAway = isHome ? "홈" : "원정";
            return $"{nextMatch.date:MM/dd}  {opponent?.name ?? "?"}  ({homeAway})";
        }
    }
}
