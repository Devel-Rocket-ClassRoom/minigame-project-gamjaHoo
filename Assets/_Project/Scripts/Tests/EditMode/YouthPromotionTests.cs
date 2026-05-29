// YouthPromotionTests.cs
// DoD: algorithms.md V0.5 L.5 CheckPromotionCandidates / PromotePlayer / DeclinePromotion.

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class YouthPromotionTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 8, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.youthPromotionAge = 18;
            _balance.youthPromotionCaRatio = 0.70f;
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. 조건 충족 시 이벤트 발행 + pending 추가 ─────────────────

        [Test]
        public void T1_EligibleYouthPlayer_FiresEventAndAddsToPending()
        {
            var state = BuildState();
            var club = MakeClub(1);
            state.userClubId = 1;
            state.AddClub(club);

            // 시니어 3명 CA=60 → 평균 60. 임계 = 60 × 0.70 = 42
            AddSeniorPlayer(state, club, id: 10, ca: 60);
            AddSeniorPlayer(state, club, id: 11, ca: 60);
            AddSeniorPlayer(state, club, id: 12, ca: 60);

            // 유스 — 나이 18, CA 45 (> 42)
            var youth = MakeYouthPlayer(id: 1, ca: 45, birthYear: _today.Year - 18, club: club, state: state);

            YouthPromotionSuggestedEvent fired = null;
            EventBus.Subscribe<YouthPromotionSuggestedEvent>(e => fired = e);

            YouthSystem.CheckPromotionCandidates(state, _balance);

            Assert.IsNotNull(fired, "T1: YouthPromotionSuggestedEvent 발행됨");
            Assert.AreEqual(youth.id, fired.playerId, "T1: playerId 일치");
            Assert.AreEqual(club.id, fired.clubId, "T1: clubId 일치");
            Assert.Contains(youth.id, club.season.pendingPromotionPlayerIds, "T1: pending에 추가됨");
        }

        // ── T2. 나이 미달 (< 18) — 이벤트 없음 ─────────────────────────

        [Test]
        public void T2_TooYoung_NoEvent()
        {
            var state = BuildState();
            var club = MakeClub(1);
            state.userClubId = 1;
            state.AddClub(club);

            AddSeniorPlayer(state, club, id: 10, ca: 60);
            MakeYouthPlayer(id: 1, ca: 80, birthYear: _today.Year - 17, club: club, state: state);

            bool fired = false;
            EventBus.Subscribe<YouthPromotionSuggestedEvent>(_ => fired = true);

            YouthSystem.CheckPromotionCandidates(state, _balance);

            Assert.IsFalse(fired, "T2: 17세 — 이벤트 없음");
            Assert.IsEmpty(club.season.pendingPromotionPlayerIds, "T2: pending 비어있음");
        }

        // ── T3. CA 미달 (< 70% 평균) — 이벤트 없음 ─────────────────────

        [Test]
        public void T3_CaTooLow_NoEvent()
        {
            var state = BuildState();
            var club = MakeClub(1);
            state.userClubId = 1;
            state.AddClub(club);

            // 시니어 평균 CA=80 → 임계 = 56. 유스 CA = 50 (< 56)
            AddSeniorPlayer(state, club, id: 10, ca: 80);
            MakeYouthPlayer(id: 1, ca: 50, birthYear: _today.Year - 18, club: club, state: state);

            bool fired = false;
            EventBus.Subscribe<YouthPromotionSuggestedEvent>(_ => fired = true);

            YouthSystem.CheckPromotionCandidates(state, _balance);

            Assert.IsFalse(fired, "T3: CA 미달 — 이벤트 없음");
        }

        // ── T4. 이미 pending — 중복 추가 없음 ──────────────────────────

        [Test]
        public void T4_AlreadyPending_NotDuplicated()
        {
            var state = BuildState();
            var club = MakeClub(1);
            state.userClubId = 1;
            state.AddClub(club);

            AddSeniorPlayer(state, club, id: 10, ca: 60);
            var youth = MakeYouthPlayer(id: 1, ca: 50, birthYear: _today.Year - 18, club: club, state: state);
            club.season.pendingPromotionPlayerIds.Add(youth.id); // 이미 추가됨

            int eventCount = 0;
            EventBus.Subscribe<YouthPromotionSuggestedEvent>(_ => eventCount++);

            YouthSystem.CheckPromotionCandidates(state, _balance);

            Assert.AreEqual(0, eventCount, "T4: 중복 이벤트 없음");
            Assert.AreEqual(1, club.season.pendingPromotionPlayerIds.Count, "T4: pending 중복 없음");
        }

        // ── T5. PromotePlayer — 유스→시니어 이동 ────────────────────────

        [Test]
        public void T5_PromotePlayer_MovesToSeniorSquad()
        {
            var state = BuildState();
            var club = MakeClub(1);
            state.userClubId = 1;
            state.AddClub(club);

            var youth = MakeYouthPlayer(id: 1, ca: 50, birthYear: _today.Year - 18, club: club, state: state);
            club.season.pendingPromotionPlayerIds.Add(youth.id);

            YouthSystem.PromotePlayer(youth.id, state);

            Assert.IsFalse(club.youthSquadIds.Contains(youth.id), "T5: youthSquadIds에서 제거");
            Assert.IsTrue(club.seniorSquadIds.Contains(youth.id), "T5: seniorSquadIds에 추가");
            Assert.IsFalse(
                club.season.pendingPromotionPlayerIds.Contains(youth.id),
                "T5: pending에서 제거"
            );
        }

        // ── T6. DeclinePromotion — pending 제거, youth 유지 ─────────────

        [Test]
        public void T6_DeclinePromotion_RemovedFromPendingStaysInYouth()
        {
            var state = BuildState();
            var club = MakeClub(1);
            state.userClubId = 1;
            state.AddClub(club);

            var youth = MakeYouthPlayer(id: 1, ca: 50, birthYear: _today.Year - 18, club: club, state: state);
            club.season.pendingPromotionPlayerIds.Add(youth.id);

            YouthSystem.DeclinePromotion(youth.id, state);

            Assert.IsFalse(
                club.season.pendingPromotionPlayerIds.Contains(youth.id),
                "T6: pending에서 제거"
            );
            Assert.IsTrue(club.youthSquadIds.Contains(youth.id), "T6: youthSquad 유지");
        }

        // ── T7. 시니어 없음 — 임계 0 → CA 0 이상이면 통과 ──────────────

        [Test]
        public void T7_EmptySeniorSquad_ZeroThreshold_AnyCAQualifies()
        {
            var state = BuildState();
            var club = MakeClub(1);
            state.userClubId = 1;
            state.AddClub(club);

            // 시니어 없음 → avg = 0 → 임계 = 0
            MakeYouthPlayer(id: 1, ca: 1, birthYear: _today.Year - 18, club: club, state: state);

            bool fired = false;
            EventBus.Subscribe<YouthPromotionSuggestedEvent>(_ => fired = true);

            YouthSystem.CheckPromotionCandidates(state, _balance);

            Assert.IsTrue(fired, "T7: 시니어 없음 — CA≥1 유스는 이벤트 발행");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private GameState BuildState() =>
            new GameState
            {
                currentDate = _today,
                randomSeed = 1,
                nextPlayerId = 100,
                userClubId = -1,
            };

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

        private Player MakeYouthPlayer(int id, int ca, int birthYear, Club club, GameState state)
        {
            var player = new Player
            {
                id = id,
                currentClubId = club.id,
                currentAbility = ca,
                potentialAbility = ca + 20,
                info = new PersonalInfo
                {
                    birthDate = new DateTime(birthYear, _today.Month, _today.Day),
                    primaryPosition = Position.CM,
                    nationalityCode = "ENG",
                },
                contract = new Contract { endDate = _today.AddYears(2) },
                state = new PlayerState(),
            };
            state.AddPlayer(player);
            club.youthSquadIds.Add(id);
            return player;
        }

        private void AddSeniorPlayer(GameState state, Club club, int id, int ca)
        {
            var player = new Player
            {
                id = id,
                currentClubId = club.id,
                currentAbility = ca,
                potentialAbility = ca,
                info = new PersonalInfo
                {
                    birthDate = _today.AddYears(-25),
                    primaryPosition = Position.CM,
                    nationalityCode = "ENG",
                },
                contract = new Contract { endDate = _today.AddYears(2) },
                state = new PlayerState(),
            };
            state.AddPlayer(player);
            club.seniorSquadIds.Add(id);
        }
    }
}
