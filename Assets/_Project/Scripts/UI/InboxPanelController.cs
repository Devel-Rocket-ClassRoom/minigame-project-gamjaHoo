// InboxPanelController.cs
// Stage B.1 (V1.0) — 인박스 패널 표시 로직 (GameState.inbox 기반, design-decisions #66 / v1.0-plan §3.1·§3.18).
// 우선순위 정렬 (RequiresAction→High→Medium→Low) / 카테고리 필터 / 안읽음 카운트 — 순수 static (테스트 대상).
// 패널 prefab 렌더링 (카테고리 탭 + 행 + 클릭→InboxAction) 은 후속 MCP 작업.
//
// 주의: FMLite.UI.InboxItem (구 V0.5 row MonoBehaviour) 와 이름 충돌 회피 위해
//       도메인 타입은 alias(Inbox) 로 참조 — `using FMLite.Domain;` 금지.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Inbox = FMLite.Domain;

namespace FMLite.UI
{
    public class InboxPanelController : MonoBehaviour
    {
        // ── 순수 표시 로직 (테스트 대상) ─────────────────────────────

        /// <summary>우선순위 표시 순서 (낮을수록 위). RequiresAction 최상단.</summary>
        public static int PriorityRank(Inbox.InboxPriority p) =>
            p switch
            {
                Inbox.InboxPriority.RequiresAction => 0,
                Inbox.InboxPriority.High => 1,
                Inbox.InboxPriority.Medium => 2,
                Inbox.InboxPriority.Low => 3,
                _ => 4,
            };

        /// <summary>표시 정렬: 우선순위 → 최신순 (createdAt 내림차순).</summary>
        public static List<Inbox.InboxItem> SortForDisplay(IEnumerable<Inbox.InboxItem> items)
        {
            if (items == null)
                return new List<Inbox.InboxItem>();
            return items
                .Where(i => i != null)
                .OrderBy(i => PriorityRank(i.priority))
                .ThenByDescending(i => i.createdAt)
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
