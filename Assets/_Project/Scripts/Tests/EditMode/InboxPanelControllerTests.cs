// InboxPanelControllerTests.cs
// Stage B.1 (#439) — 인박스 표시 로직 (정렬/필터/카운트) 순수 검증.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.UI;
using NUnit.Framework;
using Inbox = FMLite.Domain;

namespace FMLite.Tests
{
    public class InboxPanelControllerTests
    {
        private static readonly DateTime T = new DateTime(2026, 8, 1, 12, 0, 0);

        private static Inbox.InboxItem Item(
            int id,
            Inbox.InboxPriority p,
            Inbox.InboxCategory c,
            DateTime created,
            bool read = false
        ) =>
            new Inbox.InboxItem
            {
                id = id,
                priority = p,
                category = c,
                createdAt = created,
                isRead = read,
            };

        [Test]
        public void PriorityRank_RequiresActionHighest()
        {
            Assert.Less(
                InboxPanelController.PriorityRank(Inbox.InboxPriority.RequiresAction),
                InboxPanelController.PriorityRank(Inbox.InboxPriority.High)
            );
            Assert.Less(
                InboxPanelController.PriorityRank(Inbox.InboxPriority.High),
                InboxPanelController.PriorityRank(Inbox.InboxPriority.Medium)
            );
            Assert.Less(
                InboxPanelController.PriorityRank(Inbox.InboxPriority.Medium),
                InboxPanelController.PriorityRank(Inbox.InboxPriority.Low)
            );
        }

        [Test]
        public void SortForDisplay_ByRecency_NewestTopOldestBottom()
        {
            // 우선순위 무관 — 최신순(createdAt 내림차순)으로만 정렬 (사용자 요청 2026-06-07, 최신이 위).
            var items = new List<Inbox.InboxItem>
            {
                Item(1, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T.AddHours(1)),
                Item(2, Inbox.InboxPriority.RequiresAction, Inbox.InboxCategory.Transfer, T),
                Item(3, Inbox.InboxPriority.High, Inbox.InboxCategory.Board, T.AddHours(2)),
                Item(4, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T.AddHours(3)),
            };
            var sorted = InboxPanelController.SortForDisplay(items);
            // createdAt 내림차순: 4(+3h) → 3(+2h) → 1(+1h) → 2(T)
            CollectionAssert.AreEqual(new[] { 4, 3, 1, 2 }, sorted.Select(i => i.id).ToList());
        }

        [Test]
        public void SortForDisplay_SameDate_TiebreakByIdDescending()
        {
            // 같은 날 발생 (createdAt 동일) → id 내림차순 = 최근 발생이 위
            var items = new List<Inbox.InboxItem>
            {
                Item(5, Inbox.InboxPriority.Low, Inbox.InboxCategory.League, T),
                Item(2, Inbox.InboxPriority.High, Inbox.InboxCategory.Match, T),
                Item(9, Inbox.InboxPriority.RequiresAction, Inbox.InboxCategory.Transfer, T),
            };
            var sorted = InboxPanelController.SortForDisplay(items);
            CollectionAssert.AreEqual(new[] { 9, 5, 2 }, sorted.Select(i => i.id).ToList());
        }

        [Test]
        public void Filter_ByCategory()
        {
            var items = new List<Inbox.InboxItem>
            {
                Item(1, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T),
                Item(2, Inbox.InboxPriority.Low, Inbox.InboxCategory.Transfer, T),
                Item(3, Inbox.InboxPriority.Low, Inbox.InboxCategory.Transfer, T),
            };
            var transfer = InboxPanelController.Filter(items, Inbox.InboxCategory.Transfer);
            Assert.AreEqual(2, transfer.Count);
            Assert.IsTrue(transfer.All(i => i.category == Inbox.InboxCategory.Transfer));
        }

        [Test]
        public void Filter_Null_ReturnsAll()
        {
            var items = new List<Inbox.InboxItem>
            {
                Item(1, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T),
                Item(2, Inbox.InboxPriority.Low, Inbox.InboxCategory.Transfer, T),
            };
            Assert.AreEqual(2, InboxPanelController.Filter(items, null).Count);
        }

        [Test]
        public void CountUnread_CountsUnreadOnly()
        {
            var items = new List<Inbox.InboxItem>
            {
                Item(1, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T, read: true),
                Item(2, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T, read: false),
                Item(3, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T, read: false),
            };
            Assert.AreEqual(2, InboxPanelController.CountUnread(items));
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(0, InboxPanelController.SortForDisplay(null).Count);
            Assert.AreEqual(0, InboxPanelController.Filter(null, null).Count);
            Assert.AreEqual(0, InboxPanelController.CountUnread(null));
        }

        // ── B.4: 기한 만료 비활성 표시 (InboxEntryView.IsExpired) ──────

        private static Inbox.GameState StateAt(DateTime d) =>
            new Inbox.GameState { currentDate = d };

        [Test]
        public void IsExpired_PastDeadline_True()
        {
            var item = Item(1, Inbox.InboxPriority.RequiresAction, Inbox.InboxCategory.Transfer, T);
            item.deadline = T.AddDays(-1);
            Assert.IsTrue(InboxEntryView.IsExpired(item, StateAt(T)));
        }

        [Test]
        public void IsExpired_FutureDeadline_False()
        {
            var item = Item(1, Inbox.InboxPriority.RequiresAction, Inbox.InboxCategory.Transfer, T);
            item.deadline = T.AddDays(3);
            Assert.IsFalse(InboxEntryView.IsExpired(item, StateAt(T)));
        }

        [Test]
        public void IsExpired_SameDay_False()
        {
            // 마감 당일(D-0)은 아직 처리 가능 — 만료 아님
            var item = Item(1, Inbox.InboxPriority.RequiresAction, Inbox.InboxCategory.Transfer, T);
            item.deadline = T.Date.AddHours(23);
            Assert.IsFalse(InboxEntryView.IsExpired(item, StateAt(T)));
        }

        [Test]
        public void IsExpired_NoDeadlineOrNullState_False()
        {
            var item = Item(1, Inbox.InboxPriority.Low, Inbox.InboxCategory.Morale, T); // deadline null
            Assert.IsFalse(InboxEntryView.IsExpired(item, StateAt(T)));
            item.deadline = T.AddDays(-5);
            Assert.IsFalse(InboxEntryView.IsExpired(item, null));
        }
    }
}
