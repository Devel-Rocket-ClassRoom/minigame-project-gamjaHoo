// MoraleSystemTests.cs
// V1.0 G.1 — algorithms.md V1.0-6 Test Scenarios T1~T7.
// T8 (라커룸 분위기 < 30 → 폼 -5) 는 G.3 책임이라 본 PR 범위 밖.

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class MoraleSystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 5, 25);

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            EventBus.Clear();
        }

        [TearDown]
        public void Teardown()
        {
            EventBus.Clear();
        }

        // ── T1. 매치 승리 사기 변동 ──────────────────────────────────

        [Test]
        public void T1_MatchWin_StartingElevenMoralePlus8()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 50, professionalism: 50);
            state.AddPlayer(p);

            var result = new MatchResult
            {
                homeScore = 2,
                awayScore = 0,
                homeStarting11 = new List<int> { 1 },
                awayStarting11 = new List<int>(),
                playerStats = new List<PlayerMatchStat> { new PlayerMatchStat { playerId = 1, rating = 7.0f } },
            };

            MoraleSystem.OnMatchFinished(state, result, _balance);

            // 승리 +8, professionalism 50 = factor 1.0
            Assert.AreEqual(58, p.state.morale, "T1: 승리 → +8");
        }

        // ── T2. 평점 가산점 (≥ 7.5) ─────────────────────────────────

        [Test]
        public void T2_HighRating_AdditionalBonus()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 50, professionalism: 50);
            state.AddPlayer(p);

            var result = new MatchResult
            {
                homeScore = 1,
                awayScore = 0,
                homeStarting11 = new List<int> { 1 },
                awayStarting11 = new List<int>(),
                playerStats = new List<PlayerMatchStat> { new PlayerMatchStat { playerId = 1, rating = 7.5f } },
            };

            MoraleSystem.OnMatchFinished(state, result, _balance);

            // 승리 +8 + 평점 +5 = +13
            Assert.AreEqual(63, p.state.morale, "T2: 승리 + 평점 7.5+ → +13");
        }

        // ── T3. Professionalism 보정 ─────────────────────────────────

        [Test]
        public void T3_ProfessionalismFactor_ReducesSwing()
        {
            var state = NewState();
            var high = NewPlayer(1, clubId: 1, morale: 50, professionalism: 80);
            var low = NewPlayer(2, clubId: 1, morale: 50, professionalism: 20);
            state.AddPlayer(high);
            state.AddPlayer(low);

            var result = new MatchResult
            {
                homeScore = 0,
                awayScore = 2,
                homeStarting11 = new List<int> { 1, 2 },
                awayStarting11 = new List<int>(),
                playerStats = new List<PlayerMatchStat>(),
            };

            MoraleSystem.OnMatchFinished(state, result, _balance);

            int highDelta = 50 - high.state.morale;
            int lowDelta = 50 - low.state.morale;
            // high: -8 × (1 - 0.3 × 0.3) = -8 × 0.91 = -7.28 → -7
            // low : -8 × (1 + 0.3 × 0.3) = -8 × 1.09 = -8.72 → -9
            Assert.AreEqual(7, highDelta, "T3: prof 80 → -7");
            Assert.AreEqual(9, lowDelta, "T3: prof 20 → -9");
            Assert.Greater(lowDelta, highDelta, "T3: prof 낮을수록 변동폭 ↑");
        }

        // ── T4. 약속 미이행 → Happiness -20 ──────────────────────────

        [Test]
        public void T4_PromiseBroken_HappinessPenalty()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, happiness: 70, loyalty: 50);
            state.AddPlayer(p);

            var promise = new Promise { id = 1, playerId = 1, status = PromiseStatus.Broken };

            MoraleSystem.OnPromiseBroken(state, promise, _balance);

            // -20 × loyalty factor 1.0 = -20
            Assert.AreEqual(50, p.state.happiness, "T4: 약속 미이행 → Happiness -20");
        }

        // ── T5. Loyalty 완화 ─────────────────────────────────────────

        [Test]
        public void T5_LoyaltyFactor_CushionsBreak()
        {
            var state = NewState();
            var high = NewPlayer(1, clubId: 1, happiness: 70, loyalty: 80);
            var low = NewPlayer(2, clubId: 1, happiness: 70, loyalty: 20);
            state.AddPlayer(high);
            state.AddPlayer(low);

            var promise1 = new Promise { id = 1, playerId = 1, status = PromiseStatus.Broken };
            var promise2 = new Promise { id = 2, playerId = 2, status = PromiseStatus.Broken };

            MoraleSystem.OnPromiseBroken(state, promise1, _balance);
            MoraleSystem.OnPromiseBroken(state, promise2, _balance);

            int highPenalty = 70 - high.state.happiness;
            int lowPenalty = 70 - low.state.happiness;
            // high: 20 × (1 - 0.3 × 0.5) = 20 × 0.85 = 17
            // low : 20 × (1 + 0.3 × 0.5) = 20 × 1.15 = 23
            Assert.AreEqual(17, highPenalty, "T5: loyalty 80 → -17");
            Assert.AreEqual(23, lowPenalty, "T5: loyalty 20 → -23");
            Assert.Greater(lowPenalty, highPenalty, "T5: loyalty 낮을수록 충격 ↑");
        }

        // ── T6. TransferRequestEvent 자동 발행 ───────────────────────

        [Test]
        public void T6_LowHappiness_PublishesTransferRequest()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, happiness: 25, loyalty: 50);
            state.AddPlayer(p);

            int eventCount = 0;
            int receivedPid = -999;
            Action<TransferRequestEvent> handler = e =>
            {
                eventCount++;
                receivedPid = e.playerId;
            };
            EventBus.Subscribe(handler);

            var promise = new Promise { id = 1, playerId = 1, status = PromiseStatus.Broken };
            MoraleSystem.OnPromiseBroken(state, promise, _balance);

            EventBus.Unsubscribe(handler);

            // 25 - 20 = 5 < 20 threshold
            Assert.AreEqual(1, eventCount, "T6: Happiness < 20 → 1회 발행");
            Assert.AreEqual(1, receivedPid, "T6: 페이로드 playerId 일치");
        }

        // ── T7. 일일 회복 — morale 30 → ~50 까지 20일 ─────────────

        [Test]
        public void T7_DailyRecovery_ConvergesTo50()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 30, professionalism: 50);
            state.AddPlayer(p);

            for (int day = 0; day < 25; day++)
                MoraleSystem.Tick(state, _balance);

            Assert.AreEqual(50, p.state.morale, "T7: 30 → 50 (rate=1, 20일+여유)");
        }

        [Test]
        public void T7b_DailyRecovery_HighMoraleAlsoConvergesDown()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 80, professionalism: 50);
            state.AddPlayer(p);

            for (int day = 0; day < 35; day++)
                MoraleSystem.Tick(state, _balance);

            Assert.AreEqual(50, p.state.morale, "T7b: 80 → 50 (30일+여유)");
        }

        // ── Bonus. OnTransferCompleted / OnContractRenewed / OnInterview 핸들러 ──

        [Test]
        public void OnTransferCompleted_MoraleBonus_HappinessReset()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 50, happiness: 25, professionalism: 50);
            state.AddPlayer(p);

            var offer = new TransferOffer { playerId = 1 };
            MoraleSystem.OnTransferCompleted(state, offer, _balance);

            Assert.AreEqual(70, p.state.morale, "환영 +20");
            Assert.AreEqual(50, p.state.happiness, "happiness 리셋 (장기)");
        }

        [Test]
        public void OnContractRenewed_BothMoraleAndHappinessUp()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 50, happiness: 50, loyalty: 50, professionalism: 50);
            state.AddPlayer(p);

            MoraleSystem.OnContractRenewed(state, 1, _balance);

            Assert.AreEqual(65, p.state.morale, "재계약 morale +15");
            Assert.AreEqual(75, p.state.happiness, "재계약 happiness +25");
        }

        [Test]
        public void OnInterview_Praise_MoralePlus5()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 50, professionalism: 50);
            state.AddPlayer(p);

            MoraleSystem.OnInterview(state, 1, InterviewType.Praise, _balance);

            Assert.AreEqual(55, p.state.morale, "Praise +5");
        }

        [Test]
        public void OnInterview_Criticize_MoraleMinus3()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 50, professionalism: 50);
            state.AddPlayer(p);

            MoraleSystem.OnInterview(state, 1, InterviewType.Criticize, _balance);

            Assert.AreEqual(47, p.state.morale, "Criticize -3");
        }

        [Test]
        public void OnInterview_PromiseStubs_DoNotChangeMorale()
        {
            var state = NewState();
            var p = NewPlayer(1, clubId: 1, morale: 50, professionalism: 50);
            state.AddPlayer(p);

            MoraleSystem.OnInterview(state, 1, InterviewType.PromisePlaytime, _balance);
            MoraleSystem.OnInterview(state, 1, InterviewType.PromiseRenewal, _balance);

            Assert.AreEqual(50, p.state.morale, "G.2 stub — morale 변동 X");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private GameState NewState() =>
            new GameState { currentDate = _today, randomSeed = 42 };

        private static Player NewPlayer(
            int id,
            int clubId,
            int morale = 50,
            int happiness = 70,
            int professionalism = 50,
            int loyalty = 50
        )
        {
            return new Player
            {
                id = id,
                currentClubId = clubId,
                currentAbility = 100,
                potentialAbility = 100,
                info = new PersonalInfo
                {
                    firstName = $"P{id}",
                    lastName = "Test",
                    primaryPosition = Position.CM,
                    birthDate = new DateTime(2000, 1, 1),
                },
                stats = new Stats(),
                state = new PlayerState { morale = morale, happiness = happiness },
                hiddenAttrs = new HiddenAttributes
                {
                    loyalty = loyalty,
                    professionalism = professionalism,
                },
            };
        }
    }
}
