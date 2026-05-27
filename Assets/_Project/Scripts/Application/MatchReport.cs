// MatchReport.cs
// 매치 결과 요약 (신문기사 스타일). OFM news/match_report 차용.
// MatchResult + Match.events 로부터 빌드. UI 레이어(MatchTextScene — I.5 UI) 에서 사용.

using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    public class MatchReport
    {
        public string homeTeamName;
        public string awayTeamName;
        public int homeScore;
        public int awayScore;
        public float homePossessionPct;
        public float awayPossessionPct;
        public int homeShots;
        public int awayShots;
        public int homeShotsOnTarget;
        public int awayShotsOnTarget;

        // 헤드라인 이벤트 — Goal + Card + Injury (시간순).
        public List<MatchEvent> highlights = new List<MatchEvent>();

        // "homeTeam N-M awayTeam" 결과 텍스트키 (match_report_win/loss/draw_fmt).
        public string resultTextKey;

        public static MatchReport Build(Match match, GameState state)
        {
            var result = match.result;
            if (result == null)
                return null;

            var homeClub = state.GetClub(match.homeClubId);
            var awayClub = state.GetClub(match.awayClubId);

            var homeSet = new HashSet<int>(result.homeStarting11);
            int homeShots = result
                .playerStats.Where(ps => homeSet.Contains(ps.playerId))
                .Sum(ps => ps.shots);
            int awayShots = result
                .playerStats.Where(ps => !homeSet.Contains(ps.playerId))
                .Sum(ps => ps.shots);
            int homeShotsOnTarget = result
                .playerStats.Where(ps => homeSet.Contains(ps.playerId))
                .Sum(ps => ps.shotsOnTarget);
            int awayShotsOnTarget = result
                .playerStats.Where(ps => !homeSet.Contains(ps.playerId))
                .Sum(ps => ps.shotsOnTarget);

            string resultKey;
            if (result.homeScore > result.awayScore)
                resultKey = "match_report_win_fmt";
            else if (result.homeScore < result.awayScore)
                resultKey = "match_report_loss_fmt";
            else
                resultKey = "match_report_draw_fmt";

            var highlights = match
                .events.Where(e =>
                    e.type == MatchEventType.Goal
                    || e.type == MatchEventType.PenaltyGoal
                    || e.type == MatchEventType.YellowCard
                    || e.type == MatchEventType.RedCard
                    || e.type == MatchEventType.SecondYellow
                    || e.type == MatchEventType.Injury
                )
                .OrderBy(e => e.minute)
                .ToList();

            return new MatchReport
            {
                homeTeamName = homeClub?.name ?? match.homeClubId.ToString(),
                awayTeamName = awayClub?.name ?? match.awayClubId.ToString(),
                homeScore = result.homeScore,
                awayScore = result.awayScore,
                homePossessionPct = result.homePossessionPct,
                awayPossessionPct = result.awayPossessionPct,
                homeShots = homeShots,
                awayShots = awayShots,
                homeShotsOnTarget = homeShotsOnTarget,
                awayShotsOnTarget = awayShotsOnTarget,
                highlights = highlights,
                resultTextKey = resultKey,
            };
        }
    }
}
