// InboxPanelController.cs
// Stage B.1 (V1.0) — 인박스 패널 표시 로직 (GameState.inbox 기반, design-decisions #66 / v1.0-plan §3.1·§3.18).
// 우선순위 정렬 (RequiresAction→High→Medium→Low) / 카테고리 필터 / 안읽음 카운트 — 순수 static (테스트 대상).
// 패널 prefab 렌더링 (카테고리 탭 + 행 + 클릭→InboxAction) 은 후속 MCP 작업.
//
// 주의: FMLite.UI.InboxItem (구 V0.5 row MonoBehaviour) 와 이름 충돌 회피 위해
//       도메인 타입은 alias(Inbox) 로 참조 — `using FMLite.Domain;` 금지.

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Inbox = FMLite.Domain;

namespace FMLite.UI
{
    public class InboxPanelController : MonoBehaviour
    {
        // ── 패널 렌더링 (MonoBehaviour 파트) ─────────────────────────
        // GlobalNavController.ToggleInboxPanel 이 패널 활성화 시 Refresh() 호출.

        [Header("카테고리 탭 (index 0 = 전체, 1.. = InboxCategory 순서: Match·Transfer·Morale·Board·Youth·Cup·Award)")]
        [SerializeField]
        private Button[] categoryTabs;

        [SerializeField]
        private Image[] categoryTabBackgrounds; // 선택 강조 (탭 버튼 배경, 선택)

        [SerializeField]
        private TMP_Text[] tabLabels; // 탭 라벨 (런타임 로컬라이즈, index = categoryTabs 와 1:1)

        // 탭 라벨 키 (index 0 = 전체, 1.. = InboxCategory 순서). 전체는 filter_all 재사용.
        private static readonly string[] TabLabelKeys =
        {
            "filter_all",
            "inbox_category_match",
            "inbox_category_transfer",
            "inbox_category_morale",
            "inbox_category_board",
            "inbox_category_youth",
            "inbox_category_cup",
            "inbox_category_award",
        };

        [Header("리스트")]
        [SerializeField]
        private Transform listContent; // ScrollView 의 Content (VerticalLayoutGroup)

        [SerializeField]
        private GameObject entryPrefab; // InboxEntryView 를 가진 행 prefab

        [SerializeField]
        private GameObject emptyLabel; // "받은 알림이 없습니다." (inbox_empty)

        [Header("탭 강조 색 (#4A90D9 활성 / #2A2A3E 비활성)")]
        [SerializeField]
        private Color tabActiveColor = new Color(0.290f, 0.565f, 0.851f);

        [SerializeField]
        private Color tabInactiveColor = new Color(0.165f, 0.165f, 0.243f);

        // 선택된 카테고리 (null = 전체)
        private Inbox.InboxCategory? _selectedCategory = null;

        private void Awake()
        {
            WireTabs();
            LocalizeTabs();
        }

        private void LocalizeTabs()
        {
            if (tabLabels == null)
                return;
            for (int i = 0; i < tabLabels.Length && i < TabLabelKeys.Length; i++)
                if (tabLabels[i] != null)
                    tabLabels[i].text = Localization.Get(TabLabelKeys[i]);
        }

        private void WireTabs()
        {
            if (categoryTabs == null)
                return;
            for (int i = 0; i < categoryTabs.Length; i++)
            {
                int idx = i;
                var btn = categoryTabs[i];
                if (btn == null)
                    continue;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectTab(idx));
            }
        }

        /// <summary>탭 선택. index 0 = 전체, 1.. = (InboxCategory)(index-1).</summary>
        public void SelectTab(int index)
        {
            _selectedCategory =
                index <= 0 ? (Inbox.InboxCategory?)null : (Inbox.InboxCategory)(index - 1);
            Refresh();
        }

        /// <summary>GameState.inbox 읽어 탭 필터 → 정렬 → 행 재생성. 패널 열 때마다 호출.</summary>
        public void Refresh()
        {
            UpdateTabHighlight();

            if (listContent == null)
                return;

            // 기존 행 제거
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);

            var state = GameManager.Instance?.State;
            var filtered = SortForDisplay(Filter(state?.inbox, _selectedCategory));

            if (emptyLabel != null)
                emptyLabel.SetActive(filtered.Count == 0);

