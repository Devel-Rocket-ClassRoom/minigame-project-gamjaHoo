// Task 13.4 (Issue #49) — 메인 대시보드.
// 현재 날짜 / 다음 경기 / Continue + 사이드 메뉴.
// Continue → GameLoop.ContinueUntilStop → 다음 정지 이벤트까지 진행.
// DayAdvancedEvent 구독으로 날짜 실시간 갱신.
// Issue #165: 저장 슬롯 리스트 + 메인 메뉴 복귀 버튼 추가
// (Save→MainMenu→LoadGame V0.1 테스트 흐름 활성화).
// V0.5 G.2 Sub-B (#300): 인박스 패널 — Promise* / TransferRequest 5 이벤트 구독, in-memory 메시지 리스트.

using System.Collections.Generic;
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
        private const string TacticLineupScene = "TacticLineupScene"; // 전술/라인업 통합 (구 TacticScene/LineupScene 폐기, H.4)
        private const string MainMenuScene = "MainMenuScene";
        private const string MentoringScene = "MentoringScene";
        private const string SeasonSummaryScene = "SeasonSummaryScene";
        private const string MatchTextScene = "MatchTextScene";
        private const string MatchPreviewScene = "MatchPreviewScene";
        internal const string SelectedMatchIdKey = "SelectedMatchId";

        [Header("요약 정보")]
