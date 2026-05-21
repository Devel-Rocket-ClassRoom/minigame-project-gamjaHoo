// Task 13.4 (Issue #49) — 메인 대시보드.
// 현재 날짜 / 다음 경기 / Continue + 사이드 메뉴.
// Continue → GameLoop.ContinueUntilStop → 다음 정지 이벤트까지 진행.
// DayAdvancedEvent 구독으로 날짜 실시간 갱신.

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
    public class DashboardController : MonoBehaviour
    {
        private const string SquadScene = "SquadScene";
        private const string YouthScene = "YouthScene";
        private const string TransferScene = "TransferScene";
        private const string ScheduleScene = "ScheduleScene";
        private const string FacilityScene = "FacilityScene";

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

        public void OnFacilityClicked() => SceneManager.LoadScene(FacilityScene);

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
