// BoardSystem.cs
// V1.0 M.4 — 보드 평가 + 경질.
// ProcessMatchResult: 매치 결과 반영 (패배/빅매치패배/승리).
// EvaluateMonthly: 매월 1일 순위 기반 보드 평가.
// Stateless (design-decisions.md #3).

using System;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class BoardSystem
    {
        // 매치 결과 → boardConfidence 변동. 유저 구단 매치만 적용.
        public static void ProcessMatchResult(
            GameState state,
            GameBalanceSO balance,
            Match match,
            League league
        )
        {
            if (state == null || balance == null || match == null || match.result == null)
                return;

            bool userIsHome = match.homeClubId == state.userClubId;
            bool userIsAway = match.awayClubId == state.userClubId;
            if (!userIsHome && !userIsAway)
                return;

            var userClub = state.GetClub(state.userClubId);
            if (userClub?.season == null)
                return;

            var result = match.result;
            int userScore = userIsHome ? result.homeScore : result.awayScore;
            int oppScore = userIsHome ? result.awayScore : result.homeScore;
            int opponentId = userIsHome ? match.awayClubId : match.homeClubId;

            if (userScore < oppScore)
            {
                int loss = balance.boardConfidenceLossPerDefeat;
                var opp = state.GetClub(opponentId);
                if (opp != null && opp.reputation >= balance.bigMatchReputationThreshold)
                    loss += balance.boardConfidenceBigMatchLossExtra;
                userClub.season.boardConfidence -= loss;
            }
            else if (userScore > oppScore)
            {
                userClub.season.boardConfidence += balance.boardConfidenceWinGain;
            }

            userClub.season.boardConfidence = Math.Max(
                0,
                Math.Min(100, userClub.season.boardConfidence)
            );
            CheckThresholds(balance, userClub);
        }

        // 매월 1일 — 순위 목표 대비 실제 순위 평가.
        public static void EvaluateMonthly(GameState state, GameBalanceSO balance)
        {
            if (state == null || balance == null)
                return;

            var userClub = state.GetClub(state.userClubId);
            if (userClub?.season == null)
                return;

            League userLeague = null;
            foreach (var league in state.leagues)
            {
                if (league?.clubIds != null && league.clubIds.Contains(state.userClubId))
                {
                    userLeague = league;
                    break;
                }
            }

            if (userLeague?.standings?.entries == null || userLeague.standings.entries.Count == 0)
                return;

            var sorted = userLeague.standings.entries.OrderByDescending(e => e.points).ToList();
            int actualPosition = sorted.FindIndex(e => e.clubId == state.userClubId) + 1;
            if (actualPosition <= 0)
                return;

            int target = userClub.season.targetLeaguePosition;
            int delta = target - actualPosition; // 양수 = 목표보다 상위
            int change = (int)Math.Round(delta * balance.boardConfidenceRankMultiplier);

            userClub.season.boardConfidence = Math.Max(
                0,
                Math.Min(100, userClub.season.boardConfidence + change)
            );
            CheckThresholds(balance, userClub);
        }

        private static void CheckThresholds(GameBalanceSO balance, Club club)
        {
            if (club.season.boardConfidence < balance.boardSackedThreshold)
                EventBus.Publish(
                    new ManagerSackedEvent { boardConfidence = club.season.boardConfidence }
                );
            else if (club.season.boardConfidence < balance.boardWarningThreshold)
                EventBus.Publish(
                    new BoardWarningEvent { boardConfidence = club.season.boardConfidence }
                );
        }
    }
}