[SerializeField]
        private TMP_Text dateText;

        [SerializeField]
        private TMP_Text nextMatchText;

        [SerializeField]
        private UnityEngine.UI.Image opponentCrest; // Stage AD — 다음 상대 구단 크레스트 (미배선/미생성 시 자동 숨김)

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

        [Header("인박스 (V0.5 G.2 Sub-B)")]
        [SerializeField]
        private Transform inboxListParent;

        [SerializeField]
        private GameObject inboxItemPrefab;

        [SerializeField]
        private int inboxMaxItems = 10;

        [Header("이적 요청 다이얼로그 (V0.5 G.4)")]
        [SerializeField]
        private TransferRequestDialogController transferRequestDialog;

        [Header("보드 약속 모달 (V0.5 M.5)")]
        [SerializeField]
        private BoardMeetingController boardMeetingPanel;

        [Header("다음 매치 상세 (N.2)")]
        [SerializeField]
        private TMP_Text opponentFormText;

        [SerializeField]
        private TMP_Text lastResultText;

        [SerializeField]
        private TMP_Text h2hText;

        [Header("사기 / 부상 요약 (N.2)")]
        [SerializeField]
        private TMP_Text moraleWarningText;

        [SerializeField]
        private TMP_Text injuryText;

        private void OnEnable()
        {
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Subscribe<PromiseCreatedEvent>(OnPromiseCreated);
            EventBus.Subscribe<PromiseFulfilledEvent>(OnPromiseFulfilled);
            EventBus.Subscribe<PromiseBrokenEvent>(OnPromiseBroken);
            EventBus.Subscribe<PromiseDeadlineApproachingEvent>(OnPromiseDeadlineApproaching);
            EventBus.Subscribe<TransferRequestEvent>(OnTransferRequest);
            EventBus.Subscribe<YouthPromotionSuggestedEvent>(OnYouthPromotion);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Unsubscribe<PromiseCreatedEvent>(OnPromiseCreated);
            EventBus.Unsubscribe<PromiseFulfilledEvent>(OnPromiseFulfilled);
            EventBus.Unsubscribe<PromiseBrokenEvent>(OnPromiseBroken);
            EventBus.Unsubscribe<PromiseDeadlineApproachingEvent>(OnPromiseDeadlineApproaching);
            EventBus.Unsubscribe<TransferRequestEvent>(OnTransferRequest);
            EventBus.Unsubscribe<YouthPromotionSuggestedEvent>(OnYouthPromotion);
        }

        private void Start()
        {
            if (savePanel != null)
                savePanel.SetActive(false);
            RefreshInfo();
            CheckBoardMeeting();
        }

        private void CheckBoardMeeting()
        {
            if (boardMeetingPanel == null)
                return;
            var state = GameManager.Instance?.State;
            var userClub = state?.GetClub(state.userClubId);
            if (userClub?.season == null)
                return;
            foreach (var promise in userClub.season.boardPromises)
            {
                if (promise.status == FMLite.Domain.BoardPromiseStatus.PendingReview)
                {
                    boardMeetingPanel.Show(promise, state);
                    return;
                }
            }
        }

        public void OnContinueClicked()
        {
            continueButton.interactable = false;
            var state = GameManager.Instance.State;

            // H.4: 오늘 미플레이 유저 매치가 있으면 진행 막고 MatchPreviewScene 으로 (매치일 스킵 방지).
            if (TryRouteToMatchPreview(state))
                return;

            GameLoop.ContinueUntilStop(state, balance);

            // 시즌 종료일(5/15) 도달 시 요약 화면으로 전환
            if (
                balance != null
                && state.currentDate.Month == balance.seasonEndMonth
                && state.currentDate.Day == balance.seasonEndDay
            )
            {
                SceneManager.LoadScene(SeasonSummaryScene);
                return;
            }

            // H.4: 진행 후 멈춘 날이 유저 매치일(미플레이)이면 경기 직전 점검 화면으로.
            if (TryRouteToMatchPreview(state))
                return;

            // B.2 (design-decisions #66): CounterOffer 강제 NegotiationScene 전환 폐기.
            // InboxRouter 가 InboxItem(Transfer/RequiresAction, 기한 7일, OpenScene:NegotiationScene) 으로 흡수 —
            // 유저가 인박스에서 클릭해 처리. Continue 시 강제 전환 X.

            RefreshInfo();
            // #465: Continue 가 알림 도착일에 멈춰도 그날 생성된 인박스 아이템이 배지에 즉시 반영되도록
            // GlobalNav 갱신 (DayAdvancedEvent 는 인박스 생성 이전 단계라 도착 당일 배지를 놓침).
            GlobalNavController.Instance?.RefreshFromState();
            CheckBoardMeeting();
            continueButton.interactable = true;
        }

        public void OnSquadClicked() => SceneManager.LoadScene(SquadScene);

        // Stage E (#461) E.4: 대기 인스펙션 풀이면 YouthScene, 평시는 YouthManagementScene.
        public void OnYouthClicked() =>
            SceneManager.LoadScene(GlobalNavController.ResolveYouthScene());

        public void OnTransferClicked() => SceneManager.LoadScene(TransferScene);

        public void OnScheduleClicked() => SceneManager.LoadScene(ScheduleScene);

        public void OnStandingsClicked() => SceneManager.LoadScene(StandingsScene);

        public void OnFacilityClicked() => SceneManager.LoadScene(FacilityScene);

        // 구 전술/라인업 분리 씬 폐기 → 통합 TacticLineupScene 으로 (H.4). 두 핸들러 동일 타깃.
        public void OnTacticClicked() => SceneManager.LoadScene(TacticLineupScene);

        public void OnLineupClicked() => SceneManager.LoadScene(TacticLineupScene);

        public void OnMentoringClicked() => SceneManager.LoadScene(MentoringScene);

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

        // B.2 (design-decisions #66 Q3): YouthIntakeAvailableEvent 강제 YouthScene 전환 폐기.
        // InboxRouter 가 InboxItem(Youth/RequiresAction, OpenScene:YouthScene) 으로 흡수.

        // ── 인박스 (V0.5 G.2 Sub-B) ──────────────────────────────────

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

            // V0.5 G.4 — Q9 자동 트리거 + 유저 승인 패턴. 자기 구단 선수만 dialog 표시.
            if (
                transferRequestDialog != null
                && player != null
                && player.currentClubId == state?.userClubId
            )
            {
                transferRequestDialog.Show(e.playerId);
            }
        }

        private void OnYouthPromotion(YouthPromotionSuggestedEvent e)
        {
            var state = GameManager.Instance?.State;
            if (e.clubId != state?.userClubId)
                return;
            var player = state.GetPlayer(e.playerId);
            if (player == null)
                return;
            string playerName = $"{player.info.firstName} {player.info.lastName}";
            var today = state.currentDate;
            int age = today.Year - player.info.birthDate.Year;
            if (
                today.Month < player.info.birthDate.Month
                || (today.Month == player.info.birthDate.Month && today.Day < player.info.birthDate.Day)
            )
                age--;
            PushInbox(
                Localization.Get("inbox_youth_promotion_fmt", playerName, age, player.currentAbility)
            );
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
            var nextMatch = FindNextUserMatch(state);
            nextMatchText.text = nextMatch != null
                ? FormatNextMatchText(state, nextMatch)
                : Localization.Get("no_next_match");
            var nextOpponent = nextMatch != null
                ? state.GetClub(
                    nextMatch.homeClubId == state.userClubId
                        ? nextMatch.awayClubId
                        : nextMatch.homeClubId
                )
                : null;
            CrestProvider.ApplyClubCrest(opponentCrest, nextOpponent?.name);
            RefreshMatchDetail(state, nextMatch);
            RefreshSquadAlerts(state);
        }

        private static string FormatNextMatchText(GameState state, Match m)
        {
            bool isHome = m.homeClubId == state.userClubId;
            var opponent = state.GetClub(isHome ? m.awayClubId : m.homeClubId);
            var homeAway = Localization.Get(isHome ? "home" : "away");
            return $"{m.date:MM/dd}  {opponent?.name ?? "?"}  ({homeAway})";
        }

        private static Match FindNextUserMatch(GameState state)
        {
            return state
                .leagues.SelectMany(l => l.schedule)
                .Where(m =>
                    m.result == null
                    && (m.homeClubId == state.userClubId || m.awayClubId == state.userClubId)
                    && m.date >= state.currentDate
                )
                .OrderBy(m => m.date)
                .FirstOrDefault();
        }

        // ── 다음 매치 상세 (N.2) ─────────────────────────────────────

        private void RefreshMatchDetail(GameState state, Match nextMatch)
        {
            if (nextMatch == null)
            {
                // 다음 매치 없음 — 빈 텍스트로 두지 않고 행 자체를 접는다 (레이아웃 빈 줄 방지)
                if (opponentFormText != null) opponentFormText.gameObject.SetActive(false);
                if (lastResultText != null) lastResultText.gameObject.SetActive(false);
                if (h2hText != null) h2hText.gameObject.SetActive(false);
                return;
            }
            bool isHome = nextMatch.homeClubId == state.userClubId;
            int opponentId = isHome ? nextMatch.awayClubId : nextMatch.homeClubId;

            if (opponentFormText != null)
            {
                opponentFormText.gameObject.SetActive(true);
                opponentFormText.text = Localization.Get(
                    "dashboard_form_fmt",
                    OpponentForm(state, opponentId)
                );
            }
            if (lastResultText != null)
            {
                lastResultText.gameObject.SetActive(true);
                lastResultText.text = Localization.Get(
                    "dashboard_last_result_fmt",
                    LastResultVs(state, opponentId)
                );
            }
            if (h2hText != null)
            {
                h2hText.gameObject.SetActive(true);
                h2hText.text = Localization.Get("dashboard_h2h_fmt", H2HRecord(state, opponentId));
            }
        }

        private static string OpponentForm(GameState state, int opponentId)
        {
            var recent = state
                .leagues.SelectMany(l => l.schedule)
                .Where(m =>
                    m.result != null
                    && (m.homeClubId == opponentId || m.awayClubId == opponentId)
                )
                .OrderByDescending(m => m.date)
                .Take(5)
                .ToList();

            if (recent.Count == 0)
                return Localization.Get("dashboard_no_record");

            var symbols = recent
                .Select(m =>
                {
                    bool oppIsHome = m.homeClubId == opponentId;
                    int scored = oppIsHome ? m.result.homeScore : m.result.awayScore;
                    int conceded = oppIsHome ? m.result.awayScore : m.result.homeScore;
                    return scored > conceded ? "W" : scored == conceded ? "D" : "L";
                })
                .Reverse();
            return string.Join(" ", symbols);
        }

        private string LastResultVs(GameState state, int opponentId)
        {
            var match = state
                .leagues.SelectMany(l => l.schedule)
                .Where(m =>
                    m.result != null
                    && (
                        (m.homeClubId == state.userClubId && m.awayClubId == opponentId)
                        || (m.awayClubId == state.userClubId && m.homeClubId == opponentId)
                    )
                )
                .OrderByDescending(m => m.date)
                .FirstOrDefault();

            if (match == null)
                return Localization.Get("dashboard_no_record");

            bool userIsHome = match.homeClubId == state.userClubId;
            int us = userIsHome ? match.result.homeScore : match.result.awayScore;
            int them = userIsHome ? match.result.awayScore : match.result.homeScore;
            string wdl = us > them ? "W" : us == them ? "D" : "L";
            return $"{us}-{them} ({wdl})";
        }

        private string H2HRecord(GameState state, int opponentId)
        {
            var matches = state
                .leagues.SelectMany(l => l.schedule)
                .Where(m =>
                    m.result != null
                    && (
                        (m.homeClubId == state.userClubId && m.awayClubId == opponentId)
                        || (m.awayClubId == state.userClubId && m.homeClubId == opponentId)
                    )
                )
                .ToList();

            if (matches.Count == 0)
                return Localization.Get("dashboard_no_record");

            int w = 0,
                d = 0,
                l = 0;
            foreach (var m in matches)
            {
                bool userIsHome = m.homeClubId == state.userClubId;
                int us = userIsHome ? m.result.homeScore : m.result.awayScore;
                int them = userIsHome ? m.result.awayScore : m.result.homeScore;
                if (us > them) w++;
                else if (us == them) d++;
                else l++;
            }
            return $"{w}W {d}D {l}L";
        }

        // ── 사기 / 부상 요약 (N.2) ──────────────────────────────────

        private void RefreshSquadAlerts(GameState state)
        {
            var userClub = state.GetClub(state.userClubId);
            if (userClub == null)
            {
                if (moraleWarningText != null) moraleWarningText.gameObject.SetActive(false);
                if (injuryText != null) injuryText.gameObject.SetActive(false);
                return;
            }

            var unhappy = new List<string>();
            var unavailable = new List<string>();

            foreach (int pid in userClub.seniorSquadIds)
            {
                var p = state.GetPlayer(pid);
                if (p?.state == null)
                    continue;

                if (p.state.morale < 40)
                    unhappy.Add(p.info?.lastName ?? $"id={pid}");

                bool injured = p.state.injury != null && p.state.injury.injuryTypeId != -1;
                bool suspended = p.state.suspendedMatches > 0;
                if (injured || suspended)
                    unavailable.Add(p.info?.lastName ?? $"id={pid}");
            }

            if (moraleWarningText != null)
            {
                bool has = unhappy.Count > 0;
                moraleWarningText.gameObject.SetActive(has);
                if (has)
                    moraleWarningText.text = Localization.Get(
                        "dashboard_morale_warning_fmt",
                        FormatNameList(unhappy)
                    );
            }

            if (injuryText != null)
            {
                bool has = unavailable.Count > 0;
                injuryText.gameObject.SetActive(has);
                if (has)
                    injuryText.text = Localization.Get(
                        "dashboard_injury_fmt",
                        FormatNameList(unavailable)
                    );
            }
        }

        private static string FormatNameList(List<string> names) =>
            names.Count <= 3
                ? string.Join(", ", names)
                : Localization.Get("dashboard_count_fmt", names.Count);

        // H.4: 오늘 미플레이(result==null) 유저 매치가 있으면 MatchPreviewScene 으로 라우팅하고 true.
        private bool TryRouteToMatchPreview(GameState state)
        {
            var m = FindTodayUserMatch(state);
            if (m != null && m.result == null)
            {
                PlayerPrefs.SetInt(SelectedMatchIdKey, m.id);
                SceneManager.LoadScene(MatchPreviewScene);
                return true;
            }
            return false;
        }

        // N.3: 오늘 날짜에 유저 클럽 매치가 있으면 반환
        private static Match FindTodayUserMatch(GameState state)
        {
            var today = state.currentDate.Date;
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                {
                    if (m.date.Date != today)
                        continue;
                    if (m.homeClubId == state.userClubId || m.awayClubId == state.userClubId)
                        return m;
                }
            }
            return null;
        }
    }
}
