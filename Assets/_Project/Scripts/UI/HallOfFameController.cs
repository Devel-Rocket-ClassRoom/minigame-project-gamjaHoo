// HallOfFameController.cs
// 명예의 전당 화면 컨트롤러 (UI Layer). HallOfFameScene 전용.
//   · 좌(또는 상): 글로벌 상위 N 리더보드 — LeaderboardRepository.StartListener 실시간 갱신.
//   · 우(또는 하): 내 역대 시즌 — CareerRepository.LoadRecentSeasonsAsync 1회 로드.
//   · [뒤로] → MainMenuScene.
//
// Firebase 게이트: FirebaseBootstrap 은 MainMenuScene 에서 DontDestroyOnLoad 로 넘어온다.
//   아직 IsReady 가 아닐 수 있어 OnReady (이미 ready 면 즉시 호출) 로 진입을 미룬다.
//
// ⚠️ 메인스레드: StartListener 의 ValueChanged 콜백은 메인스레드 보장이 없다(Persistence 주석).
//   Start 에서 캡처한 SynchronizationContext 로 Post 해 UI 갱신을 메인스레드에 마샬링한다.
//   await(LoadRecentSeasonsAsync) 연속은 UnitySynchronizationContext 로 메인스레드 복귀가 보장된다.

using System;
using System.Collections.Generic;
using System.Threading;
using FMLite.Application;
using FMLite.Core;
using FMLite.Persistence.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LeaderboardEntry = FMLite.Persistence.Cloud.LeaderboardEntry;

namespace FMLite.UI
{
    public class HallOfFameController : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenuScene";

        [Header("리더보드 (상위 N)")]
        [SerializeField]
        private Transform leaderboardListParent;

        [SerializeField]
        private GameObject rankItemPrefab;

        [SerializeField]
        private TMP_Text leaderboardEmptyText;

        [SerializeField]
        private int topCount = 20;

        [Header("내 역대 시즌")]
        [SerializeField]
        private Transform seasonListParent;

        [SerializeField]
        private GameObject seasonItemPrefab;

        [SerializeField]
        private TMP_Text seasonEmptyText;

        [SerializeField]
        private int seasonCount = 10;

        [Header("정적 라벨 (런타임 로컬라이즈)")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text leaderboardTitleText;

        [SerializeField]
        private TMP_Text seasonTitleText;

        [SerializeField]
        private TMP_Text backLabelText;

        [Header("공통")]
        [SerializeField]
        private TMP_Text statusText; // 로딩 / 미준비 / 오류 안내 (정상 시 빈칸)

        [SerializeField]
        private Button backButton;

        private SynchronizationContext _mainThread;
        private bool _listening;

        private void Start()
        {
            _mainThread = SynchronizationContext.Current;

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(GoBack);
            }

            LocalizeLabels();
            SetStatus(Localization.Get("hof_loading"));
            ClearList(leaderboardListParent);
            ClearList(seasonListParent);
            SetEmpty(leaderboardEmptyText, false);
            SetEmpty(seasonEmptyText, false);

            var fb = FirebaseBootstrap.Instance;
            if (fb == null)
            {
                SetStatus(Localization.Get("hof_firebase_unavailable"));
                return;
            }
            fb.OnReady += OnFirebaseReady; // 이미 ready 면 즉시 호출
        }

        private void OnDestroy()
        {
            if (FirebaseBootstrap.Instance != null)
                FirebaseBootstrap.Instance.OnReady -= OnFirebaseReady;
            if (_listening)
            {
                LeaderboardRepository.StopListener();
                _listening = false;
            }
        }

        private void OnFirebaseReady()
        {
            SetStatus(string.Empty);
            StartLeaderboardListener();
            LoadMySeasons();
        }

        // ── 리더보드 (실시간) ────────────────────────────────────────
        private void StartLeaderboardListener()
        {
            try
            {
                LeaderboardRepository.StartListener(topCount, OnLeaderboardChanged);
                _listening = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoF] 리더보드 리스너 시작 실패: {ex.Message}");
                SetStatus(Localization.Get("hof_load_failed"));
            }
        }

        // Firebase 스레드 가능 → 메인스레드로 마샬링.
        private void OnLeaderboardChanged(List<LeaderboardEntry> entries)
        {
            if (_mainThread != null)
                _mainThread.Post(_ => PopulateLeaderboard(entries), null);
            else
                PopulateLeaderboard(entries);
        }

        private void PopulateLeaderboard(List<LeaderboardEntry> entries)
        {
            if (this == null || leaderboardListParent == null)
                return;

            ClearList(leaderboardListParent);

            bool empty = entries == null || entries.Count == 0;
            SetEmpty(leaderboardEmptyText, empty);
            if (empty)
                return;

            string myUid = FirebaseBootstrap.Instance?.UserId;
            for (int i = 0; i < entries.Count; i++)
            {
                var go = Instantiate(rankItemPrefab, leaderboardListParent);
                go.GetComponent<HallOfFameRankItem>()?.Setup(i + 1, entries[i], myUid);
            }
        }

        // ── 내 역대 시즌 (1회) ───────────────────────────────────────
        private async void LoadMySeasons()
        {
            try
            {
                List<SeasonRecord> seasons = await CareerRepository.LoadRecentSeasonsAsync(
                    seasonCount
                );
                if (this == null || seasonListParent == null)
                    return;

                ClearList(seasonListParent);

                bool empty = seasons == null || seasons.Count == 0;
                SetEmpty(seasonEmptyText, empty);
                if (empty)
                    return;

                foreach (var rec in seasons)
                {
                    var go = Instantiate(seasonItemPrefab, seasonListParent);
                    go.GetComponent<HallOfFameSeasonItem>()?.Setup(rec);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoF] 내 시즌 로드 실패: {ex.Message}");
                SetEmpty(seasonEmptyText, true);
            }
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────
        private void GoBack() => SceneManager.LoadScene(MainMenuScene);

        // 정적 라벨/빈 안내 텍스트를 현재 언어로. (씬에는 플레이스홀더만 들어있음.)
        private void LocalizeLabels()
        {
            SetText(titleText, "hof_title");
            SetText(leaderboardTitleText, "hof_leaderboard_title");
            SetText(seasonTitleText, "hof_my_seasons_title");
            SetText(backLabelText, "hof_back");
            SetText(leaderboardEmptyText, "hof_leaderboard_empty");
            SetText(seasonEmptyText, "hof_my_seasons_empty");
        }

        private static void SetText(TMP_Text label, string key)
        {
            if (label != null)
                label.text = Localization.Get(key);
        }

        private static void ClearList(Transform parent)
        {
            if (parent == null)
                return;
            foreach (Transform child in parent)
                Destroy(child.gameObject);
        }

        private static void SetEmpty(TMP_Text label, bool show)
        {
            if (label != null)
                label.gameObject.SetActive(show);
        }

        private void SetStatus(string text)
        {
            if (statusText == null)
                return;
            statusText.text = text;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
