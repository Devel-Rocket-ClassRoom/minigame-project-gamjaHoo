// ManagerReputationSystem.cs
// V1.0 M.8 — 매니저 평판 변동.
// Stateless (design-decisions.md #3).

using System;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class ManagerReputationSystem
    {
        public static void OnLeagueWin(GameState state, GameBalanceSO balance)
        {
            foreach (var league in state.leagues)
            {
                if (league?.standings?.entries == null || league.standings.entries.Count == 0)
                    continue;
                var winner = league
                    .standings.entries.OrderByDescending(e => e.points)
                    .ThenByDescending(e => e.goalsFor - e.goalsAgainst)
                    .First();
                if (winner.clubId == state.userClubId)
                {
                    Apply(state, balance.managerRepLeagueWin);
                    return;
                }
            }
        }

        public static void OnMonthlyManagerAward(GameState state, GameBalanceSO balance) =>
            Apply(state, balance.managerRepMonthlyAward);

        public static void OnManagerSacked(GameState state, GameBalanceSO balance) =>
            Apply(state, balance.managerRepSacked);

        private static void Apply(GameState state, int delta) =>
            state.managerReputation = Math.Max(0, Math.Min(100, state.managerReputation + delta));
    }
}
