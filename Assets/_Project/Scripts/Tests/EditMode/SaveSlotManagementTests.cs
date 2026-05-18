// SaveSlotManagementTests.cs
// DoD 검증: v0.1-tasks.md Task 4.2 — 슬롯 목록 / 삭제.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using FMLite.Domain;
using FMLite.Persistence;

namespace FMLite.Tests
{
    public class SaveSlotManagementTests
    {
        // 다른 테스트나 이전 실행이 남긴 슬롯을 배제하기 위해 prefix 로 필터.
        private string _prefix;

        [SetUp]
        public void Setup()
        {
            _prefix = "tslot_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        [TearDown]
        public void TearDown()
        {
            if (!Directory.Exists(SaveSystem.SavesPath)) return;
            foreach (var dir in Directory.GetDirectories(SaveSystem.SavesPath))
            {
                if (Path.GetFileName(dir).StartsWith(_prefix))
                    Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void ListSlots_ReturnsAllSavedSlotsWithMeta()
        {
            var state = new GameState { userClubId = 1 };
            state.AddClub(new Club { id = 1, name = "TestClub" });

            SaveSystem.Save(state, _prefix + "_a");
            SaveSystem.Save(state, _prefix + "_b");
            SaveSystem.Save(state, _prefix + "_c");

            var mySlots = SaveSystem.ListSlots()
                .Where(m => m.slotName.StartsWith(_prefix))
                .ToList();

            Assert.AreEqual(3, mySlots.Count);
            Assert.IsTrue(mySlots.All(m => m.userClubName == "TestClub"));
        }

        [Test]
        public void ListSlots_SkipsDirectoriesWithoutMeta()
        {
            // meta.json 없는 빈 폴더 생성
            var orphanPath = Path.Combine(SaveSystem.SavesPath, _prefix + "_orphan");
            Directory.CreateDirectory(orphanPath);

            var mySlots = SaveSystem.ListSlots()
                .Where(m => m.slotName.StartsWith(_prefix))
                .ToList();

            Assert.AreEqual(0, mySlots.Count, "meta.json 없으면 목록에 포함되지 않아야 함");
        }

        [Test]
        public void DeleteSlot_RemovesFolderAndReturnsTrue()
        {
            var state = new GameState();
            var slot = _prefix + "_del";
            SaveSystem.Save(state, slot);
            Assert.IsTrue(Directory.Exists(SaveSystem.GetSlotPath(slot)));

            bool removed = SaveSystem.DeleteSlot(slot);

            Assert.IsTrue(removed);
            Assert.IsFalse(Directory.Exists(SaveSystem.GetSlotPath(slot)));
            Assert.IsNull(SaveSystem.Load(slot));
        }

        [Test]
        public void DeleteSlot_NonexistentSlot_ReturnsFalse()
        {
            Assert.IsFalse(SaveSystem.DeleteSlot(_prefix + "_never_existed"));
        }
    }
}
