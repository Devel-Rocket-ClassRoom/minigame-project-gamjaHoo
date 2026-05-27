// MatchReport.cs
// 매치 결과 요약 (신문기사 스타일). OFM news/match_report 차용.
// OFM 패턴: headline {outcome}.{variant} 3종 / scorersData [{player,minute,side}] / i18n_params.
// MatchResult + Match.events 로부터 빌드. UI 레이어(MatchTextScene — I.5 UI) 에서 사용.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;

namespace FMLite.Application
{
    // OFM scorersData 상당 — 골 득점 정보 (UI 표시용).
    public class ScorerEntry
    {
        public string playerName;
        public int minute;
        public bool isHome;
        public bool isPenalty;
    }

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

        // OFM headline variant (0~2) — 같은 결과도 다양한 헤드라인 표현.
        // 텍스트키: match_report_{win|loss|draw}_headline_{0|1|2}
        public int headlineVariant;

        // OFM scorersData — 시간순 골 득점자 목록.
        public List<ScorerEntry> scorers = new List<ScorerEntry>();

        // 헤드라인 이벤트 — Goal + Card + Injury (시간순).
        public List<MatchEvent> highlights = new List<MatchEvent>();

        // "homeTeam N-M awayTeam" 결과 텍스트키.
        public string resultTextKey;

        public static MatchReport Build(Match match, GameState state, Random rng = null)
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

            string outcome;
            if (result.homeScore > result.awayScore)
                outcome = "win";
            else if (result.homeScore < result.awayScore)
                outcome = "loss";
            else
                outcome = "draw";

            int variant = rng != null ? rng.Next(3) : 0;

            // 골 득점자 목록 (OFM scorersData)
            var scorers = match
                .events.Where(e =>
                    e.type == MatchEventType.Goal || e.type == MatchEventType.PenaltyGoal
                )
                .OrderBy(e => e.minute)
                .Select(e =>
                {
                    string name =
                        e.textArgs != null && e.textArgs.TryGetValue("playerName", out var n)
                            ? n
                            : e.actorPlayerId.ToString();
                    return new ScorerEntry
                    {
                        playerName = name,
                        minute = e.minute,
                        isHome = e.side == 0,
                        isPenalty = e.type == MatchEventType.PenaltyGoal,
                    };
                })
                .ToList();

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
                headlineVariant = variant,
                scorers = scorers,
                highlights = highlights,
                resultTextKey = $"match_report_{outcome}_headline_{variant}",
            };
        }
    }
}
