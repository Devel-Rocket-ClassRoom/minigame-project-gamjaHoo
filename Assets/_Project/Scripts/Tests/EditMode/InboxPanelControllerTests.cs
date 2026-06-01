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
        public void SortForDisplay_ByPriorityThenRecency()
        {
            var items = new List<Inbox.InboxItem>
            {
                Item(1, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T.AddHours(1)),
                Item(2, Inbox.InboxPriority.RequiresAction, Inbox.InboxCategory.Transfer, T),
                Item(3, Inbox.InboxPriority.High, Inbox.InboxCategory.Board, T.AddHours(2)),
                Item(4, Inbox.InboxPriority.Low, Inbox.InboxCategory.Match, T.AddHours(3)),
            };
            var sorted = InboxPanelController.SortForDisplay(items);
            // 우선순위: RequiresAction(2) → High(3) → Low(최신순: 4 then 1)
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 1 }, sorted.Select(i => i.id).ToList());
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
    }
}
