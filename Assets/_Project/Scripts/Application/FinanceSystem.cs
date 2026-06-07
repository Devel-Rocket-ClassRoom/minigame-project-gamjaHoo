// FinanceSystem.cs
// 구단 재정 — 월별 현금흐름(주급 유출 + TV/Matchday 수입) + 시즌말 상금.
// Stateless (design-decisions.md #3). 도메인은 GBP base 유지(#61 — 통화 표시 영향 0).
//
// V1.0 R.3 재설계 (design-decisions.md #74 / algorithms.md V1.0-16):
//  - 상시 유출(주급) 신설 + 수입 정상화(매출 ≈ 임금 × 1.6, EPL 63% 앵커).
//  - 수입 분산: TV=월별, Matchday=직전 월 홈경기 집계, Prize=시즌말.
//  - ProcessMonthly = DailyProcessor Day==1 훅. ProcessSeasonFinance = 상금 전용.
//  - 월별 항목은 public 쿼리 헬퍼로 노출 → FinanceController(재정 씬) 와 단일 진실 소스 공유.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class FinanceSystem
    {
        private const double WeeksPerMonth = 52.0 / 12.0; // 주급 → 월 환산 (달력 상수)

        // ── 월별 현금흐름 (DailyProcessor Day==1) ──────────────────────────
        // 전 활성 구단: 주급 차감 + TV 월할 + 직전 캘린더 월 홈경기 matchday + 예산 재계산.
        public static void ProcessMonthly(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            foreach (var club in state.allClubs)
            {
                if (club == null || !club.isActiveSimulation)
                    continue;
                if (club.finance == null)
                    club.finance = new Finance();

                club.finance.money -= MonthlyWage(club, state); // (A) 주급 유출
                club.finance.money += MonthlyTvIncome(club, balance); // (B-1) TV 월할
                club.finance.money += LastMonthMatchday(club, state, balance); // (B-2) 직전 월 홈경기

                RecalculateBudgets(club, balance);
            }
        }

        // ── 시즌말 상금 (SeasonEndProcessor) ───────────────────────────────
        // V1.0 R.3: TV/Matchday 는 월처리로 이관 → 여기선 순위 기반 상금만.
        public static void ProcessSeasonFinance(GameState state, GameBalanceSO balance)
        {
            foreach (var league in state.leagues)
            {
                if (league?.standings?.entries == null)
                    continue;

                int totalClubs = league.standings.entries.Count;
                if (totalClubs == 0)
                    continue;

                var sorted = GetSortedEntries(league.standings);

                for (int i = 0; i < sorted.Count; i++)
                {
                    int position = i + 1;
                    var club = state.GetClub(sorted[i].clubId);
                    if (club == null)
                        continue;
                    if (club.finance == null)
                        club.finance = new Finance();

                    club.finance.money += ComputePrize(position, totalClubs, balance);
                    RecalculateBudgets(club, balance);
                }
            }
        }

        // ── Public 쿼리 헬퍼 (ProcessMonthly + FinanceController 공유) ──────

        /// 스쿼드 주 임금 합.
        public static long WeeklyWage(Club club, GameState state)
        {
            long sum = 0;
            if (club?.seniorSquadIds == null)
                return 0;
            foreach (int pid in club.seniorSquadIds)
            {
                var p = state.GetPlayer(pid);
                if (p?.contract != null)
                    sum += p.contract.weeklyWage;
            }
            return sum;
        }

        /// 월 주급 (= 주 임금 × 52/12).
        public static int MonthlyWage(Club club, GameState state) =>
            (int)Math.Round(WeeklyWage(club, state) * WeeksPerMonth);

        /// 연 주급 (= 주 임금 × 52).
        public static long AnnualWage(Club club, GameState state) => WeeklyWage(club, state) * 52;

        /// 연 TV/중계 수입 (= base + repCoeff × rep).
        public static long AnnualTvIncome(Club club, GameBalanceSO balance) =>
            (long)balance.baseTvIncome + (long)(balance.tvRepCoeff * (club?.reputation ?? 0));

        /// 월 TV/중계 수입 (= 연 TV / 12).
        public static int MonthlyTvIncome(Club club, GameBalanceSO balance) =>
            (int)Math.Round(AnnualTvIncome(club, balance) / 12.0);

        /// 직전 캘린더 월에 치른 홈경기 matchday 수입 (= base × stadiumLevel × 홈경기 수).
        public static int LastMonthMatchday(Club club, GameState state, GameBalanceSO balance)
        {
            int homeMatches = CountClubHomeMatchesInPrevMonth(club, state);
            if (homeMatches <= 0)
                return 0;
            int stadiumLevel = club.facilities?.stadiumLevel ?? 1;
            return balance.baseMatchDayIncome * stadiumLevel * homeMatches;
        }

        /// 한 시즌 추정 matchday (= base × stadiumLevel × 홈경기 총수). 재정 씬 표시용.
        public static int ProjectedSeasonMatchday(Club club, GameState state, GameBalanceSO balance)
        {
            int homeMatches = CountClubHomeMatchesInSeason(club, state);
            int stadiumLevel = club.facilities?.stadiumLevel ?? 1;
            return balance.baseMatchDayIncome * stadiumLevel * homeMatches;
        }

        // ── 내부 헬퍼 ──────────────────────────────────────────────────────

        // 직전 캘린더 월 [전월1일, 당월1일) 에 치른(result != null) 홈경기 수.
        // currentDate 는 Day==1 호출 가정 — 연말 wrap 은 AddMonths 가 처리.
        private static int CountClubHomeMatchesInPrevMonth(Club club, GameState state)
        {
            if (club == null)
                return 0;
            var thisMonthStart = new DateTime(state.currentDate.Year, state.currentDate.Month, 1);
            var prevMonthStart = thisMonthStart.AddMonths(-1);

            int count = 0;
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                {
                    if (m?.result == null || m.homeClubId != club.id)
                        continue;
                    if (m.date >= prevMonthStart && m.date < thisMonthStart)
                        count++;
                }
            }
            return count;
        }

        // 현재 시즌 일정의 클럽 홈경기 총수 (결과 무관 — 추정용).
        private static int CountClubHomeMatchesInSeason(Club club, GameState state)
        {
            if (club == null)
                return 0;
            int count = 0;
            foreach (var league in state.leagues)
            {
                if (league?.schedule == null)
                    continue;
                foreach (var m in league.schedule)
                {
                    if (m != null && m.homeClubId == club.id)
                        count++;
                }
            }
            return count;
        }

        private static void RecalculateBudgets(Club club, GameBalanceSO balance)
        {
            club.finance.transferBudget = (int)(club.finance.money * balance.transferBudgetRatio);
            club.finance.wageBudget = (int)(club.finance.money * balance.wageBudgetRatio);
        }

        private static int ComputePrize(int position, int totalClubs, GameBalanceSO balance)
        {
            if (totalClubs <= 1)
                return 0;
            return (int)((float)balance.basePrize * (totalClubs - position) / (totalClubs - 1));
        }

        private static List<StandingEntry> GetSortedEntries(Standings standings) =>
            standings
                .entries.OrderByDescending(e => e.points)
                .ThenByDescending(e => e.goalsFor - e.goalsAgainst)
                .ThenByDescending(e => e.goalsFor)
                .ToList();
    }
}
