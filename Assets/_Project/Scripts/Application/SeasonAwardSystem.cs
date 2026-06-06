// SeasonAwardSystem.cs
// V0.5 M.2: 시즌 어워드 계산 (algorithms.md V0.5-9).
// V0.5 M.3: 월간 어워드 계산 (ComputeMonthlyAwards).
// Stateless (design-decisions.md #3).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class SeasonAwardSystem
    {
        public static List<SeasonAward> ComputeSeasonAwards(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var awards = new List<SeasonAward>();
            var now = state.currentDate;

            foreach (var league in state.leagues)
            {
                if (league == null)
                    continue;

                var acc = LeaderboardSystem.BuildLeagueStats(league);
                if (acc.Count == 0)
                    continue;

                var leaguePlayers = league
                    .clubIds.SelectMany(cid =>
                        state.GetClub(cid)?.seniorSquadIds ?? Enumerable.Empty<int>()
                    )
                    .Select(pid => state.GetPlayer(pid))
                    .Where(p => p != null && acc.ContainsKey(p.id))
                    .ToList();

                if (leaguePlayers.Count == 0)
                    continue;

                // TopScorer
                var topScorer = leaguePlayers.OrderByDescending(p => acc[p.id].goals).First();
                awards.Add(MakeSingle(AwardType.TopScorer, league, now, topScorer.id));

                // TopAssist
                var topAssist = leaguePlayers.OrderByDescending(p => acc[p.id].assists).First();
                awards.Add(MakeSingle(AwardType.TopAssist, league, now, topAssist.id));

                // LeagueMVP — 평균 평점 + 우승팀 가산
                var champion = league
                    .standings?.entries.OrderByDescending(e => e.points)
                    .FirstOrDefault();
                var mvp = leaguePlayers
                    .OrderByDescending(p =>
                    {
                        var a = acc[p.id];
                        float r = a.apps > 0 ? a.ratingSum / a.apps : 0f;
                        if (champion != null && p.currentClubId == champion.clubId)
                            r += balance.mvpChampionBonus;
                        return r;
                    })
                    .First();
                awards.Add(MakeSingle(AwardType.LeagueMVP, league, now, mvp.id));

                // YoungPlayer (youngPlayerMaxAge 이하)
                var youngCandidates = leaguePlayers
                    .Where(p => GetAge(p, now) <= balance.youngPlayerMaxAge)
                    .ToList();
                if (youngCandidates.Count > 0)
                {
                    var young = youngCandidates
                        .OrderByDescending(p =>
                        {
                            var a = acc[p.id];
                            return a.apps > 0 ? a.ratingSum / a.apps : 0f;
                        })
                        .First();
                    awards.Add(MakeSingle(AwardType.YoungPlayer, league, now, young.id));
                }

                // BestEleven — 포지션 버킷별 평균 평점 최고
                var bestEleven = SelectBestEleven(leaguePlayers, acc, now);
                if (bestEleven.Count > 0)
                    awards.Add(
                        new SeasonAward
                        {
                            type = AwardType.BestEleven,
                            playerIds = bestEleven,
                            leagueId = league.id,
                            seasonYear = league.seasonYear,
                            awardedAt = now,
                        }
                    );

                // GoldenGlove — GK 무실점 경기 수
                var gkCandidates = leaguePlayers
                    .Where(p => p.info?.primaryPosition == Position.GK)
                    .ToList();
                if (gkCandidates.Count > 0)
                {
                    var gg = gkCandidates.OrderByDescending(p => acc[p.id].cleanSheets).First();
                    awards.Add(MakeSingle(AwardType.GoldenGlove, league, now, gg.id));
                }
            }

            // ManagerOfSeason — 첫 번째 리그 우승팀 매니저 (playerIds 비어있음)
            var firstLeague = state.leagues.FirstOrDefault(l => l?.standings?.entries?.Count > 0);
            if (firstLeague != null)
                awards.Add(
                    new SeasonAward
                    {
                        type = AwardType.ManagerOfSeason,
                        playerIds = new List<int>(),
                        leagueId = firstLeague.id,
                        seasonYear = firstLeague.seasonYear,
                        awardedAt = now,
                    }
                );

            // 수상 선수 morale / happiness 가산 + AwardWonEvent
            foreach (var award in awards)
            {
                foreach (var pid in award.playerIds)
                {
                    var player = state.GetPlayer(pid);
                    if (player?.state == null)
                        continue;
                    player.state.morale = Math.Min(
                        100,
                        player.state.morale + balance.awardMoraleBonus
                    );
                    player.state.happiness = Math.Min(
                        100,
                        player.state.happiness + balance.awardHappinessBonus
                    );
                    EventBus.Publish(new AwardWonEvent { awardType = award.type, playerId = pid });
                }
            }

            // state + history 기록
            state.activeAwards.AddRange(awards);
            foreach (var league in state.leagues)
            {
                if (league == null)
                    continue;
                var leagueAwards = awards.Where(a => a.leagueId == league.id).ToList();
                league.history.Add(
                    new SeasonHistory
                    {
                        seasonYear = league.seasonYear,
                        standings = league.standings?.Clone(),
                        awards = leagueAwards,
                    }
                );
            }

            return awards;
        }

        public static List<SeasonAward> ComputeMonthlyAwards(GameState state, GameBalanceSO balance)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var awards = new List<SeasonAward>();
            var now = state.currentDate;
            var monthStart = now.AddMonths(-1);

            foreach (var league in state.leagues)
            {
                if (league == null || league.clubIds.Count == 0)
                    continue;

                // Manager of the Month — 직전 월 승률 최고 클럽
                var topManagerClubId = league
                    .clubIds.OrderByDescending(cid => MonthlyWinRate(cid, league, monthStart, now))
                    .First();

                awards.Add(
                    new SeasonAward
                    {
                        type = AwardType.MonthlyManagerOfMonth,
                        playerIds = new List<int>(),
                        leagueId = league.id,
                        seasonYear = league.seasonYear,
                        awardedAt = now,
                    }
                );

                var topManagerClub = state.GetClub(topManagerClubId);
                if (topManagerClub?.season != null)
                    topManagerClub.season.boardConfidence = Math.Min(
                        100,
                        topManagerClub.season.boardConfidence
                            + balance.monthlyManagerConfidenceBonus
                    );
                if (topManagerClubId == state.userClubId)
                    ManagerReputationSystem.OnMonthlyManagerAward(state, balance);

                // Player of the Month — 직전 월 평점 + 골/어시
                var monthPlayers = league
                    .clubIds.SelectMany(cid =>
                        state.GetClub(cid)?.seniorSquadIds ?? Enumerable.Empty<int>()
                    )
                    .Select(pid => state.GetPlayer(pid))
                    .Where(p => p != null)
                    .ToList();

                if (monthPlayers.Count > 0)
                {
                    var topPlayer = monthPlayers
                        .OrderByDescending(p => MonthlyScore(p.id, league, monthStart, now))
                        .First();

                    awards.Add(
                        MakeSingle(AwardType.MonthlyPlayerOfMonth, league, now, topPlayer.id)
                    );

                    if (topPlayer.state != null)
                        topPlayer.state.morale = Math.Min(
                            100,
                            topPlayer.state.morale + balance.monthlyPlayerMoraleBonus
                        );
                }
            }

            state.activeAwards.AddRange(awards);
            return awards;
        }

        // ── helpers ──────────────────────────────────────────────────────

        private static SeasonAward MakeSingle(
            AwardType type,
            League league,
            DateTime awardedAt,
            int playerId
        ) =>
            new SeasonAward
            {
                type = type,
                playerIds = new List<int> { playerId },
                leagueId = league.id,
                seasonYear = league.seasonYear,
                awardedAt = awardedAt,
            };

        // 리그 전 경기 집계 → playerId별 스탯
        // BestEleven: GK=1, DF=4, MF=4, AT=2 (4-4-2)
        private static List<int> SelectBestEleven(
            List<Player> players,
            Dictionary<int, PlayerLeagueStats> acc,
            DateTime now
        )
        {
            float AvgRating(Player p) => acc.TryGetValue(p.id, out var a) ? a.AvgRating : 0f;

            var gk = players.Where(p => IsGk(p)).OrderByDescending(AvgRating).Take(1).ToList();
            var df = players.Where(p => IsDf(p)).OrderByDescending(AvgRating).Take(4).ToList();
            var mf = players.Where(p => IsMf(p)).OrderByDescending(AvgRating).Take(4).ToList();
            var at = players.Where(p => IsAt(p)).OrderByDescending(AvgRating).Take(2).ToList();

            var all = gk.Concat(df).Concat(mf).Concat(at).ToList();
            return all.Select(p => p.id).ToList();
        }

        private static bool IsGk(Player p) => p.info?.primaryPosition == Position.GK;

        private static bool IsDf(Player p)
        {
            var pos = p.info?.primaryPosition;
            return pos == Position.CB
                || pos == Position.LB
                || pos == Position.RB
                || pos == Position.WB;
        }

        private static bool IsMf(Player p)
        {
            var pos = p.info?.primaryPosition;
            return pos == Position.DM
                || pos == Position.CM
                || pos == Position.AM
                || pos == Position.LM
                || pos == Position.RM
                || pos == Position.LW
                || pos == Position.RW;
        }

        private static bool IsAt(Player p)
        {
            var pos = p.info?.primaryPosition;
            return pos == Position.ST || pos == Position.CF;
        }

        // 직전 월 클럽 승률
        private static float MonthlyWinRate(
            int clubId,
            League league,
            DateTime monthStart,
            DateTime monthEnd
        )
        {
            int wins = 0,
                played = 0;
            foreach (var match in league.schedule)
            {
                if (match?.result == null)
                    continue;
                if (match.date < monthStart || match.date >= monthEnd)
                    continue;
                if (match.homeClubId != clubId && match.awayClubId != clubId)
                    continue;
                played++;
                bool homeWin = match.result.homeScore > match.result.awayScore;
                if (
                    (match.homeClubId == clubId && homeWin)
                    || (match.awayClubId == clubId && !homeWin)
                )
                    wins++;
            }
            return played > 0 ? (float)wins / played : 0f;
        }

        // 직전 월 선수 점수 (평균 평점 + 골 + 어시)
        private static float MonthlyScore(
            int playerId,
            League league,
            DateTime monthStart,
            DateTime monthEnd
        )
        {
            float ratingSum = 0f;
            int apps = 0,
                goals = 0,
                assists = 0;
            foreach (var match in league.schedule)
            {
                if (match?.result == null)
                    continue;
                if (match.date < monthStart || match.date >= monthEnd)
                    continue;
                foreach (var ps in match.result.playerStats)
                {
                    if (ps.playerId != playerId)
                        continue;
                    apps++;
                    ratingSum += ps.rating;
                    goals += ps.goals;
                    assists += ps.assists;
                }
            }
            float avgRating = apps > 0 ? ratingSum / apps : 0f;
            return avgRating + goals + assists;
        }

        private static int GetAge(Player p, DateTime date)
        {
            int age = date.Year - p.info.birthDate.Year;
            if (date.DayOfYear < p.info.birthDate.DayOfYear)
                age--;
            return age;
        }
    }
}
