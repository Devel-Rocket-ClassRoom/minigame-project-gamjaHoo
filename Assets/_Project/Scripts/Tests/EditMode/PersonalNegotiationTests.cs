// PersonalNegotiationTests.cs
// V1.0 #469 — RespondToPersonalTerms DoD 검증 (Negotiating 단계 인터랙티브 협상).
// 수락→Accepted / 거절→StillNegotiating(round++) / max 초과→Rejected / 비-Negotiating 예외 / 결정성.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class PersonalNegotiationTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 7, 1);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.wageBaseAtMinCA = 500;
            _balance.wagePerCAPoint = 350f;
            _balance.wageFloor = 500;
            _balance.minCA = 30;
            _balance.playtimeAgreementBonus = 0.2f;
            _balance.maxPersonalNegotiationRounds = 3;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        // ── T1. 고임금 제안 → Accepted ───────────────────────────────

        [Test]
        public void T1_HighWage_Accepted()
        {
            var (state, c1, c2) = BuildState(42);
            var offer = MakeNegotiating(state, c1, c2);

            OfferRespondedEvent? published = null;
            EventBus.Subscribe<OfferRespondedEvent>(e => published = e);

            var result = TransferSystem.RespondToPersonalTerms(
                offer.id,
                WageContract(state, 200_000),
                includesPlaytimeAgreement: false,
                state,
                _balance
            );

            Assert.AreEqual(PersonalTermsResult.Accepted, result, "T1: 고임금 → Accepted 결과");
            Assert.AreEqual(OfferStatus.Accepted, offer.status, "T1: status Accepted");
            Assert.AreEqual(200_000, offer.proposed.weeklyWage, "T1: proposed 반영");
            Assert.IsNotNull(published);
            Assert.AreEqual(OfferStatus.Accepted, published!.newStatus, "T1: 이벤트 Accepted");
        }

        // ── T2. 저임금 1회 제안 → StillNegotiating + round++ ─────────

        [Test]
        public void T2_LowWage_StillNegotiating_RoundIncrements()
        {
            var (state, c1, c2) = BuildState(42);
            var offer = MakeNegotiating(state, c1, c2);

            var result = TransferSystem.RespondToPersonalTerms(
                offer.id,
                WageContract(state, 1),
                includesPlaytimeAgreement: false,
                state,
                _balance
            );

            Assert.AreEqual(
                PersonalTermsResult.StillNegotiating,
                result,
                "T2: 저임금 → StillNegotiating"
            );
            Assert.AreEqual(OfferStatus.Negotiating, offer.status, "T2: status 유지");
            Assert.AreEqual(1, offer.personalNegotiationRound, "T2: round=1");
        }

        // ── T3. 최대 라운드 초과 → Rejected ──────────────────────────

        [Test]
        public void T3_MaxRoundsExceeded_Rejected()
        {
            var (state, c1, c2) = BuildState(42);
            var offer = MakeNegotiating(state, c1, c2);

            PersonalTermsResult last = PersonalTermsResult.StillNegotiating;
            for (int i = 0; i < _balance.maxPersonalNegotiationRounds; i++)
                last = TransferSystem.RespondToPersonalTerms(
                    offer.id,
                    WageContract(state, 1), // 거절 유도 저임금
                    includesPlaytimeAgreement: false,
                    state,
                    _balance
                );

            Assert.AreEqual(PersonalTermsResult.Rejected, last, "T3: 최대 라운드 → Rejected");
            Assert.AreEqual(OfferStatus.Rejected, offer.status, "T3: status Rejected");
        }

        // ── T4. 재제안 인상 → 결국 수락 (반복 협상) ──────────────────

        [Test]
        public void T4_ReProposeHigher_EventuallyAccepts()
        {
            var (state, c1, c2) = BuildState(7);
            var offer = MakeNegotiating(state, c1, c2);

            // 저임금 1회(거절) 후 고임금 재제안 → 수락
            TransferSystem.RespondToPersonalTerms(
                offer.id,
                WageContract(state, 1),
                false,
                state,
                _balance
            );
            Assert.AreEqual(
                OfferStatus.Negotiating,
                offer.status,
                "T4: 1차 거절 후 Negotiating 유지"
            );

            var result = TransferSystem.RespondToPersonalTerms(
                offer.id,
                WageContract(state, 300_000),
                false,
                state,
                _balance
            );
            Assert.AreEqual(PersonalTermsResult.Accepted, result, "T4: 고임금 재제안 → Accepted");
        }

        // ── T5. 비-Negotiating 상태 → 예외 ───────────────────────────

        [Test]
        public void T5_NotNegotiatingStatus_Throws()
        {
            var (state, c1, c2) = BuildState(42);
            var offer = MakeNegotiating(state, c1, c2);
            offer.status = OfferStatus.Pending;

            Assert.Throws<InvalidOperationException>(() =>
                TransferSystem.RespondToPersonalTerms(
                    offer.id,
                    WageContract(state, 200_000),
                    false,
                    state,
                    _balance
                )
            );
        }

        // ── T6. 결정성 — 같은 시드 + 같은 조건 → 같은 결과 ───────────

        [Test]
        public void T6_Deterministic_SameSeedSameResult()
        {
            PersonalTermsResult Run(int seed)
            {
                EventBus.Clear();
                var (state, c1, c2) = BuildState(seed);
                var offer = MakeNegotiating(state, c1, c2);
                return TransferSystem.RespondToPersonalTerms(
                    offer.id,
                    WageContract(state, 26_000), // borderline
                    false,
                    state,
                    _balance
                );
            }

            Assert.AreEqual(Run(11), Run(11), "T6: 같은 시드 → 같은 결과");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private (GameState state, Club c1, Club c2) BuildState(int seed)
        {
            var state = new GameState
            {
                currentDate = _today,
                randomSeed = seed,
                userClubId = 2,
                nextPlayerId = 100,
                nextOfferId = 1,
            };
            var c1 = new Club
            {
                id = 1,
                name = "FromClub",
                leagueId = 1,
                finance = new Finance(),
            };
            var c2 = new Club
            {
                id = 2,
                name = "ToClub",
                leagueId = 1,
                finance = new Finance(),
            };
            state.AddClub(c1);
            state.AddClub(c2);
            return (state, c1, c2);
        }

        private Player NewPlayer(int id, int clubId) =>
            new Player
            {
                id = id,
                currentAbility = 100,
                potentialAbility = 110,
                currentClubId = clubId,
                info = new PersonalInfo
                {
                    firstName = "T",
                    lastName = "P",
                    birthDate = _today.AddYears(-25),
                    primaryPosition = Position.CM,
                },
                state = new PlayerState
                {
                    morale = 50,
                    form = 50,
                    injury = new InjuryInfo { injuryTypeId = -1 },
                },
                contract = new Contract
                {
                    weeklyWage = 25_000,
                    startDate = _today.AddYears(-1),
                    endDate = _today.AddYears(2),
                },
                hiddenAttrs = new HiddenAttributes { loyalty = 50, ambition = 50 },
            };

        // Negotiating 상태 오퍼 직접 생성 (구단 이적료 합의 완료 가정).
        private TransferOffer MakeNegotiating(GameState state, Club c1, Club c2)
        {
            var p = NewPlayer(1, c1.id);
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            var offer = new TransferOffer
            {
                id = state.nextOfferId++,
                playerId = 1,
                fromClubId = c1.id,
                toClubId = c2.id,
                amount = 5_000_000,
                status = OfferStatus.Negotiating,
            };
            state.activeOffers.Add(offer);
            return offer;
        }

        private Contract WageContract(GameState state, int weeklyWage) =>
            new Contract
            {
                weeklyWage = weeklyWage,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(3),
            };
    }
}
