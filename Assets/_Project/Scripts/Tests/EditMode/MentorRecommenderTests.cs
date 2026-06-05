// MentorRecommenderTests.cs
// V1.0 I.3 — MentorRecommender 점수/추천 + I.2 진행률 헬퍼(MentoringSystem) 검증.

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class MentorRecommenderTests
    {
        private static readonly DateTime BaseDate = new DateTime(2025, 8, 1);
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
        }

        // ── T1: leadership 높을수록 점수 ↑ (나머지 동일) ─────────────────

        [Test]
        public void T1_HigherLeadership_HigherScore()
        {
            var low = MakePlayer(1, leadership: 10, ageYears: 28, contractYears: 2, hidden: 50);
            var high = MakePlayer(2, leadership: 18, ageYears: 28, contractYears: 2, hidden: 50);

            Assert.Greater(
                MentorRecommender.Score(high, BaseDate, _balance),
                MentorRecommender.Score(low, BaseDate, _balance),
                "T1: leadership 높은 쪽 점수 우세"
            );
        }

        // ── T2: Hidden 평균 높을수록 점수 ↑ (나머지 동일) ───────────────

        [Test]
        public void T2_HigherHiddenMean_HigherScore()
        {
            var poor = MakePlayer(1, leadership: 15, ageYears: 28, contractYears: 2, hidden: 30);
            var good = MakePlayer(2, leadership: 15, ageYears: 28, contractYears: 2, hidden: 90);

            Assert.Greater(
                MentorRecommender.Score(good, BaseDate, _balance),
                MentorRecommender.Score(poor, BaseDate, _balance),
                "T2: Hidden(prof/amb/loy) 평균 높은 쪽 점수 우세"
            );
        }

        // ── T3: RecommendMentor 가 최고 점수 후보 선택 + 빈 입력 -1 ───────

        [Test]
        public void T3_RecommendMentor_PicksBest()
        {
            var state = new GameState { currentDate = BaseDate };
            var a = MakePlayer(1, leadership: 8, ageYears: 22, contractYears: 1, hidden: 40);
            var b = MakePlayer(2, leadership: 17, ageYears: 31, contractYears: 3, hidden: 85); // 명백한 최고
            var c = MakePlayer(3, leadership: 12, ageYears: 26, contractYears: 2, hidden: 60);
            state.AddPlayer(a);
            state.AddPlayer(b);
            state.AddPlayer(c);

            int best = MentorRecommender.RecommendMentor(
                new List<int> { 1, 2, 3 },
                state,
                _balance
            );
            Assert.AreEqual(2, best, "T3: 최고 점수 후보(b) 선택");

            Assert.AreEqual(
                -1,
                MentorRecommender.RecommendMentor(new List<int>(), state, _balance),
                "T3: 빈 후보 → -1"
            );
        }

        // ── T4: ConvergencePercent — mentee→mentor 근접도 ───────────────

        [Test]
        public void T4_ConvergencePercent()
        {
            Assert.AreEqual(100f, MentoringSystem.ConvergencePercent(80, 80), "동일 → 100%");
            Assert.AreEqual(100f, MentoringSystem.ConvergencePercent(60, 90), "초과 → 100%");
            Assert.AreEqual(50f, MentoringSystem.ConvergencePercent(80, 40), 0.01f, "40/80 → 50%");
            Assert.AreEqual(100f, MentoringSystem.ConvergencePercent(0, 0), "mentor 0 → 100%");
        }

        // ── T5: ProjectedMonthlyStep — 격차 비례 + rateCap 상한 + 부호 ───

        [Test]
        public void T5_ProjectedMonthlyStep_GapProportional()
        {
            // 큰 격차: |diff|×fraction 이 rateCap 초과 → cap 으로 제한
            Assert.AreEqual(5, MentoringSystem.ProjectedMonthlyStep(80, 20, 5, 0.15f), "큰 격차 → rateCap");
            Assert.AreEqual(-5, MentoringSystem.ProjectedMonthlyStep(20, 80, 5, 0.15f), "음수 방향 → -rateCap");
            // 격차 비례: cap 여유 있을 때 |diff|×fraction
            Assert.AreEqual(8, MentoringSystem.ProjectedMonthlyStep(90, 50, 20, 0.20f), "격차40×0.20=8");
            Assert.AreEqual(2, MentoringSystem.ProjectedMonthlyStep(60, 50, 20, 0.20f), "격차10×0.20=2");
            // 작은 격차: 반올림 0 → 최소 1 (멘토 수치까지 결국 도달)
            Assert.AreEqual(1, MentoringSystem.ProjectedMonthlyStep(53, 50, 5, 0.15f), "작은 격차 → 최소 1");
            // step 은 격차 초과 안 함 (멘토 수치 상한): 격차2, raw=round(2×1.5)=3 → 격차2로 제한
            Assert.AreEqual(2, MentoringSystem.ProjectedMonthlyStep(52, 50, 20, 1.5f), "격차 초과 금지");
            Assert.AreEqual(0, MentoringSystem.ProjectedMonthlyStep(50, 50, 5, 0.15f), "동일 → 0");
        }

        // ── T6: 격차 클수록 스텝 큼 (속도 차이 검증) ────────────────────

        [Test]
        public void T6_LargerGap_FasterStep()
        {
            int big = MentoringSystem.ProjectedMonthlyStep(90, 50, 20, 0.20f); // 격차 40
            int small = MentoringSystem.ProjectedMonthlyStep(60, 50, 20, 0.20f); // 격차 10
            Assert.Greater(big, small, "T6: 격차 클수록 월 스텝 큼");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static Player MakePlayer(
            int id,
            int leadership,
            int ageYears,
            int contractYears,
            int hidden
        )
        {
            return new Player
            {
                id = id,
                info = new PersonalInfo
                {
                    birthDate = BaseDate.AddYears(-ageYears),
                    primaryPosition = Position.CM,
                },
                hiddenAttrs = new HiddenAttributes
                {
                    professionalism = hidden,
                    ambition = hidden,
                    loyalty = hidden,
                },
                state = new PlayerState(),
                stats = new Stats
                {
                    technical = new TechnicalStats(),
                    mental = new MentalStats { leadership = leadership },
                    physical = new PhysicalStats(),
                    gk = new GoalkeepingStats(),
                },
                contract = new Contract { endDate = BaseDate.AddYears(contractYears) },
            };
        }
    }
}
