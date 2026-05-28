// BoardSystem.cs
// V1.0 M.4 — 보드 평가 + 경질.
// V1.0 M.5 — GenerateSeasonPromises (TransferIn) + RejectPromise.
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

        // 시즌 시작 시 유저 구단 보드 약속 생성 (TransferIn).
        // 스쿼드에서 가장 얕은 포지션 그룹을 타겟으로 1개 생성.
        public static void GenerateSeasonPromises(GameState state, GameBalanceSO balance)
        {
            if (state == null || balance == null)
                return;

            var userClub = state.GetClub(state.userClubId);
            if (userClub?.season == null)
                return;

            // 이미 PendingReview 약속이 있으면 중복 생성 안 함
            foreach (var p in userClub.season.boardPromises)
                if (p.status == BoardPromiseStatus.PendingReview)
                    return;

            var position = FindWeakestPosition(state, userClub);
            var deadline = new DateTime(
                state.currentDate.Year,
                balance.transferWindowSummerEndMonth,
                balance.transferWindowSummerEndDay
            );

            var promise = new BoardPromise
            {
                id = state.nextPromiseId++,
                type = BoardPromiseType.TransferIn,
                status = BoardPromiseStatus.PendingReview,
                targetPosition = position,
                deadline = deadline,
                madeAt = state.currentDate,
            };
            userClub.season.boardPromises.Add(promise);
        }

        // 매니저가 보드 약속을 거절 → boardConfidence -N.
        public static void RejectPromise(
            GameState state,
            GameBalanceSO balance,
            BoardPromise promise
        )
        {
            if (state == null || balance == null || promise == null)
                return;

            promise.status = BoardPromiseStatus.Broken;

            var userClub = state.GetClub(state.userClubId);
            if (userClub?.season == null)
                return;

            userClub.season.boardConfidence = Math.Max(
                0,
                userClub.season.boardConfidence - balance.boardPromiseRejectPenalty
            );
            CheckThresholds(balance, userClub);
        }

        private static Position FindWeakestPosition(GameState state, Club club)
        {
            // 4라인별 선수 수 집계 → 가장 적은 라인의 대표 포지션 반환
            int gk = 0,
                df = 0,
                mf = 0,
                at = 0;
            foreach (var pid in club.seniorSquadIds)
            {
                var p = state.GetPlayer(pid);
                if (p?.info == null)
                    continue;
                switch (p.info.primaryPosition)
                {
                    case Position.GK:
                        gk++;
                        break;
                    case Position.CB:
                    case Position.LB:
                    case Position.RB:
                    case Position.WB:
                        df++;
                        break;
                    case Position.DM:
                    case Position.CM:
                    case Position.AM:
                    case Position.LM:
                    case Position.RM:
                    case Position.LW:
                    case Position.RW:
                        mf++;
                        break;
                    case Position.ST:
                    case Position.CF:
                        at++;
                        break;
                }
            }

            // 4-4-2 기준 부족 비율: GK 목표 1 / DF 4 / MF 4 / AT 2
            float gkRatio = gk > 0 ? (float)gk / 1f : 0f;
            float dfRatio = df > 0 ? (float)df / 4f : 0f;
            float mfRatio = mf > 0 ? (float)mf / 4f : 0f;
            float atRatio = at > 0 ? (float)at / 2f : 0f;

            float min = Math.Min(gkRatio, Math.Min(dfRatio, Math.Min(mfRatio, atRatio)));

            if (min == gkRatio)
                return Position.GK;
            if (min == dfRatio)
                return Position.CB;
            if (min == mfRatio)
                return Position.CM;
            return Position.ST;
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
