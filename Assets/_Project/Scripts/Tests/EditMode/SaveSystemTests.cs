// SaveSystemTests.cs
// DoD 검증: v0.1-tasks.md Task 4.1 — Save/Load 라운드트립 + 인덱스 + atomic.

using System;
using System.IO;
using FMLite.Domain;
using FMLite.Persistence;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class SaveSystemTests
    {
        private string _slot;

        [SetUp]
        public void Setup()
        {
            _slot = "test_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        [TearDown]
        public void TearDown()
        {
            var path = SaveSystem.GetSlotPath(_slot);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        [Test]
        public void SaveLoad_EmptyState_Roundtrips()
        {
            var original = new GameState
            {
                currentDate = new DateTime(2024, 7, 1),
                userClubId = 1,
                rerollTokens = 3,
                randomSeed = 42,
                nextPlayerId = 501, // ClubGen 500명 호출 후 시뮬
            };

            SaveSystem.Save(original, _slot);
            var loaded = SaveSystem.Load(_slot);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(original.currentDate, loaded.currentDate);
            Assert.AreEqual(original.userClubId, loaded.userClubId);
            Assert.AreEqual(original.rerollTokens, loaded.rerollTokens);
            Assert.AreEqual(original.randomSeed, loaded.randomSeed);
            Assert.AreEqual(original.nextPlayerId, loaded.nextPlayerId);
        }

        [Test]
        public void SaveLoad_PopulatedState_IndexesRebuilt()
        {
            var original = new GameState { userClubId = 7 };
            original.AddClub(
                new Club
                {
                    id = 7,
                    name = "TestClub",
                    reputation = 50,
                }
            );
            original.AddPlayer(new Player { id = 100, currentClubId = 7 });
            original.AddPlayer(new Player { id = 200, currentClubId = 7 });

            SaveSystem.Save(original, _slot);
            var loaded = SaveSystem.Load(_slot);

            Assert.IsNotNull(loaded.GetPlayer(100));
            Assert.IsNotNull(loaded.GetPlayer(200));
            Assert.IsNotNull(loaded.GetClub(7));
            Assert.AreEqual("TestClub", loaded.GetClub(7).name);
            Assert.AreEqual(2, loaded.allPlayers.Count);
            Assert.AreEqual(1, loaded.allClubs.Count);
        }

        [Test]
        public void Save_TwiceSameSlot_OverwritesCleanly()
        {
            var first = new GameState { rerollTokens = 1 };
            SaveSystem.Save(first, _slot);

            var second = new GameState { rerollTokens = 5 };
            SaveSystem.Save(second, _slot);

            var loaded = SaveSystem.Load(_slot);
            Assert.AreEqual(5, loaded.rerollTokens);
        }

        [Test]
        public void Load_NonexistentSlot_ReturnsNull()
        {
            Assert.IsNull(SaveSystem.Load("nonexistent_slot_xyz"));
        }

        [Test]
        public void Save_WritesMetaFileWithUserClubName()
        {
            var state = new GameState { userClubId = 3, currentDate = new DateTime(2025, 8, 15) };
            state.AddClub(new Club { id = 3, name = "Arsenal" });

            SaveSystem.Save(state, _slot);

            var meta = SaveSystem.LoadSlotMeta(_slot);
            Assert.IsNotNull(meta);
            Assert.AreEqual("Arsenal", meta.userClubName);
            Assert.AreEqual(3, meta.userClubId);
            Assert.AreEqual(new DateTime(2025, 8, 15), meta.currentDate);
        }

        [Test]
        public void Save_LeavesNoTempFileBehind()
        {
            var state = new GameState { randomSeed = 99 };
            SaveSystem.Save(state, _slot);

            var slotPath = SaveSystem.GetSlotPath(_slot);
            var leftoverTmp = Directory.GetFiles(slotPath, "*.tmp");
            Assert.AreEqual(0, leftoverTmp.Length, "atomic rename should leave no .tmp files");
        }
    }
}
