// NegotiatingTests.cs
// V0.5 K.2 — PlayerNegotiate DoD 검증.
// DoD: loyalty 80 vs 20 거절률 차이.
// algorithms.md V0.5-3.1 [4] / design-decisions.md #48.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class NegotiatingTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 7, 1); // 이적시장 활성화 기간

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.marketValueBase = 5_000_000;
            _balance.marketValueCaExponent = 4.0f;
            _balance.marketValuePaCoeff = 0f;
            _balance.marketValueAgeCurve = new float[] { 0.8f, 1.0f, 0.9f, 0.7f };
            _balance.marketValueContractCurve = new float[] { 0.5f, 0.8f, 1.0f, 1.05f };
            _balance.marketValuePositionFactor = new float[] { 0.75f, 0.85f, 1.0f, 1.2f };
            _balance.marketValueInjuryFactor = 0.5f;
            _balance.aiValueNoiseSigma = 0.10f;
            _balance.aiAcceptThreshold = 1.30f;
            _balance.aiCounterOfferThreshold = 1.10f;
            _balance.aiMockingThreshold = 0.85f;
            _balance.aiCounterOfferFactor = 1.30f;
            _balance.maxNegotiationRounds = 3;
            _balance.aiMockingMoralePenalty = 3;
            _balance.playtimeAgreementBonus = 0.2f;
            _balance.wageBaseAtMinCA = 500;
            _balance.wagePerCAPoint = 350f;
            _balance.wageFloor = 500;
            _balance.minCA = 30;
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

        // ── T1. loyalty 80 → 거절률 > loyalty 20 (DoD) ───────────────
        // 100 시드 비교 — loyalty 높을수록 이적 거절

        [Test]
        public void T1_LoyaltyHigh_MoreRejection()
        {
            // 공정 주급 수준의 제안 — loyalty 차이가 결과를 가를 수 있어야
            int fairWage = 500 + (100 - 30) * 350; // 25000
            var proposed = new Contract
            {
                weeklyWage = fairWage, // wageRatio=1.0 → acceptChance=0.5 ± loyalty 보정
                startDate = _today,
                endDate = _today.AddYears(3),
            };

            int acceptedHighLoyalty = CountAccepted(loyalty: 80, ambition: 50, proposed, 100);
            int acceptedLowLoyalty = CountAccepted(loyalty: 20, ambition: 50, proposed, 100);

            Assert.Less(
                acceptedHighLoyalty,
                acceptedLowLoyalty,
                $"T1: loyalty 80 수락({acceptedHighLoyalty}) < loyalty 20 수락({acceptedLowLoyalty})"
            );
        }

        // ── T2. ambition 80 → 수락률 > ambition 20 ───────────────────

        [Test]
        public void T2_AmbitionHigh_MoreAcceptance()
        {
            int fairWage = 500 + (100 - 30) * 350;
            var proposed = new Contract
            {
                weeklyWage = fairWage,
                startDate = _today,
                endDate = _today.AddYears(3),
            };

            int acceptedHighAmbition = CountAccepted(loyalty: 50, ambition: 80, proposed, 100);
            int acceptedLowAmbition = CountAccepted(loyalty: 50, ambition: 20, proposed, 100);

            Assert.Greater(
                acceptedHighAmbition,
                acceptedLowAmbition,
                $"T2: ambition 80 수락({acceptedHighAmbition}) > ambition 20 수락({acceptedLowAmbition})"
            );
        }

        // ── T3. 고임금 → 선수 항상 수락 ─────────────────────────────

        [Test]
        public void T3_HighWage_AlwaysAccepted()
        {
            // weeklyWage = 200_000 → wageRatio = 8.0 → acceptChance > 1.0 → 항상 수락
            var proposed = new Contract
            {
                weeklyWage = 200_000,
                startDate = _today,
                endDate = _today.AddYears(3),
            };

            int accepted = CountAccepted(loyalty: 80, ambition: 20, proposed, 100);
            Assert.AreEqual(100, accepted, "T3: 고임금 → 100% 수락");
        }

        // ── T4. includesPlaytimeAgreement → 수락률 ↑ ─────────────────

        [Test]
        public void T4_PlaytimeAgreement_IncreasesAcceptance()
        {
            int fairWage = 500 + (100 - 30) * 350;
            var proposed = new Contract
            {
                weeklyWage = fairWage, // borderline wage
                startDate = _today,
                endDate = _today.AddYears(3),
            };

            int acceptedWith = CountAccepted(
                loyalty: 50,
                ambition: 50,
                proposed,
                100,
                playtimeAgreement: true
            );
            int acceptedWithout = CountAccepted(
                loyalty: 50,
                ambition: 50,
                proposed,
                100,
                playtimeAgreement: false
            );

            Assert.GreaterOrEqual(
                acceptedWith,
                acceptedWithout,
                $"T4: 출전약속 포함 수락({acceptedWith}) ≥ 미포함({acceptedWithout})"
            );
        }

        // ── T5. 결정성 — 같은 시드 → 같은 결과 ─────────────────────

        [Test]
        public void T5_Deterministic_SameSeedSameResult()
        {
            int fairWage = 500 + (100 - 30) * 350;
            var proposed = new Contract
            {
                weeklyWage = fairWage,
                startDate = _today,
                endDate = _today.AddYears(3),
            };

            bool r1 = RunOnce(seed: 7, loyalty: 50, ambition: 50, proposed);
            bool r2 = RunOnce(seed: 7, loyalty: 50, ambition: 50, proposed);

            Assert.AreEqual(r1, r2, "T5: 같은 시드 → 같은 결과");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private int CountAccepted(
            int loyalty,
            int ambition,
            Contract proposed,
            int trials,
            bool playtimeAgreement = false
        )
        {
            int accepted = 0;
            for (int seed = 0; seed < trials; seed++)
            {
                if (RunOnce(seed, loyalty, ambition, proposed, playtimeAgreement))
                    accepted++;
            }
            return accepted;
        }

        private bool RunOnce(
            int seed,
            int loyalty,
            int ambition,
            Contract proposed,
            bool playtimeAgreement = false
        )
        {
            EventBus.Clear();
            var (state, c1, c2) = BuildState(seed);

            var player = new Player
            {
                id = 1,
                currentAbility = 100,
                potentialAbility = 110,
                currentClubId = c1.id,
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
                    happiness = 50,
                    fatigue = 0,
                    form = 50,
                    injury = new InjuryInfo { injuryTypeId = -1 },
                },
                contract = new Contract
                {
                    weeklyWage = 25_000,
                    startDate = _today.AddYears(-1),
                    endDate = _today.AddYears(2),
                },
                hiddenAttrs = new HiddenAttributes { loyalty = loyalty, ambition = ambition },
            };
            state.AddPlayer(player);
            c1.seniorSquadIds.Add(player.id);

            int mv = TransferSystem.CalculateMarketValue(player, state, _balance);
            // AI 구단이 반드시 수락하도록 충분히 높은 이적료
            var offer = TransferSystem.SubmitOffer(
                player.id,
                c1.id,
                c2.id,
                (int)(mv * 2.0),
                proposed,
                state,
                _balance
            );
            offer.includesPlaytimeAgreement = playtimeAgreement;

            TransferSystem.ProcessOffers(state, _balance);

            // 새 흐름: AI 수락 → CounterOffer(counterAmount=amount). 유저 수락 시뮬레이션으로 PlayerNegotiate 발동.
            if (offer.status == OfferStatus.CounterOffer)
                TransferSystem.RespondToCounterOffer(
                    offer.id,
                    CounterResponse.Accept,
                    0,
                    state,
                    _balance
                );

            return offer.status == OfferStatus.Accepted || offer.status == OfferStatus.Completed;
        }

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
    }
}