            if (entryPrefab == null)
                return;

            foreach (var item in filtered)
            {
                var go = Instantiate(entryPrefab, listContent);
                go.SetActive(true);
                go.GetComponent<InboxEntryView>()?.Setup(item, state, OnEntryClicked);
            }
        }

        private void UpdateTabHighlight()
        {
            if (categoryTabBackgrounds == null)
                return;
            int activeIdx = _selectedCategory.HasValue ? (int)_selectedCategory.Value + 1 : 0;
            for (int i = 0; i < categoryTabBackgrounds.Length; i++)
            {
                if (categoryTabBackgrounds[i] != null)
                    categoryTabBackgrounds[i].color =
                        i == activeIdx ? tabActiveColor : tabInactiveColor;
            }
        }

        /// <summary>행 클릭: 읽음 처리 + InboxAction 라우팅. 배지/리스트 갱신.</summary>
        private void OnEntryClicked(Inbox.InboxItem item)
        {
            if (item == null)
                return;
            item.isRead = true;
            GlobalNavController.Instance?.RefreshFromState(); // TopBar 배지 갱신
            RouteAction(item); // 씬 전환이면 아래 Refresh 는 의미 없음 (씬 언로드)
            Refresh(); // 같은 씬 유지 시 읽음 강조 반영
        }

        private static void RouteAction(Inbox.InboxItem item)
        {
            switch (item.action)
            {
                case Inbox.InboxAction.OpenScene:
                    if (!string.IsNullOrEmpty(item.actionTargetSceneOrDialogId))
                    {
                        // [뒤로] 일관성 — 직전 씬 기록 (GlobalNav.NavigateTo 와 동일 패턴)
                        PlayerPrefs.SetString(
                            GlobalNavController.PreviousSceneKey,
                            SceneManager.GetActiveScene().name
                        );
                        SceneManager.LoadScene(item.actionTargetSceneOrDialogId);
                    }
                    break;
                case Inbox.InboxAction.OpenDialog:
                // 다이얼로그 처리는 Stage B.2/B.3. B.1 은 읽음 처리까지만.
                case Inbox.InboxAction.None:
                default:
                    break;
            }
        }

        // ── 순수 표시 로직 (테스트 대상) ─────────────────────────────

        /// <summary>우선순위 표시 순서 (낮을수록 위). RequiresAction 최상단. (SortForDisplay 는 미사용 — 행 강조 등 보조용 유지.)</summary>
        public static int PriorityRank(Inbox.InboxPriority p) =>
            p switch
            {
                Inbox.InboxPriority.RequiresAction => 0,
                Inbox.InboxPriority.High => 1,
                Inbox.InboxPriority.Medium => 2,
                Inbox.InboxPriority.Low => 3,
                _ => 4,
            };

        /// <summary>
        /// 표시 정렬: 최신순 (createdAt 내림차순 → id 내림차순). 최신 항목이 맨 위에 쌓임.
        /// (사용자 요청 2026-06-07 — 카테고리/우선순위 그룹핑 폐기, 타임라인식. 최신이 위로 와 스크롤 불필요. id = 단조증가 = 발생 순서 타이브레이크.)
        /// </summary>
        public static List<Inbox.InboxItem> SortForDisplay(IEnumerable<Inbox.InboxItem> items)
        {
            if (items == null)
                return new List<Inbox.InboxItem>();
            return items
                .Where(i => i != null)
                .OrderByDescending(i => i.createdAt)
                .ThenByDescending(i => i.id)
                .ToList();
        }

        /// <summary>카테고리 필터. null = 전체.</summary>
        public static List<Inbox.InboxItem> Filter(
            IEnumerable<Inbox.InboxItem> items,
            Inbox.InboxCategory? category
        )
        {
            if (items == null)
                return new List<Inbox.InboxItem>();
            var nonNull = items.Where(i => i != null);
            if (category.HasValue)
                nonNull = nonNull.Where(i => i.category == category.Value);
            return nonNull.ToList();
        }

        /// <summary>안 읽은 항목 수 (TopBar 배지용).</summary>
        public static int CountUnread(IEnumerable<Inbox.InboxItem> items) =>
            items?.Count(i => i != null && !i.isRead) ?? 0;
    }
}
