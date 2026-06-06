// LeaderboardSystem.cs
// V1.0 R.11 (이슈 #528): 리그 개인 리더보드 — 득점 / 도움 / 평점 / GK 클린시트 / 출전.
// SeasonAwardSystem.BuildLeagueStats(현 private) 를 일반화해 시즌 중 조회 가능한 public 쿼리로 추출.
// Stateless (design-decisions.md #3).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public enum LeaderboardCategory
    {
        Goals,
        Assists,
        Rating,
        CleanSheets,
        Appearances,
    }

    // 리그 전 경기 누적 스탯 (playerId 단위). SeasonAwardSystem 공유.
    public class PlayerLeagueStats
    {
        public int playerId;
        public int apps;
        public int goals;
        public int assists;
        public float ratingSum;
        public int cleanSheets;

        public float AvgRating => apps > 0 ? ratingSum / apps : 0f;
    }

    // 리더보드 한 행 (정렬 + rank 부여 결과). 이름/색상 해석은 UI 컨트롤러 담당.
    public class LeaderboardEntry
    {
        public int rank;
        public int playerId;
        public int clubId;
        public float value; // 득점/도움/클린시트/출전 = 정수, 평점 = 평균
        public int apps;
    }

    public static class LeaderboardSystem
    {
        private const float RatingEpsilon = 0.001f;

        // 리그 전 경기 집계 → playerId별 스탯. (SeasonAwardSystem.ComputeSeasonAwards 와 공유)
        public static Dictionary<int, PlayerLeagueStats> BuildLeagueStats(League league)
        {
            var acc = new Dictionary<int, PlayerLeagueStats>();
            if (league?.schedule == null)
                return acc;

            foreach (var match in league.schedule)
            {
                if (match?.result == null)
                    continue;
                var result = match.result;

                foreach (var ps in result.playerStats)
                {
                    if (!acc.TryGetValue(ps.playerId, out var entry))
                    {
                        entry = new PlayerLeagueStats { playerId = ps.playerId };
                        acc[ps.playerId] = entry;
                    }
                    entry.apps++;
                    entry.goals += ps.goals;
                    entry.assists += ps.assists;
                    entry.ratingSum += ps.rating;
                }

                // 클린시트 — 무실점 팀 선발 11명에 가산 (GoldenGlove 와 동일 출처).
                if (result.awayScore == 0)
                    CreditCleanSheets(acc, result.homeStarting11);
                if (result.homeScore == 0)
                    CreditCleanSheets(acc, result.awayStarting11);
            }
            return acc;
        }

        // 선발 명단이 있으나 playerStats 가 없는 경우는 무시(선발은 항상 playerStats 보유).
        private static void CreditCleanSheets(
            Dictionary<int, PlayerLeagueStats> acc,
            List<int> starting11
        )
        {
            if (starting11 == null)
                return;
            foreach (var pid in starting11)
                if (acc.TryGetValue(pid, out var entry))
                    entry.cleanSheets++;
        }

        // 카테고리별 순위. Rating 만 최소 출전수 필터. topN<=0 이면 balance 기본값.
        public static List<LeaderboardEntry> GetLeaderboard(
            GameState state,
            League league,
            GameBalanceSO balance,
            LeaderboardCategory category,
            int topN = 0
        )
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var result = new List<LeaderboardEntry>();
            if (league == null)
                return result;

            int limit = topN > 0 ? topN : balance.leaderboardDefaultTopN;
            var acc = BuildLeagueStats(league);

            // acc 에 등장한 선수 → 도메인 조회 (포지션/소속 클럽 필요).
            var rows = new List<(Player player, PlayerLeagueStats stats)>();
            foreach (var kv in acc)
            {
                var player = state.GetPlayer(kv.Key);
                if (player?.info == null)
                    continue;
                rows.Add((player, kv.Value));
            }

            // 카테고리별 필터 + value 산정.
            IEnumerable<(Player player, PlayerLeagueStats stats, float value)> projected;
            switch (category)
            {
                case LeaderboardCategory.Goals:
                    projected = rows.Where(r => r.stats.goals > 0)
                        .Select(r => (r.player, r.stats, (float)r.stats.goals));
                    break;
                case LeaderboardCategory.Assists:
                    projected = rows.Where(r => r.stats.assists > 0)
                        .Select(r => (r.player, r.stats, (float)r.stats.assists));
                    break;
                case LeaderboardCategory.Rating:
                {
                    // 최소 출전 필터를 "리그 최다 출전수" 로 상한 적응 — 시즌 초반(아무도 기준 미달)
                    // 에도 가장 많이 뛴 선수들의 평점이 보이도록. 충분히 진행되면 설정값으로 고정.
                    int maxApps = rows.Count > 0 ? rows.Max(r => r.stats.apps) : 0;
                    int minReq = Math.Min(balance.leaderboardRatingMinApps, maxApps);
                    projected = rows.Where(r => r.stats.apps >= minReq)
                        .Select(r => (r.player, r.stats, r.stats.AvgRating));
                    break;
                }
                case LeaderboardCategory.CleanSheets:
                    projected = rows.Where(r =>
                            r.player.info.primaryPosition == Position.GK && r.stats.cleanSheets > 0
                        )
                        .Select(r => (r.player, r.stats, (float)r.stats.cleanSheets));
                    break;
                case LeaderboardCategory.Appearances:
                    projected = rows.Where(r => r.stats.apps > 0)
                        .Select(r => (r.player, r.stats, (float)r.stats.apps));
                    break;
                default:
                    return result;
            }

            // value 내림차순 정렬 (동률은 출전 적은 순 → 골/도움 보조).
            var sorted = projected
                .OrderByDescending(x => x.value)
                .ThenBy(x => x.stats.apps)
                .ThenByDescending(x => x.stats.goals)
                .ThenByDescending(x => x.stats.assists)
                .Take(limit)
                .ToList();

            // competition ranking (동률 = 같은 순위, 다음은 건너뜀).
            float prevValue = float.NaN;
            for (int i = 0; i < sorted.Count; i++)
            {
                var x = sorted[i];
                int rank =
                    (i > 0 && Math.Abs(x.value - prevValue) < RatingEpsilon)
                        ? result[i - 1].rank
                        : i + 1;
                prevValue = x.value;
                result.Add(
                    new LeaderboardEntry
                    {
                        rank = rank,
                        playerId = x.player.id,
                        clubId = x.player.currentClubId,
                        value = x.value,
                        apps = x.stats.apps,
                    }
                );
            }
            return result;
        }
    }
}
