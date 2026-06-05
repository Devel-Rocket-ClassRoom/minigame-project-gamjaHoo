// MatchTextController.cs
// V0.5 N.3 — 매치 텍스트 이벤트 화면.
// PlayerPrefs("SelectedMatchId") 로 매치 ID 수신.
// collectEvents=true 로 시뮬된 이벤트를 분 단위로 표시.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class MatchTextController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";
        private const string MatchResultDashboardScene = "MatchResultDashboard";
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

        [Header("진행 바 (분)")]
        [SerializeField]
        private Image progressFill; // type=Filled. 분 진행률 0..1.

        [Header("라인업 패널")]
        [SerializeField]
        private GameObject lineupPanel;

        [SerializeField]
        private TMP_Text lineupText;

        [SerializeField]
        private Button lineupToggleButton;

        [SerializeField]
        private Button lineupCloseButton;

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

        // AA.6 — 결과 패널에서 MatchResultDashboard(6탭 대시보드) 진입.
        [SerializeField]
        private Button resultDashboardButton;

        private GameState _state;
        private Match _match;
        private int _homeScore;
        private int _awayScore;
        private float _speedMultiplier = 1f;
        private bool _isSkipped;
        private bool _finished;
        private int _playIndex;
        private int _totalMinutes = 90;

        // 추가시간 표기용 진행 단계 (전반/후반/연장 — 45+x, 90+x 등 표시).
        private bool _secondHalf;
        private bool _extraTime;
        private bool _etSecondHalf;
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
            if (resultDashboardButton != null)
                resultDashboardButton.onClick.AddListener(OnViewResultDashboard);
            if (lineupToggleButton != null)
                lineupToggleButton.onClick.AddListener(ToggleLineupPanel);
            if (lineupCloseButton != null)
                lineupCloseButton.onClick.AddListener(() => SetLineupPanel(false));

            if (resultPanel != null)
                resultPanel.SetActive(false);
            if (lineupPanel != null)
                lineupPanel.SetActive(false);
        }

        private void Update()
        {
            if (_finished)
                return;
            var kb = Keyboard.current;
            if (kb == null)
                return;
            // J.1 단축키: 1/2/3/4 = ×1/×2/×4/스킵 (v1.0-plan §3.20.5).
            if (kb.digit1Key.wasPressedThisFrame)
                SetSpeed(1f);
            else if (kb.digit2Key.wasPressedThisFrame)
                SetSpeed(2f);
            else if (kb.digit3Key.wasPressedThisFrame)
                SetSpeed(4f);
            else if (kb.digit4Key.wasPressedThisFrame)
                OnSkip();
        }

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            int matchId = PlayerPrefs.GetInt(DashboardController.SelectedMatchIdKey, -1);
            _match = FindMatch(matchId);

            // J.5 — 매치 BGM (에셋/인스턴스 없으면 silent fallback).
            SoundManager.Instance?.PlayBGM(BgmId.Match);

            if (_match?.result == null)
            {
                ShowResultPanel();
                return;
            }

            SetupHeader();
            _totalMinutes = ComputeTotalMinutes();
            BuildLineupText();
            UpdateProgress(0);
            _playCoroutine = StartCoroutine(PlayEvents());
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        // AA.6 — [결과 보기] → MatchResultDashboard. SelectedMatchId 는 이미 PlayerPrefs 에 설정됨.
        public void OnViewResultDashboard() =>
            SceneManager.LoadScene(MatchResultDashboardScene);

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

            for (_playIndex = 0; _playIndex < events.Count; _playIndex++)
            {
                if (_isSkipped)
                    break;

                bool shown = ProcessEvent(events[_playIndex]);
                if (shown)
                    yield return new WaitForSeconds(BaseDelay / _speedMultiplier);
            }

            // 스킵 시 남은 이벤트 즉시 처리 (인덱스 기준 — 필터로 표시 항목 수 ≠ 이벤트 수)
            if (_isSkipped)
            {
                for (; _playIndex < events.Count; _playIndex++)
                    ProcessEvent(events[_playIndex]);
                ScrollToBottom();
            }

            ShowResultPanel();
        }

        // 점수/분/진행은 모든 이벤트로 갱신. 텍스트/SFX 는 핵심 이벤트만 (J.2). 표시 여부 반환.
        private bool ProcessEvent(MatchEvent ev)
        {
            UpdateScore(ev);
            UpdateMinute(ev.minute);
            UpdateProgress(ev.minute);

            bool show =
                MatchEventDisplay.ShouldShowText(ev.type) && !string.IsNullOrEmpty(ev.textKey);
            if (show)
            {
                SpawnEventItem(ev);
                PlayEventSfx(ev.type);
                ScrollToBottom();
            }

            UpdatePhase(ev.type);
            return show;
        }

        private void SpawnEventItem(MatchEvent ev)
        {
            if (eventItemPrefab == null || eventListContent == null)
                return;

            // worldPositionStays=false — 스케일된 캔버스 하위 배치 시 localScale/위치 보존 (UI 표준).
            var go = Instantiate(eventItemPrefab, eventListContent, false);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = FormatEvent(ev);
        }

        private void UpdatePhase(MatchEventType type)
        {
            if (type == MatchEventType.HalfTime)
                _secondHalf = true;
            else if (type == MatchEventType.ExtraTimeKickOff)
                _extraTime = true;
            else if (type == MatchEventType.ExtraTimeHalfTime)
                _etSecondHalf = true;
        }

        private static void PlayEventSfx(MatchEventType type)
        {
            var sfx = MatchEventDisplay.SfxFor(type);
            if (sfx.HasValue)
                SoundManager.Instance?.PlaySFX(sfx.Value);
        }

        private void UpdateProgress(int minute)
        {
            if (progressFill == null)
                return;
            progressFill.fillAmount =
                _totalMinutes > 0 ? Mathf.Clamp01((float)minute / _totalMinutes) : 0f;
        }

        private int ComputeTotalMinutes()
        {
            int max = 90;
            var ev = _match?.events;
            if (ev != null && ev.Count > 0)
                max = Mathf.Max(max, ev[ev.Count - 1].minute);
            return max;
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
                minuteText.text = FormatMinute(minute);
        }

        // 추가시간은 기준 분 + 초과분으로 표기 (전반 45+x / 후반 90+x / 연장 105+x·120+x).
        private string FormatMinute(int m)
        {
            if (!_secondHalf)
                return m > 45 ? $"45+{m - 45}'" : $"{m}'";
            if (!_extraTime)
                return m > 90 ? $"90+{m - 90}'" : $"{m}'";
            if (!_etSecondHalf)
                return m > 105 ? $"105+{m - 105}'" : $"{m}'";
            return m > 120 ? $"120+{m - 120}'" : $"{m}'";
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
            _finished = true;
            UpdateProgress(_totalMinutes);

            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);

            // 결과 표시 시 라인업 패널은 닫고, 결과 패널을 최상위로 (#497 — 라인업 위에 떠 갇히는 문제 방지).
            SetLineupPanel(false);
            if (lineupToggleButton != null)
                lineupToggleButton.interactable = false;

            if (resultPanel != null)
            {
                resultPanel.transform.SetAsLastSibling();
                resultPanel.SetActive(true);
            }

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
                lines.Add($"{name}  {ps.rating:0.0}");
            }
            return string.Join("\n", lines);
        }

        // ── 라인업 패널 (J.4 — 상대/자팀 선발 11) ─────────────────────

        private void ToggleLineupPanel()
        {
            if (lineupPanel != null)
                SetLineupPanel(!lineupPanel.activeSelf);
        }

        private void SetLineupPanel(bool show)
        {
            if (lineupPanel != null)
                lineupPanel.SetActive(show);
        }

        private void BuildLineupText()
        {
            if (lineupText == null || _match?.result == null)
                return;

            var home = _state.GetClub(_match.homeClubId)?.name ?? "?";
            var away = _state.GetClub(_match.awayClubId)?.name ?? "?";

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{home}</b>");
            foreach (var id in _match.result.homeStarting11)
                sb.AppendLine(GetPlayerName(id));
            sb.AppendLine();
            sb.AppendLine($"<b>{away}</b>");
            foreach (var id in _match.result.awayStarting11)
                sb.AppendLine(GetPlayerName(id));

            lineupText.text = sb.ToString();
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
