// DailyProcessorTests.cs
// DoD: Task 8.2 fatigue 회복 + 부상 카운트다운. T1~T6.

using System;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class DailyProcessorTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 8, 16);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // 기본값: fatigueRecoveryPerDay = 15
        }

        private GameState NewState(params Player[] players)
        {
            var state = new GameState { currentDate = _today };
            foreach (var p in players)
                state.AddPlayer(p);
            return state;
        }

        private static Player NewPlayer(int id, int fatigue = 0, InjuryInfo injury = null) =>
            new Player
            {
                id = id,
                state = new PlayerState
                {
                    fatigue = fatigue,
                    injury = injury ?? new InjuryInfo { injuryTypeId = -1 },
                },
            };

        // ── T1. fatigue 회복 — 30 → 15 ────────────────────────────────

        [Test]
        public void T1_FatigueRecovery_ReducesByRecoveryAmount()
        {
            var p = NewPlayer(1, fatigue: 30);
            var state = NewState(p);

            DailyProcessor.Run(state, _balance);

            Assert.AreEqual(
                15,
                p.state.fatigue,
                $"T1: fatigue 30 - {_balance.fatigueRecoveryPerDay} = 15"
            );
        }

        // ── T2. fatigue 0 clamp ───────────────────────────────────────

        [Test]
        public void T2_FatigueRecovery_ClampedAtZero()
        {
            var p = NewPlayer(1, fatigue: 5);
            var state = NewState(p);

            DailyProcessor.Run(state, _balance);

            Assert.AreEqual(0, p.state.fatigue, "T2: fatigue 5 - 15 → 음수 X, 0 clamp");
        }

        // ── T3. 부상 회복 — expectedReturn ≤ today ────────────────────

        [Test]
        public void T3_InjuryRecovery_WhenExpectedReturnReached()
        {
            var p = NewPlayer(
                1,
                injury: new InjuryInfo
                {
                    injuryTypeId = 5,
                    startDate = _today.AddDays(-10),
                    expectedReturn = _today, // 오늘 회복일
                    isCareerThreatening = true,
                }
            );
            var state = NewState(p);

            DailyProcessor.Run(state, _balance);

            Assert.AreEqual(-1, p.state.injury.injuryTypeId, "T3: 회복 → -1 sentinel");
            Assert.IsFalse(p.state.injury.isCareerThreatening, "T3: isCareerThreatening 리셋");
        }

        // ── T4. 부상 미회복 — expectedReturn > today ──────────────────

        [Test]
        public void T4_InjuryNotRecovered_WhenExpectedReturnFuture()
        {
            var p = NewPlayer(
                1,
                injury: new InjuryInfo
                {
                    injuryTypeId = 5,
                    expectedReturn = _today.AddDays(7), // 일주일 후
                }
            );
            var state = NewState(p);

            DailyProcessor.Run(state, _balance);

            Assert.AreEqual(5, p.state.injury.injuryTypeId, "T4: 아직 회복 안 됨, 그대로");
        }

        // ── T5. 부상 없는 선수 — no-op ────────────────────────────────

        [Test]
        public void T5_HealthyPlayer_InjuryFieldUnchanged()
        {
            var p = NewPlayer(1); // injury 디폴트 = -1
            var state = NewState(p);

            DailyProcessor.Run(state, _balance);

            Assert.AreEqual(-1, p.state.injury.injuryTypeId, "T5: 건강한 선수 변경 없음");
        }

        // ── T6. 결정성 — 같은 입력 → 같은 결과 ───────────────────────

        [Test]
        public void T6_Determinism_SameInputSameOutput()
        {
            var p1 = NewPlayer(1, fatigue: 50);
            var p2 = NewPlayer(1, fatigue: 50);
            var s1 = NewState(p1);
            var s2 = NewState(p2);

            DailyProcessor.Run(s1, _balance);
            DailyProcessor.Run(s2, _balance);

            Assert.AreEqual(p1.state.fatigue, p2.state.fatigue, "T6: 결정성");
        }

        // ── 추가: 빈 state 안전성 ─────────────────────────────────────

        [Test]
        public void T7_EmptyState_NoException()
        {
            var state = NewState();
            Assert.DoesNotThrow(() => DailyProcessor.Run(state, _balance));
        }
    }
}
