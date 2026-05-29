// RenewContractTests.cs
// V0.5 H.1 — TransferSystem.RenewContract DoD 검증.
// DoD: 주급 ×1.5 재계약 → 거의 무조건 수락 / morale +15 / happiness +25.
// algorithms.md V0.5-3.1 / design-decisions.md #48.

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class RenewContractTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 11, 1); // 이적 비활성화 기간

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // 임금 관련 기본값 명시
            _balance.wageBaseAtMinCA = 500;
            _balance.wagePerCAPoint = 350f;
            _balance.wageFloor = 500;
            _balance.minCA = 30;
            // 사기 회복 기본값
            _balance.contractRenewalMoraleBoost = 15;
            _balance.contractRenewalHappinessBoost = 25;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        // ── T1. 주급 ×1.5 재계약 → 거의 무조건 수락 (DoD) ─────────────
        // wageRatio=1.5 → baseAcceptChance = 0.4 + 0.3 = 0.7 (loyalty=50 중립)
        // 100 시드 반복 → 수락률 ≥ 60% 검증

        [Test]
        public void T1_Wage1p5x_HighAcceptRate()
        {
            int accepted = 0;
            const int trials = 100;

            for (int seed = 0; seed < trials; seed++)
            {
                var state = NewState(randomSeed: seed);
                var player = NewPlayer(1, ca: 100, contractEndYears: 1, loyalty: 50);
                state.AddPlayer(player);

                int fairWage = 500 + (100 - 30) * 350; // 25000
                int offerWage = (int)(fairWage * 1.5f); // 37500

                var proposed = new Contract
                {
                    weeklyWage = offerWage,
                    startDate = _today,
                    endDate = _today.AddYears(2),
                };

                TransferSystem.RenewContract(1, proposed, state, _balance);

                if (player.contract.weeklyWage == offerWage)
                    accepted++;

                EventBus.Clear();
            }

            Assert.GreaterOrEqual(
                accepted,
                60,
                $"T1: 주급 ×1.5 수락률 {accepted}/100 — 60% 이상 기대"
            );
        }

        // ── T2. 수락 시 morale +15 / happiness +25 (DoD) ───────────────
        // professionalism=50, loyalty=50 → 보정 배율 1.0 → boost 정확히 적용

        [Test]
        public void T2_Accept_MoraleAndHappinessUp()
        {
            // 매우 높은 주급(×3) + loyalty=50 → acceptChance=1.0+ → 반드시 수락하는 시드 탐색
            // acceptChance = 0.4 + (3-1)*0.6 = 1.6 → clamp 없으나 rng.NextDouble() < 1.6 는 항상 true
            var state = NewState(randomSeed: 42);
            var player = NewPlayer(1, ca: 100, contractEndYears: 1, loyalty: 50);
            player.state.morale = 50;
            player.state.happiness = 50;
            state.AddPlayer(player);

            int fairWage = 500 + (100 - 30) * 350; // 25000
            var proposed = new Contract
            {
                weeklyWage = fairWage * 3, // acceptChance 1.6 → 반드시 수락
                startDate = _today,
                endDate = _today.AddYears(2),
            };

            ContractRenewedEvent? published = null;
            EventBus.Subscribe<ContractRenewedEvent>(e => published = e);

            TransferSystem.RenewContract(1, proposed, state, _balance);

            Assert.IsNotNull(published, "T2: ContractRenewedEvent 발행됨");
            Assert.AreEqual(1, published!.playerId);
            // professionalism=50 → factor=1.0 → morale +15
            Assert.AreEqual(65, player.state.morale, "T2: morale 50+15=65");
            // loyalty=50 → factor=1.0 → happiness +25
            Assert.AreEqual(75, player.state.happiness, "T2: happiness 50+25=75");
        }

        // ── T3. 거절 시 ContractRenewalRejectedEvent, contract 미변경 ──

        [Test]
        public void T3_Reject_RejectedEventAndContractUnchanged()
        {
            // wageRatio=0.5 → acceptChance = 0.4 + (0.5-1)*0.6 = 0.1 → 반드시 거절하는 시드 탐색
            // acceptChance = 0.1 → rng.NextDouble() >= 0.1 확률 높지만 완전 보장 안 됨
            // 주급=1(최저) → wageRatio ≈ 0 → acceptChance = 0.4 + (0-1)*0.6 = -0.2 → 100% 거절
            var state = NewState(randomSeed: 42);
            var player = NewPlayer(1, ca: 100, contractEndYears: 2, loyalty: 50);
            int originalWage = player.contract.weeklyWage;
            state.AddPlayer(player);

            var proposed = new Contract
            {
                weeklyWage = 1, // 말도 안 되는 낮은 주급 → acceptChance < 0 → 반드시 거절
                startDate = _today,
                endDate = _today.AddYears(2),
            };

            ContractRenewalRejectedEvent? rejected = null;
            ContractRenewedEvent? accepted = null;
            EventBus.Subscribe<ContractRenewalRejectedEvent>(e => rejected = e);
            EventBus.Subscribe<ContractRenewedEvent>(e => accepted = e);

            TransferSystem.RenewContract(1, proposed, state, _balance);

            Assert.IsNotNull(rejected, "T3: ContractRenewalRejectedEvent 발행됨");
            Assert.IsNull(accepted, "T3: ContractRenewedEvent 미발행");
            Assert.AreEqual(1, rejected!.playerId);
            Assert.AreEqual(originalWage, player.contract.weeklyWage, "T3: 계약 미변경");
        }

        // ── T4. 결정성 — 같은 시드 → 같은 결과 ─────────────────────────

        [Test]
        public void T4_Deterministic_SameSeedSameResult()
        {
            int fairWage = 500 + (100 - 30) * 350;
            var proposed = new Contract
            {
                weeklyWage = (int)(fairWage * 1.5f),
                startDate = _today,
                endDate = _today.AddYears(2),
            };

            bool result1 = RunOnce(seed: 1, proposed);
            bool result2 = RunOnce(seed: 1, proposed);
            bool result3 = RunOnce(seed: 2, proposed);

            Assert.AreEqual(result1, result2, "T4: 같은 시드 → 같은 결과");
            // seed=2 는 다를 수도 있고 같을 수도 있으나 결정적이기만 하면 OK
            _ = result3;
        }

        // ── T5. loyalty ↑ → 수락률 ↑ (100 시드 비교) ─────────────────

        [Test]
        public void T5_LoyaltyHigh_MoreAcceptance()
        {
            int fairWage = 500 + (100 - 30) * 350;
            var proposed = new Contract
            {
                weeklyWage = (int)(fairWage * 1.1f), // 낮은 주급 — 충성도가 차이를 만들어야
                startDate = _today,
                endDate = _today.AddYears(2),
            };
            const int trials = 100;

            int acceptedHighLoyalty = CountAcceptances(loyalty: 80, proposed, trials);
            int acceptedLowLoyalty = CountAcceptances(loyalty: 20, proposed, trials);

            Assert.GreaterOrEqual(
                acceptedHighLoyalty,
                acceptedLowLoyalty,
                $"T5: loyalty 80 수락({acceptedHighLoyalty}) ≥ loyalty 20 수락({acceptedLowLoyalty})"
            );
        }

        // ── T6. 잔여 6개월 이내 → 가산점 +0.15 ─────────────────────────

        [Test]
        public void T6_Within6Months_BonusApplied()
        {
            // acceptChance 가 경계에 있는 주급으로 테스트 (borderline wage)
            int fairWage = 500 + (100 - 30) * 350;
            // wageRatio = 0.9 → acceptChance = 0.4 + (0.9-1)*0.6 = 0.34
            // + 6개월 이내 +0.15 → 0.49 vs 0.34: 100 시드 비교
            var proposed = new Contract
            {
                weeklyWage = (int)(fairWage * 0.9f),
                startDate = _today,
                endDate = _today.AddYears(2),
            };
            const int trials = 100;

            int acceptedShort = CountAcceptancesWithContractEnd(
                daysLeft: 90,
                proposed,
                trials
            );
            int acceptedLong = CountAcceptancesWithContractEnd(
                daysLeft: 365,
                proposed,
                trials
            );

            Assert.GreaterOrEqual(
                acceptedShort,
                acceptedLong,
                $"T6: 잔여 90일 수락({acceptedShort}) ≥ 잔여 365일 수락({acceptedLong})"
            );
        }

        // ── T7. Null 파라미터 예외 ────────────────────────────────────

        [Test]
        public void T7_NullContract_ThrowsArgumentNullException()
        {
            var state = NewState(randomSeed: 0);
            var player = NewPlayer(1, ca: 100, contractEndYears: 2, loyalty: 50);
            state.AddPlayer(player);

            Assert.Throws<ArgumentNullException>(() =>
                TransferSystem.RenewContract(1, null!, state, _balance)
            );
        }

        [Test]
        public void T7b_PlayerNotFound_ThrowsArgumentException()
        {
            var state = NewState(randomSeed: 0);
            var proposed = new Contract
            {
                weeklyWage = 30000,
                startDate = _today,
                endDate = _today.AddYears(2),
            };

            Assert.Throws<ArgumentException>(() =>
                TransferSystem.RenewContract(999, proposed, state, _balance)
            );
        }

        // ── Helpers ──────────────────────────────────────────────────

        private GameState NewState(int randomSeed = 42) =>
            new GameState
            {
                currentDate = _today,
                randomSeed = randomSeed,
                userClubId = 1,
                nextPlayerId = 10,
                nextIntakeId = 1,
            };

        private Player NewPlayer(int id, int ca, int contractEndYears, int loyalty)
        {
            int fairWage = 500 + (ca - 30) * 350;
            return new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = ca + 10,
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
                    weeklyWage = fairWage,
                    startDate = _today.AddYears(-1),
                    endDate = _today.AddYears(contractEndYears),
                },
                hiddenAttrs = new HiddenAttributes { loyalty = loyalty, professionalism = 50 },
            };
        }

        private bool RunOnce(int seed, Contract proposed)
        {
            EventBus.Clear();
            var state = NewState(randomSeed: seed);
            var player = NewPlayer(1, ca: 100, contractEndYears: 2, loyalty: 50);
            state.AddPlayer(player);

            int originalWage = player.contract.weeklyWage;
            TransferSystem.RenewContract(1, proposed, state, _balance);
            return player.contract.weeklyWage == proposed.weeklyWage;
        }

        private int CountAcceptances(int loyalty, Contract proposed, int trials)
        {
            int accepted = 0;
            for (int seed = 0; seed < trials; seed++)
            {
                EventBus.Clear();
                var state = NewState(randomSeed: seed);
                var player = NewPlayer(1, ca: 100, contractEndYears: 2, loyalty: loyalty);
                state.AddPlayer(player);
                TransferSystem.RenewContract(1, proposed, state, _balance);
                if (player.contract.weeklyWage == proposed.weeklyWage)
                    accepted++;
            }
            return accepted;
        }

        private int CountAcceptancesWithContractEnd(int daysLeft, Contract proposed, int trials)
        {
            int accepted = 0;
            for (int seed = 0; seed < trials; seed++)
            {
                EventBus.Clear();
                var state = NewState(randomSeed: seed);
                var player = NewPlayer(1, ca: 100, contractEndYears: 0, loyalty: 50);
                player.contract.endDate = _today.AddDays(daysLeft);
                state.AddPlayer(player);
                TransferSystem.RenewContract(1, proposed, state, _balance);
                if (player.contract.weeklyWage == proposed.weeklyWage)
                    accepted++;
            }
            return accepted;
        }
    }
}
