// Task 13.4 (Issue #49) — 메인 대시보드.
// 현재 날짜 / 다음 경기 / Continue + 사이드 메뉴.
// Continue → GameLoop.ContinueUntilStop → 다음 정지 이벤트까지 진행.
// DayAdvancedEvent 구독으로 날짜 실시간 갱신.
// Issue #165: 저장 슬롯 리스트 + 메인 메뉴 복귀 버튼 추가
// (Save→MainMenu→LoadGame V0.1 테스트 흐름 활성화).
// V1.0 G.2 Sub-B (#300): 인박스 패널 — Promise* / TransferRequest 5 이벤트 구독, in-memory 메시지 리스트.

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
        private const string TacticScene = "TacticScene";
        private const string LineupScene = "LineupScene";
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

        [Header("인박스 (V1.0 G.2 Sub-B)")]
        [SerializeField]
        private Transform inboxListParent;

        [SerializeField]
        private GameObject inboxItemPrefab;

        [SerializeField]
        private int inboxMaxItems = 10;

        [Header("이적 요청 다이얼로그 (V1.0 G.4)")]
        [SerializeField]
        private TransferRequestDialogController transferRequestDialog;

        private void OnEnable()
        {
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Subscribe<YouthIntakeAvailableEvent>(OnYouthIntakeAvailable);
            EventBus.Subscribe<PromiseCreatedEvent>(OnPromiseCreated);
            EventBus.Subscribe<PromiseFulfilledEvent>(OnPromiseFulfilled);
            EventBus.Subscribe<PromiseBrokenEvent>(OnPromiseBroken);
            EventBus.Subscribe<PromiseDeadlineApproachingEvent>(OnPromiseDeadlineApproaching);
            EventBus.Subscribe<TransferRequestEvent>(OnTransferRequest);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Unsubscribe<YouthIntakeAvailableEvent>(OnYouthIntakeAvailable);
            EventBus.Unsubscribe<PromiseCreatedEvent>(OnPromiseCreated);
            EventBus.Unsubscribe<PromiseFulfilledEvent>(OnPromiseFulfilled);
            EventBus.Unsubscribe<PromiseBrokenEvent>(OnPromiseBroken);
            EventBus.Unsubscribe<PromiseDeadlineApproachingEvent>(OnPromiseDeadlineApproaching);
            EventBus.Unsubscribe<TransferRequestEvent>(OnTransferRequest);
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

        public void OnTacticClicked() => SceneManager.LoadScene(TacticScene);

        public void OnLineupClicked() => SceneManager.LoadScene(LineupScene);

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

        // ── 인박스 (V1.0 G.2 Sub-B) ──────────────────────────────────

        private void OnPromiseCreated(PromiseCreatedEvent e) =>
            PushInbox(FormatPromise("inbox_promise_created_fmt", e.promiseId));

        private void OnPromiseFulfilled(PromiseFulfilledEvent e) =>
            PushInbox(FormatPromise("inbox_promise_fulfilled_fmt", e.promiseId));

        private void OnPromiseBroken(PromiseBrokenEvent e) =>
            PushInbox(FormatPromise("inbox_promise_broken_fmt", e.promiseId));

        private void OnPromiseDeadlineApproaching(PromiseDeadlineApproachingEvent e) =>
            PushInbox(FormatPromiseApproaching(e.promiseId, e.daysRemaining));

        private void OnTransferRequest(TransferRequestEvent e)
        {
            var state = GameManager.Instance?.State;
            var player = state?.GetPlayer(e.playerId);
            string playerName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={e.playerId}";
            PushInbox(Localization.Get("inbox_transfer_request_fmt", playerName));

            // V1.0 G.4 — Q9 자동 트리거 + 유저 승인 패턴. 자기 구단 선수만 dialog 표시.
            if (
                transferRequestDialog != null
                && player != null
                && player.currentClubId == state?.userClubId
            )
            {
                transferRequestDialog.Show(e.playerId);
            }
        }

        private string FormatPromise(string key, int promiseId)
        {
            var state = GameManager.Instance?.State;
            var promise = state?.activePromises?.Find(p => p.id == promiseId);
            string playerName = "?";
            string typeLabel = "?";
            if (promise != null)
            {
                var player = state.GetPlayer(promise.playerId);
                playerName =
                    player?.info != null
                        ? $"{player.info.firstName} {player.info.lastName}"
                        : $"id={promise.playerId}";
                typeLabel = Localization.Get(PromiseTypeKey(promise.type));
            }
            return Localization.Get(key, playerName, typeLabel);
        }

        private string FormatPromiseApproaching(int promiseId, int daysRemaining)
        {
            var state = GameManager.Instance?.State;
            var promise = state?.activePromises?.Find(p => p.id == promiseId);
            string playerName = "?";
            string typeLabel = "?";
            if (promise != null)
            {
                var player = state.GetPlayer(promise.playerId);
                playerName =
                    player?.info != null
                        ? $"{player.info.firstName} {player.info.lastName}"
                        : $"id={promise.playerId}";
                typeLabel = Localization.Get(PromiseTypeKey(promise.type));
            }
            return Localization.Get(
                "inbox_promise_approaching_fmt",
                playerName,
                typeLabel,
                daysRemaining
            );
        }

        private static string PromiseTypeKey(PromiseType type) =>
            type switch
            {
                PromiseType.PlaytimeAgreement => "promise_type_playtime",
                PromiseType.TransferIn => "promise_type_transfer_in",
                PromiseType.Renewal => "promise_type_renewal",
                PromiseType.TransferOut => "promise_type_transfer_out",
                _ => "promise_type_playtime",
            };

        private void PushInbox(string message)
        {
            if (inboxListParent == null || inboxItemPrefab == null)
                return;

            var item = Instantiate(inboxItemPrefab, inboxListParent);
            item.transform.SetSiblingIndex(0); // 최신 메시지를 맨 위로
            var inboxItem = item.GetComponent<InboxItem>();
            if (inboxItem != null)
                inboxItem.Setup(message);

            // 초과 시 가장 오래된 메시지 제거 (in-memory 단순 정책)
            while (inboxListParent.childCount > inboxMaxItems)
            {
                var last = inboxListParent.GetChild(inboxListParent.childCount - 1);
                Destroy(last.gameObject);
            }
        }

        private void RefreshInfo()
        {
            var state = GameManager.Instance.State;
            if (state == null)
                return;

            dateText.text = state.currentDate.ToString("yyyy-MM-dd");
            tokenText.text = Localization.Get("reroll_token_fmt", state.rerollTokens);
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
                return Localization.Get("no_next_match");

            bool isHome = nextMatch.homeClubId == state.userClubId;
            var opponentId = isHome ? nextMatch.awayClubId : nextMatch.homeClubId;
            var opponent = state.GetClub(opponentId);
            var homeAway = Localization.Get(isHome ? "home" : "away");
            return $"{nextMatch.date:MM/dd}  {opponent?.name ?? "?"}  ({homeAway})";
        }
    }
}
