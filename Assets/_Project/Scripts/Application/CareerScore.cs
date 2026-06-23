// CareerScore.cs
// 명예의 전당 — 유저 감독의 커리어 점수 계산 (Stateless, design-decisions.md #3).
// GameState 만 입력받아 계산하며 부수효과 없음.
//
// 누적 모델: league.history 전체를 매번 재계산 → 멱등(중복 누적 없음).
//   score = managerReputation + Σ over 유저 구단이 참여한 완료 시즌:
//             (그 시즌 승점) + (우승 시 TitleBonus)

using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class CareerScore
    {
        public const int TitleBonus = 200;

        public static int ComputeTotal(GameState state)
        {
            var club = state?.GetClub(state.userClubId);
            if (club == null)
                return 0;

            int score = state.managerReputation;
            foreach (var league in state.leagues)
            {
                if (league?.history == null)
                    continue;
                foreach (var h in league.history)
                {
                    var entry = FindEntry(h.standings, club.id);
                    if (entry == null)
                        continue;
                    int position = PositionOf(h.standings, club.id);
                    score += entry.points + (position == 1 ? TitleBonus : 0);
                }
            }
            return score;
        }

        // 가장 최근 완료 시즌의 유저 구단 성적. 없으면 null.
        public static SeasonOutcome GetLatestSeason(GameState state)
        {
            var club = state?.GetClub(state.userClubId);
            if (club == null)
                return null;

            League userLeague = null;
            SeasonHistory latest = null;
            foreach (var league in state.leagues)
            {
                if (league?.history == null)
                    continue;
                for (int i = league.history.Count - 1; i >= 0; i--)
                {
                    if (FindEntry(league.history[i].standings, club.id) != null)
                    {
                        // 가장 높은 seasonYear 를 최신으로 선택
                        if (latest == null || league.history[i].seasonYear > latest.seasonYear)
                        {
                            latest = league.history[i];
                            userLeague = league;
                        }
                        break;
                    }
                }
            }

            if (latest == null)
                return null;

            var e = FindEntry(latest.standings, club.id);
            int pos = PositionOf(latest.standings, club.id);
            return new SeasonOutcome
            {
                year = latest.seasonYear,
                clubName = club.name,
                position = pos,
                points = e?.points ?? 0,
            };
        }

        private static StandingEntry FindEntry(Standings standings, int clubId)
        {
            return standings?.entries?.FirstOrDefault(e => e.clubId == clubId);
        }

        // 승점 내림차순, 동점 시 골득실 내림차순 기준 1-based 순위.
        private static int PositionOf(Standings standings, int clubId)
        {
            if (standings?.entries == null)
                return 0;
            var sorted = new List<StandingEntry>(standings.entries);
            sorted.Sort(
                (a, b) =>
                {
                    int cmp = b.points.CompareTo(a.points);
                    if (cmp != 0)
                        return cmp;
                    return (b.goalsFor - b.goalsAgainst).CompareTo(a.goalsFor - a.goalsAgainst);
                }
            );
            for (int i = 0; i < sorted.Count; i++)
                if (sorted[i].clubId == clubId)
                    return i + 1;
            return 0;
        }
    }

    public class SeasonOutcome
    {
        public int year;
        public string clubName;
        public int position;
        public int points;
    }
}
