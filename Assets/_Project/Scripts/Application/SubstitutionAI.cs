// SubstitutionAI.cs
// 자동 교체 판단 + 실행. algorithms.md V1.0-2 (Stage I.6).
// 트리거 3종: Injury(즉시) / Fatigue > threshold / Tactical(60분+ 스코어 기반).
// MatchSimulator 내부에서 호출 — SimState 외부화 대신 필요 컨텍스트를 파라미터로 전달.

using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class SubstitutionAI
    {
        // 교체 컨텍스트 — MatchSimulator 가 SimState 에서 조립해서 전달.
        public sealed class SubContext
        {
            public List<int> xi; // 현재 출전 XI (in-place 수정됨)
            public int subsRemaining;
            public Club club; // bench 원천 (seniorSquadIds)
            public GameState state;
            public GameBalanceSO balance;
            public HashSet<int> sentOff;
            public Dictionary<int, PlayerMatchStat> stats;
            public int currentMinute;
            public int homeScore;
            public int awayScore;
            public bool isHome; // 이 컨텍스트가 home 팀인지
            public List<MatchEvent> events; // collectEvents=true 시 채움 (null 도 허용)
        }

        // 부상 교체 — 부상 선수 즉시 교체.
        // Returns: 교체 발생 여부.
        public static bool TrySubstituteForInjury(SubContext ctx, int injuredPlayerId)
        {
            if (ctx.subsRemaining <= 0)
                return false;
            if (!ctx.xi.Contains(injuredPlayerId))
                return false;

            var injured = ctx.state.GetPlayer(injuredPlayerId);
            Line? preferredLine =
                injured != null ? StartingSquadGacha.LineOf(injured.info.primaryPosition) : null;

            int replacementId = FindReplacement(ctx, injuredPlayerId, preferredLine);
            if (replacementId == -1)
                return false;

            ApplySubstitution(ctx, injuredPlayerId, replacementId);
            return true;
        }

        // 전술 교체 — fatigue > threshold 이거나 스코어 기반.
        // 매치 중 체크 포인트(60/75분)에서 호출. 교체가 발생하면 true.
        public static bool TryTacticalSubstitution(SubContext ctx)
        {
            if (ctx.subsRemaining <= 0)
                return false;
            if (ctx.currentMinute < ctx.balance.substitutionTacticalMinute)
                return false;

            // 1순위: fatigue 높은 선수 교체
            int tiredId = FindTiredPlayer(ctx);
            if (tiredId != -1)
            {
                var tired = ctx.state.GetPlayer(tiredId);
                Line? line =
                    tired != null ? StartingSquadGacha.LineOf(tired.info.primaryPosition) : null;
                int replacementId = FindReplacement(ctx, tiredId, line);
                if (replacementId != -1)
                {
                    ApplySubstitution(ctx, tiredId, replacementId);
                    return true;
                }
            }

            // 2순위: 스코어 기반 — 지고 있을 때 공격 교체 (AT 선수 투입)
            bool isLosingByOne = ctx.isHome
                ? ctx.homeScore < ctx.awayScore
                : ctx.awayScore < ctx.homeScore;
            if (isLosingByOne)
            {
                // DF → AT 교체 (수비 1명 줄이고 공격 강화)
                int defId = FindWorstByLine(ctx, Line.DF);
                if (defId != -1)
                {
                    int attReplId = FindReplacement(ctx, defId, Line.AT);
                    if (attReplId != -1)
                    {
                        ApplySubstitution(ctx, defId, attReplId);
                        return true;
                    }
                }
            }

            return false;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        // fatigue > threshold 인 선수 중 가장 높은 선수 ID 반환. 없으면 -1.
        private static int FindTiredPlayer(SubContext ctx)
        {
            int best = -1;
            int bestFatigue = ctx.balance.substitutionFatigueThreshold;
            foreach (var id in ctx.xi)
            {
                if (ctx.sentOff.Contains(id))
                    continue;
                var p = ctx.state.GetPlayer(id);
                if (p == null)
                    continue;
                int fatigue = p.state.fatigue;
                if (fatigue > bestFatigue)
                {
                    bestFatigue = fatigue;
                    best = id;
                }
            }
            return best;
        }

        // Line 내 최저 CA 선수 (교체 대상 선정용). 없으면 -1.
        private static int FindWorstByLine(SubContext ctx, Line line)
        {
            int worst = -1;
            int worstCa = int.MaxValue;
            foreach (var id in ctx.xi)
            {
                if (ctx.sentOff.Contains(id))
                    continue;
                var p = ctx.state.GetPlayer(id);
                if (p == null || StartingSquadGacha.LineOf(p.info.primaryPosition) != line)
                    continue;
                if (p.currentAbility < worstCa)
                {
                    worstCa = p.currentAbility;
                    worst = id;
                }
            }
            return worst;
        }

        // 교체 투입 선수 선정 — 부상 X, 출전 X, sentOff X, preferredLine 우선.
        private static int FindReplacement(SubContext ctx, int playerOutId, Line? preferredLine)
        {
            var bench = ctx
                .club.seniorSquadIds.Where(id =>
                    id != playerOutId
                    && !ctx.xi.Contains(id)
                    && !ctx.sentOff.Contains(id)
                    && !ctx.stats.ContainsKey(id) // 이미 출전했던 선수 제외
                )
                .Select(id => ctx.state.GetPlayer(id))
                .Where(p => p != null && p.state.injury.injuryTypeId == -1)
                .ToList();

            if (bench.Count == 0)
                return -1;

            // 같은 라인 우선
            if (preferredLine.HasValue)
            {
                var sameLine = bench
                    .Where(p => StartingSquadGacha.LineOf(p.info.primaryPosition) == preferredLine)
                    .OrderByDescending(p => p.currentAbility)
                    .FirstOrDefault();
                if (sameLine != null)
                    return sameLine.id;
            }

            // 라인 매치 없으면 전체 최고 CA
            return bench.OrderByDescending(p => p.currentAbility).First().id;
        }

        // 교체 실행 — XI 갱신 + minutesPlayed 가변 + stats 추가 + 이벤트 발행.
        private static void ApplySubstitution(SubContext ctx, int playerOutId, int playerInId)
        {
            int idx = ctx.xi.IndexOf(playerOutId);
            if (idx == -1)
                return;

            ctx.xi[idx] = playerInId;
            ctx.subsRemaining--;

            // minutesPlayed 확정
            if (ctx.stats.TryGetValue(playerOutId, out var outStat))
                outStat.minutesPlayed = ctx.currentMinute;

            // 투입 선수 stats 초기화
            ctx.stats[playerInId] = new PlayerMatchStat
            {
                playerId = playerInId,
                minutesPlayed = 90 - ctx.currentMinute,
            };

            // Substitution 이벤트 (collectEvents=true 시)
            if (ctx.events != null)
            {
                string pOutName = PlayerName(ctx.state, playerOutId);
                string pInName = PlayerName(ctx.state, playerInId);
                ctx.events.Add(
                    new MatchEvent
                    {
                        minute = ctx.currentMinute,
                        type = MatchEventType.Substitution,
                        side = ctx.isHome ? 0 : 1,
                        actorPlayerId = playerInId,
                        targetPlayerId = playerOutId,
                        textKey = "match_substitution_fmt",
                        textArgs = new System.Collections.Generic.Dictionary<string, string>
                        {
                            ["minute"] = ctx.currentMinute.ToString(),
                            ["playerIn"] = pInName,
                            ["playerOut"] = pOutName,
                        },
                    }
                );
            }
        }

        private static string PlayerName(GameState state, int id)
        {
            var p = state.GetPlayer(id);
            if (p == null)
                return id.ToString();
            var info = p.info;
            if (!string.IsNullOrEmpty(info.lastName))
                return string.IsNullOrEmpty(info.firstName)
                    ? info.lastName
                    : $"{info.firstName[0]}. {info.lastName}";
            return id.ToString();
        }
    }
}
