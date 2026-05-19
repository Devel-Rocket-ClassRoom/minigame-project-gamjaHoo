// GameStateTests.cs
// DoD 검증: v0.1-tasks.md Task 3.3.

using NUnit.Framework;
using FMLite.Domain;

namespace FMLite.Tests
{
    public class GameStateTests
    {
        private static Player MakePlayer(int id) => new Player { id = id };
        private static Club MakeClub(int id) => new Club { id = id };

        [Test]
        public void AddPlayer_GetPlayer_ReturnsSameInstance()
        {
            var state = new GameState();
            var p = MakePlayer(42);

            state.AddPlayer(p);

            Assert.AreSame(p, state.GetPlayer(42));
        }

        [Test]
        public void BuildIndexes_AfterPrePopulatedList_GetPlayerWorks()
        {
            var state = new GameState();
            var p = MakePlayer(7);
            state.allPlayers.Add(p);   // 마스터 리스트에 직접 추가 (로드 시뮬레이션)

            state.BuildIndexes();

            Assert.AreSame(p, state.GetPlayer(7));
        }

        [Test]
        public void RemovePlayer_RemovesFromBothListAndIndex()
        {
            var state = new GameState();
            var p = MakePlayer(99);
            state.AddPlayer(p);

            bool removed = state.RemovePlayer(99);

            Assert.IsTrue(removed);
            Assert.IsNull(state.GetPlayer(99));
            Assert.AreEqual(0, state.allPlayers.Count);
        }

        [Test]
        public void RemovePlayer_NonexistentId_ReturnsFalse()
        {
            var state = new GameState();
            state.BuildIndexes();

            Assert.IsFalse(state.RemovePlayer(404));
        }

        [Test]
        public void GetPlayer_BeforeBuildIndexes_LazilyBuildsAndReturns()
        {
            var state = new GameState();
            var p = MakePlayer(1);
            state.allPlayers.Add(p);  // BuildIndexes 호출 없이

            Assert.AreSame(p, state.GetPlayer(1));
        }

        [Test]
        public void AddClub_GetClub_ReturnsSameInstance()
        {
            var state = new GameState();
            var c = MakeClub(3);

            state.AddClub(c);

            Assert.AreSame(c, state.GetClub(3));
        }

        [Test]
        public void RemoveClub_RemovesFromBothListAndIndex()
        {
            var state = new GameState();
            var c = MakeClub(5);
            state.AddClub(c);

            bool removed = state.RemoveClub(5);

            Assert.IsTrue(removed);
            Assert.IsNull(state.GetClub(5));
            Assert.AreEqual(0, state.allClubs.Count);
        }

        // ── nextPlayerId (design-decisions.md #31) ────────────────────

        [Test]
        public void NextPlayerId_DefaultsToOne()
        {
            var state = new GameState();
            Assert.AreEqual(1, state.nextPlayerId);
        }

        [Test]
        public void NextPlayerId_CanBeUpdatedAndReadBack()
        {
            var state = new GameState();
            state.nextPlayerId = 501;        // ClubGen 호출 후 500명 생성 시뮬
            Assert.AreEqual(501, state.nextPlayerId);
        }
    }
}
