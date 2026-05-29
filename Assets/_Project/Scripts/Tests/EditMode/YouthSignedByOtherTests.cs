// YouthSignedByOtherTests.cs
// DoD: algorithms.md V0.5 L.6 — 미영입 후보 일부 AI 다른 구단 영입.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class YouthSignedByOtherTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 8, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.youthRejectedToOtherClubRatio = 1.0f; // 테스트용: 100% AI 영입
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. ratio=1.0 — 미영입 전원 AI 다른 구단 영입 + 이벤트 발행 ─

        [Test]
        public void T1_AllRejectedSignedByOther_WhenRatioIs1()
        {
            var (state, userClub, otherClub) = BuildTwoClubScenario();
            var intake = BuildIntake(state, userClub, candidateCount: 4);

            // 유저는 1명만 영입, 나머지 3명 미영입
            var signId = intake.candidatePlayerIds[0];
            var rejectedIds = intake.candidatePlayerIds.Skip(1).ToList();

            var firedEvents = new List<YouthSignedByOtherEvent>();
            EventBus.Subscribe<YouthSignedByOtherEvent>(e => firedEvents.Add(e));

            YouthSystem.SignPlayers(intake, new List<int> { signId }, userClub, state, _balance);

            Assert.AreEqual(3, firedEvents.Count, "T1: 미영입 3명 → 이벤트 3개");
            foreach (var id in rejectedIds)
            {
                Assert.IsNotNull(state.GetPlayer(id), $"T1: AI 영입자 id={id} GameState 유지");
                Assert.Contains(id, intake.rejectedPlayerIds, $"T1: rejectedPlayerIds 보존 id={id}");
                Assert.AreEqual(otherClub.id, state.GetPlayer(id).currentClubId, $"T1: otherClub 소속");
                Assert.Contains(id, otherClub.youthSquadIds, $"T1: otherClub.youthSquadIds 추가");
            }
        }

        // ── T2. ratio=0 — 미영입 전원 제거 (기존 V0.1 동작 유지) ────────

        [Test]
        public void T2_ZeroRatio_AllRejectedRemoved()
        {
            _balance.youthRejectedToOtherClubRatio = 0f;
            var (state, userClub, _) = BuildTwoClubScenario();
            var intake = BuildIntake(state, userClub, candidateCount: 4);

            var signId = intake.candidatePlayerIds[0];
            var rejectedIds = intake.candidatePlayerIds.Skip(1).ToList();

            bool anyFired = false;
            EventBus.Subscribe<YouthSignedByOtherEvent>(_ => anyFired = true);

            YouthSystem.SignPlayers(intake, new List<int> { signId }, userClub, state, _balance);

            Assert.IsFalse(anyFired, "T2: ratio=0 → 이벤트 없음");
            foreach (var id in rejectedIds)
                Assert.IsNull(state.GetPlayer(id), $"T2: 미영입 id={id} GameState 제거");
        }

        // ── T3. 다른 구단 없음 → AI 영입 불가, 전원 제거 ────────────────

        [Test]
        public void T3_NoOtherClubs_AllRejectedRemoved()
        {
            var state = new GameState { currentDate = _today, randomSeed = 1, nextPlayerId = 1 };
            var userClub = MakeClub(1);
            state.AddClub(userClub); // 구단 1개만

            var intake = BuildIntake(state, userClub, candidateCount: 3);
            var signId = intake.candidatePlayerIds[0];
            var rejectedIds = intake.candidatePlayerIds.Skip(1).ToList();

            bool anyFired = false;
            EventBus.Subscribe<YouthSignedByOtherEvent>(_ => anyFired = true);

            YouthSystem.SignPlayers(intake, new List<int> { signId }, userClub, state, _balance);

            Assert.IsFalse(anyFired, "T3: 다른 구단 없음 → 이벤트 없음");
            foreach (var id in rejectedIds)
                Assert.IsNull(state.GetPlayer(id), $"T3: 미영입 제거 id={id}");
        }

        // ── T4. rejectedPlayerIds 는 AI 영입 여부 관계없이 항상 보존 ────

        [Test]
        public void T4_RejectedPlayerIds_AlwaysPreserved()
        {
            var (state, userClub, _) = BuildTwoClubScenario();
            _balance.youthRejectedToOtherClubRatio = 0.5f;
            var intake = BuildIntake(state, userClub, candidateCount: 6);

            var signId = intake.candidatePlayerIds[0];
            var allRejected = intake.candidatePlayerIds.Skip(1).ToList();

            YouthSystem.SignPlayers(intake, new List<int> { signId }, userClub, state, _balance);

            foreach (var id in allRejected)
                Assert.Contains(id, intake.rejectedPlayerIds, $"T4: rejectedPlayerIds 보존 id={id}");
            Assert.IsEmpty(intake.candidatePlayerIds, "T4: candidatePlayerIds 비워짐");
        }

        // ── T5. balance=null → ratio=0, 기존 동작 (하위 호환) ──────────

        [Test]
        public void T5_NullBalance_FallsBackToRemoveAll()
        {
            var (state, userClub, _) = BuildTwoClubScenario();
            var intake = BuildIntake(state, userClub, candidateCount: 3);

            var signId = intake.candidatePlayerIds[0];
            var rejectedIds = intake.candidatePlayerIds.Skip(1).ToList();

            bool anyFired = false;
            EventBus.Subscribe<YouthSignedByOtherEvent>(_ => anyFired = true);

            YouthSystem.SignPlayers(intake, new List<int> { signId }, userClub, state, balance: null);

            Assert.IsFalse(anyFired, "T5: balance=null → AI 영입 없음");
            foreach (var id in rejectedIds)
                Assert.IsNull(state.GetPlayer(id), $"T5: 미영입 제거 id={id}");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private (GameState state, Club userClub, Club otherClub) BuildTwoClubScenario()
        {
            var state = new GameState { currentDate = _today, randomSeed = 42, nextPlayerId = 1 };
            var userClub = MakeClub(1);
            var otherClub = MakeClub(2);
            state.AddClub(userClub);
            state.AddClub(otherClub);
            return (state, userClub, otherClub);
        }

        private YouthIntake BuildIntake(GameState state, Club club, int candidateCount)
        {
            var intake = new YouthIntake
            {
                id = 1,
                clubId = club.id,
                intakeDate = _today,
                candidatePlayerIds = new List<int>(),
                signedPlayerIds = new List<int>(),
                rejectedPlayerIds = new List<int>(),
                rerollsUsed = 0,
            };

            for (int i = 0; i < candidateCount; i++)
            {
                int id = state.nextPlayerId++;
                var player = new Player
                {
                    id = id,
                    currentClubId = -1,
                    currentAbility = 50,
                    potentialAbility = 70,
                    info = new PersonalInfo
                    {
                        birthDate = _today.AddYears(-17),
                        primaryPosition = Position.CM,
                        nationalityCode = "ENG",
                    },
                    contract = new Contract { endDate = _today.AddYears(2) },
                    state = new PlayerState(),
                };
                state.AddPlayer(player);
                intake.candidatePlayerIds.Add(id);
            }

            club.intakeHistory.Add(intake);
            return intake;
        }

        private static Club MakeClub(int id) =>
            new Club
            {
                id = id,
                name = $"Club{id}",
                facilities = new Facilities(),
                finance = new Finance(),
                season = new SeasonState(),
                seniorSquadIds = new List<int>(),
                youthSquadIds = new List<int>(),
                intakeHistory = new List<YouthIntake>(),
            };
    }
}
