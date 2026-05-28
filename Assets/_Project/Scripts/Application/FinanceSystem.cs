// FinanceSystem.cs
// V1.0 M.6 — 시즌 종료 재정 결산.
// SeasonEndProcessor.Run 에서 호출. Stateless (design-decisions.md #3).

using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class FinanceSystem
    {
        public static void ProcessSeasonFinance(GameState state, GameBalanceSO balance)
        {
            foreach (var league in state.leagues)
            {
                if (league?.standings?.entries == null || league.schedule == null)
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

                    int income =
                        ComputeMatchDay(club, league.schedule, balance)
                        + ComputeTv(club, balance)
                        + ComputePrize(position, totalClubs, balance);

                    club.finance.money += income;
                    club.finance.transferBudget = (int)(
                        club.finance.money * balance.transferBudgetRatio
                    );
                    club.finance.wageBudget = (int)(club.finance.money * balance.wageBudgetRatio);
                }
            }
        }

        private static int ComputeMatchDay(Club club, List<Match> schedule, GameBalanceSO balance)
        {
            if (club.facilities == null)
                return 0;
            int homeMatches = schedule.Count(m => m.homeClubId == club.id && m.result != null);
            return (int)(
                balance.baseMatchDayIncome
                * club.facilities.stadiumLevel
                * (club.reputation / 100f)
                * homeMatches
            );
        }

        private static int ComputeTv(Club club, GameBalanceSO balance) =>
            (int)(balance.baseTvIncome * (club.reputation / 100f));

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
