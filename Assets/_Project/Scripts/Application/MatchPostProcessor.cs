// MatchPostProcessor.cs
// MatchSimulator 산출 결과를 GameState 에 적용. data-flows.md #3 [4] 시퀀스.
// V0.1: match.result + 피로 갱신 + 리그 순위 갱신 + MatchFinishedEvent 발행.
// V1.0 G.1: 사기 갱신 (MoraleSystem.OnMatchFinished — 결과 / 평점 / Hidden professionalism 보정).
// V1.0 G.2: seasonAppearances 증가 (PlaytimeAgreement Promise 평가용).
// V1.0+: 폼 갱신 (#30) / 부상자 / 카드 / 텍스트 이벤트.

using System;
using System.Linq;
using FMLite.Core;
using FMLite.Domain;
using UnityEngine;

namespace FMLite.Application
{
    public static class MatchPostProcessor
    {
        public static void Process(
            Match match,
            MatchResult result,
            GameState state,
            GameBalanceSO balance
        )
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            // 부정합 방지 — 이미 처리된 매치 재처리 금지 (idempotent 아님)
            if (match.result != null)
                throw new InvalidOperationException(
                    $"Match {match.id} 이미 처리됨 (result != null). 재처리는 호출자가 의도 명시 후 result=null 로 리셋 필요."
                );

            // a. match.result = result
            match.result = result;

            // b. 피로 갱신 + seasonAppearances (starting11 22명)
            ApplyFatigueAndAppearance(result.homeStarting11, state, balance.fatigueGainPerMatch);
            ApplyFatigueAndAppearance(result.awayStarting11, state, balance.fatigueGainPerMatch);

            // c. 사기 갱신 (V1.0 G.1 — algorithms.md V1.0-6 OnMatchFinished).
            //    승/무/패 ±8 + 평점 ≥ 7.5 +5 / 평점 < 6 -3 + Hidden professionalism 보정.
            //    폼 갱신은 #30 V1.0+ 이연 (form 시스템 별도 도입 시).
            MoraleSystem.OnMatchFinished(state, result, balance);

            // d. 순위 갱신 — League 만 (V0.1 호출 경로상 League 뿐, 컵은 V1.0+)
            if (match.type == CompetitionType.League)
            {
                UpdateStandings(match, result, state);
            }
            else
            {
                Debug.LogWarning(
                    $"[MatchPostProcessor] match.type={match.type} — V0.1 순위 갱신 스킵 (League 만 갱신)."
                );
            }

            // e. 부상자 / 카드 — V1.0+ (미구현)

            // f. MatchFinishedEvent 발행
            EventBus.Publish(new MatchFinishedEvent { matchId = match.id, result = result });
        }

        // ── 피로 + 출전 횟수 ──────────────────────────────────────────

        private static void ApplyFatigueAndAppearance(
            System.Collections.Generic.List<int> playerIds,
            GameState state,
            int gain
        )
        {
            for (int i = 0; i < playerIds.Count; i++)
            {
                var p = state.GetPlayer(playerIds[i]);
                if (p?.state == null)
                    continue;
                int newFatigue = p.state.fatigue + gain;
                if (newFatigue > 100)
                    newFatigue = 100;
                if (newFatigue < 0)
                    newFatigue = 0;
                p.state.fatigue = newFatigue;
                p.state.seasonAppearances += 1; // V1.0 G.2 — PlaytimeAgreement 평가
            }
        }

        // ── 순위 ──────────────────────────────────────────────────────

        private static void UpdateStandings(Match match, MatchResult result, GameState state)
        {
            var homeClub = state.GetClub(match.homeClubId);
            if (homeClub == null)
            {
                Debug.LogWarning(
                    $"[MatchPostProcessor] homeClub id={match.homeClubId} not found — 순위 갱신 스킵"
                );
                return;
            }
            var league = state.leagues.FirstOrDefault(l => l.id == homeClub.leagueId);
            if (league == null || league.standings == null)
            {
                Debug.LogWarning(
                    $"[MatchPostProcessor] league id={homeClub.leagueId} / standings 없음 — 순위 갱신 스킵"
                );
                return;
            }

            var homeEntry = league.standings.entries.FirstOrDefault(e =>
                e.clubId == match.homeClubId
            );
            var awayEntry = league.standings.entries.FirstOrDefault(e =>
                e.clubId == match.awayClubId
            );
            if (homeEntry == null || awayEntry == null)
            {
                Debug.LogWarning(
                    $"[MatchPostProcessor] StandingEntry 누락 (home={homeEntry == null} / away={awayEntry == null}) — 순위 갱신 스킵"
                );
                return;
            }

            // 공통 — 출전 + 골 통계
            homeEntry.played += 1;
            awayEntry.played += 1;
            homeEntry.goalsFor += result.homeScore;
            homeEntry.goalsAgainst += result.awayScore;
            awayEntry.goalsFor += result.awayScore;
            awayEntry.goalsAgainst += result.homeScore;

            // 결과별 (EPL 룰: 승 3 / 무 1 / 패 0)
            if (result.homeScore > result.awayScore)
            {
                homeEntry.won += 1;
                homeEntry.points += 3;
                awayEntry.lost += 1;
            }
            else if (result.homeScore < result.awayScore)
            {
                awayEntry.won += 1;
                awayEntry.points += 3;
                homeEntry.lost += 1;
            }
            else
            {
                homeEntry.drawn += 1;
                homeEntry.points += 1;
                awayEntry.drawn += 1;
                awayEntry.points += 1;
            }
        }
    }
}
