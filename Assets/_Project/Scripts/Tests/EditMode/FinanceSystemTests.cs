// FinanceSystemTests.cs
// V1.0 R.3 (#74) — 월별 현금흐름(주급 유출 + TV/Matchday) + 시즌말 상금 + 이적 자금 하드 차단.
// (V0.5 M.6 입장료/TV/상금 테스트는 ProcessMonthly 기준으로 마이그레이션됨.)

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
            _balance.baseMatchDayIncome = 150_000;
            _balance.baseTvIncome = 24_000_000;
            _balance.tvRepCoeff = 0f; // 테스트별로 필요 시 개별 설정
            _balance.basePrize = 20_000_000;
            _balance.transferBudgetRatio = 0.20f;
            _balance.wageBudgetRatio = 0.50f;

            // 월처리 테스트 기본 날짜 = 9/1 (Day==1) → 직전 캘린더 월 = 8월
            _state = new GameState { currentDate = new DateTime(2025, 9, 1) };

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

        // ══ ProcessMonthly — 주급 유출 ════════════════════════════════════

        [Test]
        public void T1_Monthly_WageDeducted()
        {
            _balance.baseTvIncome = 0; // 수입 격리 → 유출만 측정
            var club = MakeClub(1, rep: 50, stadiumLevel: 1, money: 10_000_000);
            AddSquadPlayer(club, pid: 101, weeklyWage: 10_000);
            AddSquadPlayer(club, pid: 102, weeklyWage: 20_000);
            AddSquadPlayer(club, pid: 103, weeklyWage: 30_000);

            FinanceSystem.ProcessMonthly(_state, _balance);

            // weekly=60,000 → monthly = round(60,000 × 52/12) = 260,000
            Assert.AreEqual(
                10_000_000 - 260_000,
                club.finance.money,
                "T1: 월 주급 = Σweekly × 52/12 차감"
            );
        }

        // ══ ProcessMonthly — TV (월할) ═══════════════════════════════════

        [Test]
        public void T2_Monthly_TvIncome_FlatPlusRep()
        {
            _balance.baseTvIncome = 24_000_000;
            _balance.tvRepCoeff = 200_000f;
            var club = MakeClub(1, rep: 50, stadiumLevel: 1, money: 0);
            // 스쿼드 없음(주급 0), 홈경기 없음 → TV 만

            FinanceSystem.ProcessMonthly(_state, _balance);

            long annualTv = 24_000_000L + (long)(200_000f * 50); // 34,000,000
            int monthlyTv = (int)Math.Round(annualTv / 12.0); // 2,833,333
            Assert.AreEqual(monthlyTv, club.finance.money, "T2: 월 TV = (base + coeff×rep) / 12");
        }

        // ══ ProcessMonthly — Matchday (직전 월 홈경기) ════════════════════

        [Test]
        public void T3_Monthly_Matchday_PrevMonthHomeOnly()
        {
            _balance.baseTvIncome = 0;
            var club = MakeClub(1, rep: 100, stadiumLevel: 2, money: 0);

            AddMatch(homeId: 1, date: new DateTime(2025, 8, 10), played: true); // 8월 홈 ✓
            AddMatch(homeId: 1, date: new DateTime(2025, 8, 25), played: true); // 8월 홈 ✓
            AddMatch(homeId: 1, date: new DateTime(2025, 8, 28), played: false); // 미완료 ✗
            AddMatch(homeId: 1, date: new DateTime(2025, 9, 5), played: true); // 당월 ✗
            AddMatch(homeId: 1, date: new DateTime(2025, 7, 20), played: true); // 두 달 전 ✗
            AddMatch(homeId: 2, date: new DateTime(2025, 8, 15), played: true); // 타 구단 홈 ✗

            FinanceSystem.ProcessMonthly(_state, _balance);

            // 8월 완료 홈경기 2개 × base(150,000) × stadium(2) = 600,000
            Assert.AreEqual(150_000 * 2 * 2, club.finance.money, "T3: 직전 월 완료 홈경기만 matchday");
        }

        // ══ ProcessMonthly — isActiveSimulation 게이팅 ════════════════════

        [Test]
        public void T4_Monthly_InactiveClub_Untouched()
        {
            var club = MakeClub(1, rep: 50, stadiumLevel: 1, money: 5_000_000);
            club.isActiveSimulation = false;
            AddSquadPlayer(club, pid: 201, weeklyWage: 50_000);

            FinanceSystem.ProcessMonthly(_state, _balance);

            Assert.AreEqual(5_000_000, club.finance.money, "T4: 비활성 구단은 월처리 제외");
        }

        // ══ ProcessMonthly — 예산 재계산 ═════════════════════════════════

        [Test]
        public void T5_Monthly_BudgetsRecalculated()
        {
            _balance.baseTvIncome = 0;
            var club = MakeClub(1, rep: 50, stadiumLevel: 1, money: 10_000_000);

            FinanceSystem.ProcessMonthly(_state, _balance);

            Assert.AreEqual(
                (int)(club.finance.money * 0.20f),
                club.finance.transferBudget,
                "T5: transferBudget = money × 0.20"
            );
            Assert.AreEqual(
                (int)(club.finance.money * 0.50f),
                club.finance.wageBudget,
                "T5: wageBudget = money × 0.50"
            );
        }

        // ══ ProcessSeasonFinance — 상금 전용 ══════════════════════════════

        [Test]
        public void T6_Season_PrizeOnly_NoTvNoMatchday()
        {
            var club1 = MakeClub(1, rep: 50, stadiumLevel: 1, money: 0);
            var club2 = MakeClub(2, rep: 50, stadiumLevel: 1, money: 0);
            AddHomeMatchesForSeason(club1.id, 3); // 시즌 finance 는 matchday 무시해야 함

            _league.standings.entries[0].points = 3; // club1 1위
            _league.standings.entries[1].points = 0; // club2 2위(최하위)

            FinanceSystem.ProcessSeasonFinance(_state, _balance);

            // 2팀: 1위 prize = basePrize×(2-1)/(2-1) = 20M / 최하위 = 0. TV·matchday 가산 없음.
            Assert.AreEqual(20_000_000, club1.finance.money, "T6: 1위 상금만 (TV/matchday 없음)");
            Assert.AreEqual(0, club2.finance.money, "T6: 최하위 상금 0");
        }

        // ══ 이적 자금 하드 차단 (#74) ════════════════════════════════════

        [Test]
        public void T7_Transfer_BlockedWhenInsufficientFunds()
        {
            var seller = MakeClub(1, rep: 50, stadiumLevel: 1, money: 0);
            var buyer = MakeClub(2, rep: 50, stadiumLevel: 1, money: 1_000_000);
            var player = AddSquadPlayer(seller, pid: 301, weeklyWage: 10_000);

            var proposed = new Contract { weeklyWage = 10_000 };

            // 잔액(1M) < 이적료(5M) → 차단
            Assert.Throws<InvalidOperationException>(
                () =>
                    TransferSystem.SubmitOffer(
                        player.id,
                        seller.id,
                        buyer.id,
                        5_000_000,
                        proposed,
                        _state,
                        _balance
                    ),
                "T7: 자금 부족 시 오퍼 차단"
            );
        }

        [Test]
        public void T8_Transfer_AllowedWhenSufficientFunds()
        {
            var seller = MakeClub(1, rep: 50, stadiumLevel: 1, money: 0);
            var buyer = MakeClub(2, rep: 50, stadiumLevel: 1, money: 10_000_000);
            var player = AddSquadPlayer(seller, pid: 401, weeklyWage: 10_000);

            var proposed = new Contract { weeklyWage = 10_000 };

            var offer = TransferSystem.SubmitOffer(
                player.id,
                seller.id,
                buyer.id,
                5_000_000,
                proposed,
                _state,
                _balance
            );

            Assert.IsNotNull(offer, "T8: 자금 충분 시 오퍼 생성");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private Club MakeClub(int id, int rep, int stadiumLevel, int money)
        {
            var club = new Club
            {
                id = id,
                reputation = rep,
                isActiveSimulation = true,
                facilities = new Facilities { stadiumLevel = stadiumLevel },
                finance = new Finance { money = money },
                season = new SeasonState(),
            };
            _state.AddClub(club);
            _league.clubIds.Add(id);
            _league.standings.entries.Add(new StandingEntry { clubId = id });
            return club;
        }

        private Player AddSquadPlayer(Club club, int pid, int weeklyWage)
        {
            var p = new Player
            {
                id = pid,
                currentClubId = club.id,
                contract = new Contract { weeklyWage = weeklyWage },
            };
            _state.AddPlayer(p);
            club.seniorSquadIds.Add(pid);
            return p;
        }

        private void AddMatch(int homeId, DateTime date, bool played)
        {
            _league.schedule.Add(
                new Match
                {
                    id = _league.schedule.Count + 1,
                    homeClubId = homeId,
                    awayClubId = 999,
                    date = date,
                    result = played ? new MatchResult() : null,
                }
            );
        }

        private void AddHomeMatchesForSeason(int clubId, int played)
        {
            for (int i = 0; i < played; i++)
                AddMatch(clubId, new DateTime(2025, 8, 10), played: true);
        }
    }
}
