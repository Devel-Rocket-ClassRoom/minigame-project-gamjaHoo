// InjurySystemTests.cs
// V0.5 D.4 Sub-B — algorithms.md V0.5-11 Test Scenarios T1~T6.

using System;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class InjurySystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 5, 26);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        // ── ComputeRecoveryDays ─────────────────────────────────────

        [Test]
        public void T1_Recovery_MedicalLv10_ReducesTo20()
        {
            // base 30일, Medical Lv10 = ×1.5 → 30 / 1.5 = 20
            int days = InjurySystem.ComputeRecoveryDays(30, medicalLevel: 10, gymLevel: 0, _balance);
            Assert.AreEqual(20, days, "T1: Medical Lv10 (×1.5) → 회복 20일");
        }

        [Test]
        public void T2_Recovery_GymLv10_ReducesTo25()
        {
            // base 30일, Gym Lv10 = ×1.2 → 30 / 1.2 = 25
            int days = InjurySystem.ComputeRecoveryDays(30, medicalLevel: 0, gymLevel: 10, _balance);
            Assert.AreEqual(25, days, "T2: Gym Lv10 (×1.2) → 회복 25일");
        }

        [Test]
        public void T3_Recovery_MedicalAndGymCombined()
        {
            // base 30일, Medical Lv10 + Gym Lv10 = ×1.5 × ×1.2 = ×1.8 → 30 / 1.8 ≈ 17
            int days = InjurySystem.ComputeRecoveryDays(30, medicalLevel: 10, gymLevel: 10, _balance);
            Assert.AreEqual(17, days, "T3: Medical + Gym 결합 → 30/1.8 ≈ 17");
        }

        [Test]
        public void T3b_Recovery_MinOneDay()
        {
            // base 0 (이론적) → min 1 보정
            int days = InjurySystem.ComputeRecoveryDays(0, medicalLevel: 10, gymLevel: 10, _balance);
            Assert.AreEqual(1, days, "T3b: 0일 → min 1 clamp");
        }

        // ── ComputeInjuryRate ────────────────────────────────────────

        [Test]
        public void T4_Rate_MedicalLv10_ReducesToHalf()
        {
            // Medical Lv10 → max(0.5, 1 - 10*0.05) = max(0.5, 0.5) = 0.5
            float rate = InjurySystem.ComputeInjuryRate(medicalLevel: 10, _balance);
            Assert.AreEqual(0.5f, rate, 0.001f, "T4: Medical Lv10 → 0.5");
        }

        [Test]
        public void T4b_Rate_MedicalLv5_ReducesPartial()
        {
            // Medical Lv5 → 1 - 5*0.05 = 0.75
            float rate = InjurySystem.ComputeInjuryRate(medicalLevel: 5, _balance);
            Assert.AreEqual(0.75f, rate, 0.001f, "T4b: Medical Lv5 → 0.75");
        }

        [Test]
        public void T5_Rate_FloorAt05()
        {
            // Medical Lv20 (가상) → 1 - 20*0.05 = 0 → clamp 0.5
            float rate = InjurySystem.ComputeInjuryRate(medicalLevel: 20, _balance);
            Assert.AreEqual(0.5f, rate, 0.001f, "T5: 발생률 floor 0.5 (완전 차단 불가)");
        }

        [Test]
        public void T5b_Rate_MedicalLv0_NoReduction()
        {
            // Medical Lv0 (이론) → 1.0
            float rate = InjurySystem.ComputeInjuryRate(medicalLevel: 0, _balance);
            Assert.AreEqual(1f, rate, 0.001f, "T5b: Medical Lv0 → 1.0 (보정 없음)");
        }

        // ── ProcessRecovery (DailyProcessor 통합) ────────────────────

        [Test]
        public void T6_ProcessRecovery_OnExpectedReturn_PublishesEvent()
        {
            var state = NewState(
                NewPlayer(
                    1,
                    injury: new InjuryInfo
                    {
                        injuryTypeId = 5,
                        startDate = _today.AddDays(-10),
                        expectedReturn = _today,
                        isCareerThreatening = true,
                    }
                )
            );

            int eventCount = 0;
            int eventPlayerId = -1;
            Action<PlayerInjuryRecoveredEvent> handler = e =>
            {
                eventCount++;
                eventPlayerId = e.playerId;
            };
            EventBus.Subscribe(handler);

            InjurySystem.ProcessRecovery(state, _balance);

            Assert.AreEqual(-1, state.GetPlayer(1).state.injury.injuryTypeId, "T6: 회복 → sentinel");
            Assert.IsFalse(state.GetPlayer(1).state.injury.isCareerThreatening, "T6: isCareerThreatening 리셋");
            Assert.AreEqual(1, eventCount, "T6: PlayerInjuryRecoveredEvent 1회 발행");
            Assert.AreEqual(1, eventPlayerId, "T6: 이벤트 playerId 정확");

            EventBus.Unsubscribe(handler);
        }

        [Test]
        public void T6b_ProcessRecovery_BeforeExpectedReturn_NoChange()
        {
            var state = NewState(
                NewPlayer(
                    1,
                    injury: new InjuryInfo
                    {
                        injuryTypeId = 5,
                        expectedReturn = _today.AddDays(7),
                    }
                )
            );

            int eventCount = 0;
            Action<PlayerInjuryRecoveredEvent> handler = e => eventCount++;
            EventBus.Subscribe(handler);

            InjurySystem.ProcessRecovery(state, _balance);

            Assert.AreEqual(5, state.GetPlayer(1).state.injury.injuryTypeId, "T6b: 아직 회복 X");
            Assert.AreEqual(0, eventCount, "T6b: 이벤트 발행 X");

            EventBus.Unsubscribe(handler);
        }

        [Test]
        public void T6c_ProcessRecovery_HealthyPlayer_NoOp()
        {
            var state = NewState(NewPlayer(1)); // injury -1 sentinel
            Assert.DoesNotThrow(() => InjurySystem.ProcessRecovery(state, _balance));
            Assert.AreEqual(-1, state.GetPlayer(1).state.injury.injuryTypeId);
        }

        // ── Helpers ──────────────────────────────────────────────────

        private GameState NewState(params Player[] players)
        {
            var state = new GameState { currentDate = _today };
            foreach (var p in players)
                state.AddPlayer(p);
            return state;
        }

        private static Player NewPlayer(int id, InjuryInfo injury = null) =>
            new Player
            {
                id = id,
                state = new PlayerState
                {
                    fatigue = 0,
                    injury = injury ?? new InjuryInfo { injuryTypeId = -1 },
                },
            };
    }
}
