// FreeAgentContractTests.cs
// V1.0 H.3 — TransferSystem.SubmitFreeAgentContract DoD 검증.
// DoD: FA 영입 시나리오 — 잔여 6개월 이내 선수 → 이적료 0 + 즉시 Accepted.
// algorithms.md V1.0-3.1 / design-decisions.md #48.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class FreeAgentContractTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 11, 1);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.wageBaseAtMinCA = 500;
            _balance.wagePerCAPoint = 350f;
            _balance.wageFloor = 500;
            _balance.minCA = 30;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        // ── T1. 잔여 ≤180일 → status=Accepted, amount=0 (DoD) ────────

        [Test]
        public void T1_Within180Days_AcceptedWithZeroFee()
        {
            var (state, c1, c2) = BuildState();
            var player = NewPlayer(1, daysLeft: 90);
            player.currentClubId = c1.id;
            state.AddPlayer(player);

            int eventCount = 0;
            EventBus.Subscribe<OfferSubmittedEvent>(_ => eventCount++);

            var proposed = NewContract();
            var offer = TransferSystem.SubmitFreeAgentContract(1, c2.id, proposed, state, _balance);

            Assert.AreEqual(OfferStatus.Accepted, offer.status, "T1: 즉시 Accepted");
            Assert.AreEqual(0, offer.amount, "T1: 이적료 0");
            Assert.AreEqual(1, eventCount, "T1: OfferSubmittedEvent 발행");
            Assert.AreEqual(c1.id, offer.fromClubId);
            Assert.AreEqual(c2.id, offer.toClubId);
        }

        // ── T2. 잔여 >180일 → ArgumentException ─────────────────────

        [Test]
        public void T2_Over180Days_ThrowsArgumentException()
        {
            var (state, c1, c2) = BuildState();
            var player = NewPlayer(1, daysLeft: 181);
            player.currentClubId = c1.id;
            state.AddPlayer(player);

            Assert.Throws<ArgumentException>(() =>
                TransferSystem.SubmitFreeAgentContract(1, c2.id, NewContract(), state, _balance)
            );
        }

        // ── T3. 딱 180일 → 허용 ──────────────────────────────────────

        [Test]
        public void T3_Exactly180Days_Allowed()
        {
            var (state, c1, c2) = BuildState();
            var player = NewPlayer(1, daysLeft: 180);
            player.currentClubId = c1.id;
            state.AddPlayer(player);

            Assert.DoesNotThrow(() =>
                TransferSystem.SubmitFreeAgentContract(1, c2.id, NewContract(), state, _balance)
            );
        }

        // ── T4. null contract → ArgumentNullException ─────────────

        [Test]
        public void T4_NullContract_Throws()
        {
            var (state, c1, c2) = BuildState();
            var player = NewPlayer(1, daysLeft: 90);
            player.currentClubId = c1.id;
            state.AddPlayer(player);

            Assert.Throws<ArgumentNullException>(() =>
                TransferSystem.SubmitFreeAgentContract(1, c2.id, null!, state, _balance)
            );
        }

        // ── T5. 미존재 선수 → ArgumentException ──────────────────────

        [Test]
        public void T5_PlayerNotFound_Throws()
        {
            var (state, _, c2) = BuildState();

            Assert.Throws<ArgumentException>(() =>
                TransferSystem.SubmitFreeAgentContract(999, c2.id, NewContract(), state, _balance)
            );
        }

        // ── T6. 이미 같은 클럽 → ArgumentException ───────────────────

        [Test]
        public void T6_SameClub_Throws()
        {
            var (state, c1, _) = BuildState();
            var player = NewPlayer(1, daysLeft: 90);
            player.currentClubId = c1.id;
            state.AddPlayer(player);

            Assert.Throws<ArgumentException>(() =>
                TransferSystem.SubmitFreeAgentContract(1, c1.id, NewContract(), state, _balance)
            );
        }

        // ── Helpers ──────────────────────────────────────────────────

        private (GameState state, Club c1, Club c2) BuildState()
        {
            var state = new GameState
            {
                currentDate = _today,
                randomSeed = 42,
                userClubId = 1,
                nextPlayerId = 10,
                nextIntakeId = 1,
                nextOfferId = 1,
            };
            var c1 = new Club
            {
                id = 1,
                name = "CurrentClub",
                reputation = 60,
                leagueId = 1,
                finance = new Finance { money = 10_000_000 },
            };
            var c2 = new Club
            {
                id = 2,
                name = "BiddingClub",
                reputation = 70,
                leagueId = 1,
                finance = new Finance { money = 10_000_000 },
            };
            state.AddClub(c1);
            state.AddClub(c2);
            return (state, c1, c2);
        }

        private Player NewPlayer(int id, int daysLeft)
        {
            return new Player
            {
                id = id,
                currentAbility = 80,
                potentialAbility = 90,
                currentClubId = 1,
                info = new PersonalInfo
                {
                    firstName = "Test",
                    lastName = "Player",
                    birthDate = _today.AddYears(-25),
                    primaryPosition = Position.CM,
                },
                state = new PlayerState
                {
                    morale = 50,
                    happiness = 50,
                    fatigue = 0,
                    form = 50,
                    injury = new InjuryInfo { injuryTypeId = -1 },
                },
                contract = new Contract
                {
                    weeklyWage = 20_000,
                    startDate = _today.AddYears(-1),
                    endDate = _today.AddDays(daysLeft),
                },
            };
        }

        private Contract NewContract() =>
            new Contract
            {
                weeklyWage = 25_000,
                startDate = _today.AddDays(180),
                endDate = _today.AddDays(180).AddYears(3),
            };
    }
}
