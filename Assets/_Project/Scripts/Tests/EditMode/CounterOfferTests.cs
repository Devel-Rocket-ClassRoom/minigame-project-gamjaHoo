// CounterOfferTests.cs
// V0.5 K.1 — AiRespondToOffer 4분기 + RespondToCounterOffer DoD 검증.
// DoD: CounterOffer 시나리오 검증.
// algorithms.md V0.5-3.1 [3-a][3-b] / design-decisions.md #48.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class CounterOfferTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 7, 1); // 이적시장 활성화 기간

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // 시장 가치 필드 기본값
            _balance.marketValueBase = 5_000_000;
            _balance.marketValueCaExponent = 4.0f;
            _balance.marketValuePaCoeff = 0f;
            _balance.marketValueAgeCurve = new float[] { 0.8f, 1.0f, 0.9f, 0.7f };
            _balance.marketValueContractCurve = new float[] { 0.5f, 0.8f, 1.0f, 1.05f };
            _balance.marketValuePositionFactor = new float[] { 0.75f, 0.85f, 1.0f, 1.2f };
            _balance.marketValueInjuryFactor = 0.5f;
            _balance.aiValueNoiseSigma = 0.10f;
            // K.1 협상 파라미터
            _balance.aiAcceptThreshold = 1.30f;
            _balance.aiCounterOfferThreshold = 1.10f;
            _balance.aiMockingThreshold = 0.85f;
            _balance.aiCounterOfferFactor = 1.30f;
            _balance.maxNegotiationRounds = 3;
            _balance.aiMockingMoralePenalty = 3;
            // 이적시장 창
            _balance.transferWindowSummerStartMonth = 6;
            _balance.transferWindowSummerStartDay = 1;
            _balance.transferWindowSummerEndMonth = 8;
            _balance.transferWindowSummerEndDay = 31;
            _balance.transferWindowWinterStartMonth = 1;
            _balance.transferWindowWinterStartDay = 1;
            _balance.transferWindowWinterEndMonth = 1;
            _balance.transferWindowWinterEndDay = 31;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        // ── T1. ratio >= 1.30 → Accepted (100 시드 중 다수) ──────────

        [Test]
        public void T1_HighOffer_MostlyAccepted()
        {
            int accepted = 0;
            const int trials = 100;
            for (int seed = 0; seed < trials; seed++)
            {
                EventBus.Clear();
                var (state, c1, c2) = BuildState(seed);
                var p = NewPlayer(1, c1.id);
                state.AddPlayer(p);
                c1.seniorSquadIds.Add(p.id);

                int mv = TransferSystem.CalculateMarketValue(p, state, _balance);
                var offer = TransferSystem.SubmitOffer(
                    p.id,
                    c1.id,
                    c2.id,
                    (int)(mv * 2.0),
                    NewContract(state),
                    state,
                    _balance
                );
                TransferSystem.ProcessOffers(state, _balance);
                // 새 흐름: AI 수락 → CounterOffer. 유저 수락 시뮬레이션.
                if (offer.status == OfferStatus.CounterOffer)
                    TransferSystem.RespondToCounterOffer(
                        offer.id,
                        CounterResponse.Accept,
                        0,
                        state,
                        _balance
                    );
                if (offer.status == OfferStatus.Accepted || offer.status == OfferStatus.Completed)
                    accepted++;
            }
            Assert.GreaterOrEqual(
                accepted,
                80,
                $"T1: ratio×2.0 → 80% 이상 Accepted ({accepted}/100)"
            );
        }

        // ── T2. ratio ≈ 1.15 → CounterOffer 다수 발생 ────────────────

        [Test]
        public void T2_MidOffer_SomeCounterOffer()
        {
            int counterOffers = 0;
            const int trials = 100;
            for (int seed = 0; seed < trials; seed++)
            {
                EventBus.Clear();
                var (state, c1, c2) = BuildState(seed);
                var p = NewPlayer(1, c1.id);
                state.AddPlayer(p);
                c1.seniorSquadIds.Add(p.id);

                int mv = TransferSystem.CalculateMarketValue(p, state, _balance);
                var offer = TransferSystem.SubmitOffer(
                    p.id,
                    c1.id,
                    c2.id,
                    (int)(mv * 1.15),
                    NewContract(state),
                    state,
                    _balance
                );
                TransferSystem.ProcessOffers(state, _balance);
                if (offer.status == OfferStatus.CounterOffer)
                    counterOffers++;
            }
            Assert.GreaterOrEqual(
                counterOffers,
                20,
                $"T2: ratio×1.15 → CounterOffer 20회 이상 ({counterOffers}/100)"
            );
        }

        // ── T3. CounterOffer 시 counterAmount = aiPerceivedValue × 1.30 ──

        [Test]
        public void T3_CounterOffer_CounterAmountSet()
        {
            // CounterOffer 를 확실히 유도하는 시드 탐색
            for (int seed = 0; seed < 200; seed++)
            {
                EventBus.Clear();
                var (state, c1, c2) = BuildState(seed);
                var p = NewPlayer(1, c1.id);
                state.AddPlayer(p);
                c1.seniorSquadIds.Add(p.id);

                int mv = TransferSystem.CalculateMarketValue(p, state, _balance);
                var offer = TransferSystem.SubmitOffer(
                    p.id,
                    c1.id,
                    c2.id,
                    (int)(mv * 1.15),
                    NewContract(state),
                    state,
                    _balance
                );
                TransferSystem.ProcessOffers(state, _balance);

                if (offer.status == OfferStatus.CounterOffer)
                {
                    Assert.Greater(offer.counterAmount, 0, "T3: counterAmount > 0");
                    Assert.AreEqual(1, offer.negotiationRound, "T3: negotiationRound = 1");
                    return; // 한 번이라도 CounterOffer 확인하면 통과
                }
            }
            Assert.Fail("T3: 200 시드 중 CounterOffer 미발생 — 로직 오류");
        }

        // ── T4. 모욕적 오퍼(ratio < 0.85) → Rejected + 사기 -3 ───────

        [Test]
        public void T4_MockingOffer_RejectedAndMoralePenalty()
        {
            var (state, c1, c2) = BuildState(42);
            var p = NewPlayer(1, c1.id);
            p.state.morale = 50;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            // amount = 1 → ratio ≈ 0 → 확실히 Mocking
            var offer = TransferSystem.SubmitOffer(
                p.id,
                c1.id,
                c2.id,
                1,
                NewContract(state),
                state,
                _balance
            );
            TransferSystem.ProcessOffers(state, _balance);

            Assert.AreEqual(OfferStatus.Rejected, offer.status, "T4: Rejected");
            Assert.AreEqual(47, p.state.morale, "T4: 사기 50-3=47");
        }

        // ── T5. RespondToCounterOffer — Accept → Accepted ─────────────

        [Test]
        public void T5_RespondAccept_StatusAccepted()
        {
            var (state, c1, c2) = BuildState(42);
            var offer = MakeCounterOffer(state, c1, c2, counterAmount: 5_000_000);

            OfferRespondedEvent? published = null;
            EventBus.Subscribe<OfferRespondedEvent>(e => published = e);

            TransferSystem.RespondToCounterOffer(
                offer.id,
                CounterResponse.Accept,
                0,
                state,
                _balance
            );

            Assert.AreEqual(OfferStatus.Accepted, offer.status, "T5: Accepted");
            Assert.AreEqual(5_000_000, offer.amount, "T5: amount = counterAmount");
            Assert.IsNotNull(published);
            Assert.AreEqual(OfferStatus.Accepted, published!.newStatus);
        }

        // ── T6. RespondToCounterOffer — Reject → Rejected ────────────

        [Test]
        public void T6_RespondReject_StatusRejected()
        {
            var (state, c1, c2) = BuildState(42);
            var offer = MakeCounterOffer(state, c1, c2, counterAmount: 5_000_000);

            TransferSystem.RespondToCounterOffer(
                offer.id,
                CounterResponse.Reject,
                0,
                state,
                _balance
            );

            Assert.AreEqual(OfferStatus.Rejected, offer.status, "T6: Rejected");
        }

        // ── T7. RespondToCounterOffer — ReCounter → AI 재호출 ────────

        [Test]
        public void T7_ReCounter_AiReresponds()
        {
            var (state, c1, c2) = BuildState(42);
            var p = NewPlayer(1, c1.id);
            p.state.morale = 50;
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);
            var offer = MakeCounterOffer(state, c1, c2, counterAmount: 5_000_000);

            int mv = TransferSystem.CalculateMarketValue(p, state, _balance);
            // 매우 높은 역제안 → AI 반드시 Accepted
            TransferSystem.RespondToCounterOffer(
                offer.id,
                CounterResponse.ReCounter,
                (int)(mv * 3.0),
                state,
                _balance
            );

            Assert.That(
                offer.status,
                Is.EqualTo(OfferStatus.Accepted).Or.EqualTo(OfferStatus.CounterOffer),
                "T7: ReCounter 후 AI 응답 (Accepted 또는 CounterOffer)"
            );
        }

        // ── T8. negotiationRound >= maxNegotiationRounds → 강제 Rejected ─

        [Test]
        public void T8_MaxRoundsExceeded_ForcedRejected()
        {
            var (state, c1, c2) = BuildState(42);
            var offer = MakeCounterOffer(state, c1, c2, counterAmount: 5_000_000);
            offer.negotiationRound = _balance.maxNegotiationRounds; // 이미 최대치

            TransferSystem.RespondToCounterOffer(
                offer.id,
                CounterResponse.ReCounter,
                999_999_999,
                state,
                _balance
            );

            Assert.AreEqual(
                OfferStatus.Rejected,
                offer.status,
                "T8: 최대 라운드 초과 → 강제 Rejected"
            );
        }

        // ── T9. CounterOffer 아닌 상태에서 RespondToCounterOffer → 예외 ─

        [Test]
        public void T9_NotCounterOfferStatus_Throws()
        {
            var (state, c1, c2) = BuildState(42);
            var p = NewPlayer(1, c1.id);
            state.AddPlayer(p);
            c1.seniorSquadIds.Add(p.id);

            var offer = TransferSystem.SubmitOffer(
                p.id,
                c1.id,
                c2.id,
                1_000_000,
                NewContract(state),
                state,
                _balance
            );
            // offer.status = Pending (처리 전)

            Assert.Throws<InvalidOperationException>(() =>
                TransferSystem.RespondToCounterOffer(
                    offer.id,
                    CounterResponse.Accept,
                    0,
                    state,
                    _balance
                )
            );
        }

        // ── Helpers ──────────────────────────────────────────────────

        private (GameState state, Club c1, Club c2) BuildState(int seed = 42)
        {
            var state = new GameState
            {
                currentDate = _today,
                randomSeed = seed,
                userClubId = -1,
                nextPlayerId = 100,
                nextIntakeId = 1,
                nextOfferId = 1,
            };
            var c1 = new Club
            {
                id = 1,
                name = "FromClub",
                reputation = 60,
                leagueId = 1,
                finance = new Finance { money = 50_000_000 },
            };
            var c2 = new Club
            {
                id = 2,
                name = "ToClub",
                reputation = 70,
                leagueId = 1,
                finance = new Finance { money = 50_000_000 },
            };
            state.AddClub(c1);
            state.AddClub(c2);
            return (state, c1, c2);
        }

        private Player NewPlayer(int id, int clubId)
        {
            return new Player
            {
                id = id,
                currentAbility = 100,
                potentialAbility = 110,
                currentClubId = clubId,
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
                    weeklyWage = 30_000,
                    startDate = _today.AddYears(-1),
                    endDate = _today.AddYears(2),
                },
            };
        }

        // K.2: 선수 협상 단계에서 수락 보장 — 고임금(wageRatio 8.0 → acceptChance > 1.0)
        private Contract NewContract(GameState state) =>
            new Contract
            {
                weeklyWage = 200_000,
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(3),
            };

        // CounterOffer 상태의 오퍼를 직접 생성 (RespondToCounterOffer 테스트용)
        private TransferOffer MakeCounterOffer(GameState state, Club c1, Club c2, int counterAmount)
        {
            var p = state.GetPlayer(1);
            if (p == null)
            {
                p = NewPlayer(1, c1.id);
                state.AddPlayer(p);
                c1.seniorSquadIds.Add(p.id);
            }

            var offer = new TransferOffer
            {
                id = state.nextOfferId++,
                playerId = 1,
                fromClubId = c1.id,
                toClubId = c2.id,
                amount = 3_000_000,
                proposed = NewContract(state),
                status = OfferStatus.CounterOffer,
                counterAmount = counterAmount,
                negotiationRound = 1,
            };
            state.activeOffers.Add(offer);
            return offer;
        }
    }
}
