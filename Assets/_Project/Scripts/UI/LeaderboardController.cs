// LeaderboardController.cs
// V1.0 M.1 / R.11 (#528): 리그 개인 리더보드 — 득점 / 도움 / 평점 / 클린시트 / 출전.
// LeaderboardSystem.GetLeaderboard 호출. 카테고리 전환 = MUIP HorizontalSelector.
// StandingsScene 리더보드 탭 패널에 독립 배치 (StandingsController 가 패널 토글만 담당).

using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FMLite.UI
{
    public class LeaderboardController : MonoBehaviour
    {
        [Header("카테고리 전환 (MUIP HorizontalSelector)")]
        [SerializeField]
        private HorizontalSelector categorySelector;

        [Header("목록")]
        [SerializeField]
        private Transform listParent;

        [SerializeField]
        private GameObject entryItemPrefab;

        [Header("컬럼 헤더 / 빈 상태")]
        [SerializeField]
        private TMP_Text valueHeaderText;

        [SerializeField]
        private TMP_Text emptyText;

        [Header("더보기 (top N → 전체)")]
        [SerializeField]
        private Button moreButton;

        [SerializeField]
        private TMP_Text moreLabel;

        // 셀렉터 인덱스 → 카테고리 (표시 순서).
        private static readonly LeaderboardCategory[] Categories = new[]
        {
            LeaderboardCategory.Goals,
            LeaderboardCategory.Assists,
            LeaderboardCategory.Rating,
            LeaderboardCategory.CleanSheets,
            LeaderboardCategory.Appearances,
        };

        private GameState _state;
        private League _league;
        private int _shownLimit; // 현재 표시 개수 (기본 = topN, 더보기 시 전체)
        private int _currentIndex;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            if (_state == null)
                return;

            _league = _state.leagues.Find(l => l.clubIds.Contains(_state.userClubId));
            if (_league == null)
                return;

            if (moreButton != null)
            {
                moreButton.onClick.AddListener(OnMoreClicked);
                if (moreLabel != null)
                    moreLabel.text = Localization.Get("leaderboard_more");
            }

            SetupSelector();
            _shownLimit = DefaultLimit();
            RefreshList(0);
        }

        private static int DefaultLimit()
        {
            var b = GameDatabase.GameBalance;
            return b != null && b.leaderboardDefaultTopN > 0 ? b.leaderboardDefaultTopN : 10;
        }

        private void SetupSelector()
        {
            if (categorySelector == null)
                return;

            categorySelector.itemList.Clear();
            foreach (var cat in Categories)
                categorySelector.CreateNewItem(Localization.Get(CategoryKey(cat)));
            categorySelector.defaultIndex = 0;
            categorySelector.SetupSelector();
            categorySelector.onValueChanged.AddListener(OnCategorySelected);
        }

        // 카테고리 변경 시 표시 개수를 기본(top N)으로 리셋.
        private void OnCategorySelected(int selectorIndex)
        {
            _shownLimit = DefaultLimit();
            RefreshList(selectorIndex);
        }

        private void OnMoreClicked()
        {
            _shownLimit = int.MaxValue; // 전체 펼침
            RefreshList(_currentIndex);
        }

        private void RefreshList(int selectorIndex)
        {
            if (selectorIndex < 0 || selectorIndex >= Categories.Length)
                return;
            if (listParent == null || entryItemPrefab == null)
                return;

            _currentIndex = selectorIndex;
            var category = Categories[selectorIndex];

            if (valueHeaderText != null)
                valueHeaderText.text = Localization.Get(CategoryKey(category));

            foreach (Transform child in listParent)
                Destroy(child.gameObject);

            var balance = GameDatabase.GameBalance;
            // 전체를 받아온 뒤 _shownLimit 만큼만 표시 → "더보기" 로 나머지 노출.
            var entries = LeaderboardSystem.GetLeaderboard(_state, _league, balance, category, 9999);
            int show = Mathf.Min(_shownLimit, entries.Count);

            if (emptyText != null)
            {
                emptyText.text = Localization.Get("leaderboard_empty");
                emptyText.gameObject.SetActive(entries.Count == 0);
            }

            for (int i = 0; i < show; i++)
            {
                var item = Instantiate(entryItemPrefab, listParent);
                item.GetComponent<LeaderboardEntryItem>()
                    .Setup(entries[i], _state, _state.userClubId, category);
            }

            if (moreButton != null)
                moreButton.gameObject.SetActive(entries.Count > show);

            Canvas.ForceUpdateCanvases();
            if (listParent is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private static string CategoryKey(LeaderboardCategory category) =>
            category switch
            {
                LeaderboardCategory.Goals => "leaderboard_cat_goals",
                LeaderboardCategory.Assists => "leaderboard_cat_assists",
                LeaderboardCategory.Rating => "leaderboard_cat_rating",
                LeaderboardCategory.CleanSheets => "leaderboard_cat_cleansheets",
                LeaderboardCategory.Appearances => "leaderboard_cat_appearances",
                _ => "leaderboard_cat_goals",
            };
    }
}
