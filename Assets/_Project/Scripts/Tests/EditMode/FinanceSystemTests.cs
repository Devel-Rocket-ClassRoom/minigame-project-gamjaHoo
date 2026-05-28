// FinanceSystemTests.cs
// DoD: v1.0-tasks.md M.6 — 재정 결산 (입장료 + TV 중계권 + 상금)

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class FinanceSystemTests
    {
        private GameBalanceSO _balance;
        private GameState _state;
        private League _league;

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();

            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.baseMatchDayIncome = 50_000;
            _balance.baseTvIncome = 1_500_000;
            _balance.basePrize = 5_000_000;
            _balance.transferBudgetRatio = 0.20f;
            _balance.wageBudgetRatio = 0.50f;

            _state = new GameState { currentDate = new DateTime(2025, 5, 15) };

            _league = new League { id = 1, seasonYear = 2025 };
            _league.standings = new Standings();
            _league.schedule = new List<Match>();
            _state.leagues.Add(_league);
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. 입장료 — 홈 매치 수 × stadiumLevel × rep 비율 × base ─────

        [Test]
        public void T1_MatchDayIncome_Applied()
        {
            var club = MakeClub(1, rep: 100, stadiumLevel: 1);
            AddHomeMatches(club.id, played: 3);

            FinanceSystem.ProcessSeasonFinance(_state, _balance);

            int expected = (int)(50_000 * 1 * 1.0f * 3);
            Assert.AreEqual(expected, club.finance.money, "T1: 입장료 = base × level × (rep/100) × homeMatches");
        }

        // ── T2. TV 중계권 — rep 비율 적용 ────────────────────────────────

        [Test]
        public void T2_TvIncome_Applied()
        {
            var club = MakeClub(1, rep: 50, stadiumLevel: 1);

            FinanceSystem.ProcessSeasonFinance(_state, _balance);

            int expectedTv = (int)(1_500_000 * 0.5f);
            Assert.AreEqual(expectedTv, club.finance.money, "T2: TV = baseTvIncome × (rep/100)");
        }

        // ── T3. 상금 — 1위 최대, 최하위 0 ──────────────────────────────

        [Test]
        public void T3_Prize_FirstGetsMax_LastGetsZero()
        {
            var club1 = MakeClub(1, rep: 50, stadiumLevel: 1);
            var club2 = MakeClub(2, rep: 50, stadiumLevel: 1);

            // club1 1위 (3점), club2 2위 (0점) — 2팀 리그
            _league.standings.entries[0].points = 3; // club1
            _league.standings.entries[1].points = 0; // club2

            club1.finance.money = 0;
            club2.finance.money = 0;

            FinanceSystem.ProcessSeasonFinance(_state, _balance);

            int tv = (int)(1_500_000 * 0.5f); // rep=50

            // 2팀: position=1 → prize = basePrize × (2-1)/(2-1) = 5M
            // position=2 → prize = basePrize × (2-2)/(2-1) = 0
            Assert.AreEqual(tv + 5_000_000, club1.finance.money, "T3: 1위 prize = basePrize");
            Assert.AreEqual(tv, club2.finance.money, "T3: 최하위 prize = 0");
        }

        // ── T4. transferBudget / wageBudget 재계산 ───────────────────────

        [Test]
        public void T4_Budgets_Recalculated()
        {
            var club = MakeClub(1, rep: 100, stadiumLevel: 1);
            club.finance.money = 10_000_000;

            FinanceSystem.ProcessSeasonFinance(_state, _balance);

            int expectedTransfer = (int)(club.finance.money * 0.20f);
            int expectedWage = (int)(club.finance.money * 0.50f);
            Assert.AreEqual(expectedTransfer, club.finance.transferBudget, "T4: transferBudget = money × 0.20");
            Assert.AreEqual(expectedWage, club.finance.wageBudget, "T4: wageBudget = money × 0.50");
        }

        // ── T5. 홈 경기 없는 구단 — 입장료 0 ────────────────────────────

        [Test]
        public void T5_NoHomeMatches_ZeroMatchDay()
        {
            var club = MakeClub(1, rep: 100, stadiumLevel: 5);
            // 홈 경기 추가 안 함

            FinanceSystem.ProcessSeasonFinance(_state, _balance);

            int expectedTv = (int)(1_500_000 * 1.0f);
            Assert.AreEqual(expectedTv, club.finance.money, "T5: 홈 경기 없으면 입장료 0, TV만");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private Club MakeClub(int id, int rep, int stadiumLevel)
        {
            var club = new Club
            {
                id = id,
                reputation = rep,
                facilities = new Facilities { stadiumLevel = stadiumLevel },
                finance = new Finance { money = 0 },
                season = new SeasonState(),
            };
            _state.AddClub(club);
            _league.clubIds.Add(id);
            _league.standings.entries.Add(new StandingEntry { clubId = id });
            return club;
        }

        private void AddHomeMatches(int clubId, int played)
        {
            for (int i = 0; i < played; i++)
            {
                _league.schedule.Add(
                    new Match
                    {
                        id = _league.schedule.Count + 1,
                        homeClubId = clubId,
                        awayClubId = 999,
                        result = new MatchResult(),
                    }
                );
            }
        }
    }
}
