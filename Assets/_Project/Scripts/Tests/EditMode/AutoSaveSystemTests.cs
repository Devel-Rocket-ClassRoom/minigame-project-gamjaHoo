// AutoSaveSystemTests.cs
// Stage X.7 (#431) — 자동 저장 순수 헬퍼 검증 (슬롯명 / 삭제 대상 선정). 실제 I/O 없음.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Persistence;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class AutoSaveSystemTests
    {
        private static SaveSlotMeta Meta(string name, DateTime savedAt) =>
            new SaveSlotMeta { slotName = name, savedAt = savedAt };

        // ── 슬롯명 ───────────────────────────────────────────────────

        [Test]
        public void MonthlySlotName_Format()
        {
            Assert.AreEqual(
                "autosave_Arsenal_2026-08",
                AutoSaveSystem.MonthlySlotName("Arsenal", new DateTime(2026, 8, 15))
            );
        }

        [Test]
        public void EventSlotName_Format()
        {
            Assert.AreEqual(
                "autosave_Man_City_2027_season_end",
                AutoSaveSystem.EventSlotName("Man City", 2027, "season_end")
            );
        }

        [Test]
        public void Sanitize_ReplacesSpacesAndInvalid()
        {
            var s = AutoSaveSystem.Sanitize("FC/Bar 1:2");
            StringAssert.DoesNotContain("/", s);
            StringAssert.DoesNotContain(":", s);
            StringAssert.DoesNotContain(" ", s);
        }

        [Test]
        public void Sanitize_Null_FallsBackToUser()
        {
            Assert.AreEqual("user", AutoSaveSystem.Sanitize(null));
        }

        // ── 삭제 대상 선정 (최근 3 보관) ─────────────────────────────

        [Test]
        public void SelectToDelete_KeepsRecent3_DeletesOlder()
        {
            var baseT = new DateTime(2026, 1, 1, 12, 0, 0);
            var list = new List<SaveSlotMeta>
            {
                Meta("autosave_a_1", baseT.AddMinutes(1)),
                Meta("autosave_a_2", baseT.AddMinutes(2)),
                Meta("autosave_a_3", baseT.AddMinutes(3)),
                Meta("autosave_a_4", baseT.AddMinutes(4)),
                Meta("autosave_a_5", baseT.AddMinutes(5)),
            };
            var toDelete = AutoSaveSystem.SelectToDelete(list, 3);
            Assert.AreEqual(2, toDelete.Count);
            var names = toDelete.Select(m => m.slotName).ToList();
            CollectionAssert.Contains(names, "autosave_a_1"); // 가장 오래된 2개
            CollectionAssert.Contains(names, "autosave_a_2");
            CollectionAssert.DoesNotContain(names, "autosave_a_5");
        }

        [Test]
        public void SelectToDelete_AtOrUnderKeep_ReturnsEmpty()
        {
            var baseT = new DateTime(2026, 1, 1);
            var list = new List<SaveSlotMeta>
            {
                Meta("autosave_a_1", baseT.AddDays(1)),
                Meta("autosave_a_2", baseT.AddDays(2)),
                Meta("autosave_a_3", baseT.AddDays(3)),
            };
            Assert.AreEqual(0, AutoSaveSystem.SelectToDelete(list, 3).Count);
        }

        [Test]
        public void SelectToDelete_Null_ReturnsEmpty()
        {
            Assert.AreEqual(0, AutoSaveSystem.SelectToDelete(null, 3).Count);
        }
    }
}
