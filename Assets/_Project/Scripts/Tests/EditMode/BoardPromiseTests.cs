// BoardPromiseTests.cs
// DoD: v0.5-tasks.md M.5 — 보드 약속 생성 + 거절 패널티

using System;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class BoardPromiseTests
    {
        private GameBalanceSO _balance;
        private GameState _state;
        private Club _userClub;

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();

            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.boardPromiseRejectPenalty = 10;
            _balance.boardSackedThreshold = 10;
            _balance.boardWarningThreshold = 30;
            _balance.transferWindowSummerEndMonth = 8;
            _balance.transferWindowSummerEndDay = 31;

            _state = new GameState { currentDate = new DateTime(2025, 6, 1) };

            _userClub = new Club
            {
                id = 1,
                name = "UserClub",
                reputation = 60,
                season = new SeasonState { boardConfidence = 50 },
            };
            _state.AddClub(_userClub);
            _state.userClubId = _userClub.id;
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. 약속 생성 — PendingReview 상태로 추가 ────────────────────

        [Test]
        public void T1_GenerateSeasonPromises_AddsPendingPromise()
        {
            AddSquadPlayers(gk: 1, df: 4, mf: 4, at: 2);

            BoardSystem.GenerateSeasonPromises(_state, _balance);

            Assert.AreEqual(1, _userClub.season.boardPromises.Count, "T1: 약속 1개 생성");
            Assert.AreEqual(
                BoardPromiseStatus.PendingReview,
                _userClub.season.boardPromises[0].status,
                "T1: 상태 PendingReview"
            );
            Assert.AreEqual(
                BoardPromiseType.TransferIn,
                _userClub.season.boardPromises[0].type,
                "T1: 타입 TransferIn"
            );
        }

        // ── T2. 중복 생성 방지 ────────────────────────────────────────────

        [Test]
        public void T2_GenerateSeasonPromises_NoDuplicateWhenPendingExists()
        {
            AddSquadPlayers(gk: 1, df: 4, mf: 4, at: 2);
            BoardSystem.GenerateSeasonPromises(_state, _balance);
            BoardSystem.GenerateSeasonPromises(_state, _balance);

            Assert.AreEqual(1, _userClub.season.boardPromises.Count, "T2: 중복 생성 안 함");
        }

        // ── T3. 약점 포지션 — GK 없을 때 GK 요구 ────────────────────────

        [Test]
        public void T3_WeakestPosition_NoGK_RequestsGK()
        {
            AddSquadPlayers(gk: 0, df: 4, mf: 4, at: 2);

            BoardSystem.GenerateSeasonPromises(_state, _balance);

            var promise = _userClub.season.boardPromises[0];
            Assert.AreEqual(Position.GK, promise.targetPosition, "T3: GK 0명 → GK 요구");
        }

        // ── T4. 약점 포지션 — AT 부족 시 ST 요구 ────────────────────────

        [Test]
        public void T4_WeakestPosition_FewAttackers_RequestsST()
        {
            AddSquadPlayers(gk: 1, df: 4, mf: 4, at: 0);

            BoardSystem.GenerateSeasonPromises(_state, _balance);

            var promise = _userClub.season.boardPromises[0];
            Assert.AreEqual(Position.ST, promise.targetPosition, "T4: AT 0명 → ST 요구");
        }

        // ── T5. 거절 시 boardConfidence -10 ─────────────────────────────

        [Test]
        public void T5_RejectPromise_ReducesConfidence()
        {
            AddSquadPlayers(gk: 1, df: 4, mf: 4, at: 2);
            BoardSystem.GenerateSeasonPromises(_state, _balance);
            var promise = _userClub.season.boardPromises[0];

            BoardSystem.RejectPromise(_state, _balance, promise);

            Assert.AreEqual(40, _userClub.season.boardConfidence, "T5: 거절 → confidence -10");
            Assert.AreEqual(BoardPromiseStatus.Broken, promise.status, "T5: 상태 Broken");
        }

        // ── T6. 거절 후 BoardWarningEvent 발행 ───────────────────────────

        [Test]
        public void T6_RejectPromise_LowConfidence_FiresWarning()
        {
            _userClub.season.boardConfidence = 35;
            AddSquadPlayers(gk: 1, df: 4, mf: 4, at: 2);
            BoardSystem.GenerateSeasonPromises(_state, _balance);
            var promise = _userClub.season.boardPromises[0];

            BoardWarningEvent received = null;
            EventBus.Subscribe<BoardWarningEvent>(e => received = e);

            BoardSystem.RejectPromise(_state, _balance, promise);

            Assert.IsNotNull(received, "T6: 거절 후 confidence < 30 → BoardWarningEvent");
        }

        // ── T7. 마감일 — 8/31 설정 ───────────────────────────────────────

        [Test]
        public void T7_GeneratedPromise_DeadlineIsTransferWindowEnd()
        {
            AddSquadPlayers(gk: 1, df: 4, mf: 4, at: 2);
            BoardSystem.GenerateSeasonPromises(_state, _balance);

            var promise = _userClub.season.boardPromises[0];
            Assert.AreEqual(8, promise.deadline.Month, "T7: deadline 월 = 8");
            Assert.AreEqual(31, promise.deadline.Day, "T7: deadline 일 = 31");
        }

        // ── T8. 비유저 구단은 약속 미생성 ────────────────────────────────

        [Test]
        public void T8_NonUserClub_NoPromiseGenerated()
        {
            var otherClub = new Club
            {
                id = 2,
                name = "OtherClub",
                season = new SeasonState(),
            };
            _state.AddClub(otherClub);
            // userClubId = 1 (otherClub = 2)
            AddSquadPlayers(gk: 0, df: 0, mf: 0, at: 0); // user squad 비어있어도 됨

            BoardSystem.GenerateSeasonPromises(_state, _balance);

            Assert.AreEqual(0, otherClub.season.boardPromises.Count, "T8: 비유저 구단 약속 없음");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private void AddSquadPlayers(int gk, int df, int mf, int at)
        {
            int id = 100;
            AddPlayers(ref id, gk, Position.GK);
            AddPlayers(ref id, df, Position.CB);
            AddPlayers(ref id, mf, Position.CM);
            AddPlayers(ref id, at, Position.ST);
        }

        private void AddPlayers(ref int id, int count, Position pos)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new Player
                {
                    id = id++,
                    info = new PersonalInfo { primaryPosition = pos },
                    state = new PlayerState(),
                    currentClubId = _userClub.id,
                };
                _userClub.seniorSquadIds.Add(p.id);
                _state.AddPlayer(p);
            }
        }
    }
}
