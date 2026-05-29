// MatchTextController.cs
// V0.5 N.3 — 매치 텍스트 이벤트 화면.
// PlayerPrefs("SelectedMatchId") 로 매치 ID 수신.
// collectEvents=true 로 시뮬된 이벤트를 분 단위로 표시.

using System.Collections;
using System.Collections.Generic;
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
    public class MatchTextController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";
        private const float BaseDelay = 0.8f;

        [Header("헤더")]
        [SerializeField]
        private TMP_Text homeName;

        [SerializeField]
        private TMP_Text awayName;

        [SerializeField]
        private TMP_Text scoreText;

        [SerializeField]
        private TMP_Text minuteText;

        [Header("이벤트 스크롤")]
        [SerializeField]
        private ScrollRect eventScrollRect;

        [SerializeField]
        private Transform eventListContent;

        [SerializeField]
        private GameObject eventItemPrefab;

        [Header("속도 버튼")]
        [SerializeField]
        private Button speedX1Button;

        [SerializeField]
        private Button speedX2Button;

        [SerializeField]
        private Button speedX4Button;

        [SerializeField]
        private Button skipButton;

        [Header("결과 패널")]
        [SerializeField]
        private GameObject resultPanel;

        [SerializeField]
        private TMP_Text resultScoreText;

        [SerializeField]
        private TMP_Text resultScorersText;

        [SerializeField]
        private TMP_Text resultRatingsText;

        [SerializeField]
        private Button resultBackButton;

        private GameState _state;
        private Match _match;
        private int _homeScore;
        private int _awayScore;
        private float _speedMultiplier = 1f;
        private bool _isSkipped;
        private Coroutine _playCoroutine;

        private void Awake()
        {
            if (speedX1Button != null)
                speedX1Button.onClick.AddListener(() => SetSpeed(1f));
            if (speedX2Button != null)
                speedX2Button.onClick.AddListener(() => SetSpeed(2f));
            if (speedX4Button != null)
                speedX4Button.onClick.AddListener(() => SetSpeed(4f));
            if (skipButton != null)
                skipButton.onClick.AddListener(OnSkip);
            if (resultBackButton != null)
                resultBackButton.onClick.AddListener(OnBackClicked);

            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            int matchId = PlayerPrefs.GetInt(DashboardController.SelectedMatchIdKey, -1);
            _match = FindMatch(matchId);

            if (_match?.result == null)
            {
                ShowResultPanel();
                return;
            }

            SetupHeader();
            _playCoroutine = StartCoroutine(PlayEvents());
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        // ── 속도 제어 ─────────────────────────────────────────────────

        private void SetSpeed(float multiplier)
        {
            _speedMultiplier = multiplier;
        }

        private void OnSkip()
        {
            _isSkipped = true;
        }

        // ── 이벤트 재생 ───────────────────────────────────────────────

        private IEnumerator PlayEvents()
        {
            var events = _match.events;

            for (int i = 0; i < events.Count; i++)
            {
                if (_isSkipped)
                    break;

                var ev = events[i];
                SpawnEventItem(ev);
                UpdateScore(ev);
                UpdateMinute(ev.minute);
                ScrollToBottom();

                yield return new WaitForSeconds(BaseDelay / _speedMultiplier);
            }

            // 스킵 시 남은 이벤트 즉시 표시
            if (_isSkipped)
            {
                int startIdx = eventListContent != null ? eventListContent.childCount : 0;
                for (int i = startIdx; i < events.Count; i++)
                {
                    var ev = events[i];
                    SpawnEventItem(ev);
                    UpdateScore(ev);
                }
                UpdateMinute(events.Count > 0 ? events[events.Count - 1].minute : 90);
                ScrollToBottom();
            }

            ShowResultPanel();
        }

        private void SpawnEventItem(MatchEvent ev)
        {
            if (eventItemPrefab == null || eventListContent == null)
                return;

            var go = Instantiate(eventItemPrefab, eventListContent);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = FormatEvent(ev);
        }

        private void UpdateScore(MatchEvent ev)
        {
            if (
                ev.type == MatchEventType.Goal
                || ev.type == MatchEventType.PenaltyGoal
                || ev.type == MatchEventType.PenaltyShootoutKick
            )
            {
                bool isGoal =
                    ev.type == MatchEventType.Goal || ev.type == MatchEventType.PenaltyGoal;
                if (!isGoal)
                    return;

                if (ev.side == 0)
                    _homeScore++;
                else
                    _awayScore++;

                if (scoreText != null)
                    scoreText.text = $"{_homeScore} : {_awayScore}";
            }
        }

        private void UpdateMinute(int minute)
        {
            if (minuteText != null)
                minuteText.text = $"{minute}'";
        }

        private void ScrollToBottom()
        {
            if (eventScrollRect != null)
                Canvas.ForceUpdateCanvases();
            if (eventScrollRect != null)
                eventScrollRect.verticalNormalizedPosition = 0f;
        }

        // ── 결과 패널 ─────────────────────────────────────────────────

        private void ShowResultPanel()
        {
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);

            if (resultPanel != null)
                resultPanel.SetActive(true);

            if (_match?.result == null)
                return;

            var result = _match.result;

            if (resultScoreText != null)
            {
                var home = _state.GetClub(_match.homeClubId)?.name ?? "?";
                var away = _state.GetClub(_match.awayClubId)?.name ?? "?";
                resultScoreText.text = $"{home}  {result.homeScore} : {result.awayScore}  {away}";
            }

            if (resultScorersText != null)
                resultScorersText.text = BuildScorersText(result);

            if (resultRatingsText != null)
                resultRatingsText.text = BuildRatingsText(result);
        }

        private string BuildScorersText(MatchResult result)
        {
            var lines = new List<string>();
            foreach (var ev in _match.events)
            {
                if (ev.type != MatchEventType.Goal && ev.type != MatchEventType.PenaltyGoal)
                    continue;
                string playerName = GetPlayerName(ev.actorPlayerId);
                lines.Add($"{ev.minute}' {playerName}");
            }
            return lines.Count > 0
                ? string.Join("\n", lines)
                : Localization.Get("match_text_no_goals");
        }

        private string BuildRatingsText(MatchResult result)
        {
            var userSquadIds = _state.GetClub(_state.userClubId)?.seniorSquadIds;
            if (userSquadIds == null)
                return "";

            var sorted = result
                .playerStats.Where(ps => userSquadIds.Contains(ps.playerId))
                .OrderByDescending(ps => ps.rating)
                .Take(5)
                .ToList();

            var lines = new List<string>();
            foreach (var ps in sorted)
            {
                string name = GetPlayerName(ps.playerId);
                lines.Add(Localization.Get("match_text_rating_fmt", name, ps.rating));
            }
            return string.Join("\n", lines);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private void SetupHeader()
        {
            if (_match == null)
                return;

            var home = _state.GetClub(_match.homeClubId)?.name ?? "?";
            var away = _state.GetClub(_match.awayClubId)?.name ?? "?";

            if (homeName != null)
                homeName.text = home;
            if (awayName != null)
                awayName.text = away;
            if (scoreText != null)
                scoreText.text = "0 : 0";
            if (minuteText != null)
                minuteText.text = "0'";
        }

        private Match FindMatch(int matchId)
        {
            if (_state == null)
                return null;
            foreach (var league in _state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                    if (m.id == matchId)
                        return m;
            }
            return null;
        }

        private string GetPlayerName(int playerId)
        {
            var p = _state?.GetPlayer(playerId);
            return p?.info != null ? $"{p.info.firstName} {p.info.lastName}" : $"#{playerId}";
        }

        private static string FormatEvent(MatchEvent ev)
        {
            if (string.IsNullOrEmpty(ev.textKey))
                return $"{ev.minute}' {ev.type}";

            string text = Localization.Get(ev.textKey);
            if (ev.textArgs != null)
                foreach (var kv in ev.textArgs)
                    text = text.Replace("{" + kv.Key + "}", kv.Value);
            return text;
        }
    }
}
