// MatchSimulator.cs
// algorithms.md V1.0-2 Match Simulation V1.0 (분 단위 이벤트 시퀀스) — Stage I.1 골격.
// 인터페이스 (Simulate(match, state, balance) → MatchResult) 유지 / 내부 재작성 (design-decisions.md #34 / #44).
// 이벤트 종류 = I.2 / 부상·카드 = I.3 / 평점 = I.4 / 텍스트 = I.5 / SubstitutionAI = I.6 / SimulateLite = I.7 / 외부 영향 = I.8 / strengthExponent 폐기 = I.9.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;
using UnityEngine;
using Random = System.Random;

namespace FMLite.Application
{
    public static class MatchSimulator
    {
        public static MatchResult Simulate(Match match, GameState state, GameBalanceSO balance)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var home =
                state.GetClub(match.homeClubId)
                ?? throw new ArgumentException($"homeClub id={match.homeClubId} not found");
            var away =
                state.GetClub(match.awayClubId)
                ?? throw new ArgumentException($"awayClub id={match.awayClubId} not found");

            if (match.type != CompetitionType.League)
                Debug.LogWarning(
                    $"[MatchSimulator] V1.0 호출 경로 없음 — match.type={match.type}. League 와 동일 처리."
                );

            // 1단계: 시드 고정 (algorithms.md V1.0-2 1단계 / design-decisions.md #17 유지)
            var rng = new Random(match.id ^ state.randomSeed);

            // 2단계: starting11 결정 (Tactic 도입 = Stage J. I.1 골격 = top-by-CA 자동 라인업 + 부상/정지 제외)
            var homeStarting11 = SelectStartingEleven(home, state);
            var awayStarting11 = SelectStartingEleven(away, state);

            // 3단계: 경기 상태 초기화 (minute=0, score=0:0)
            int homeScore = 0;
            int awayScore = 0;

            // 4단계: 분 단위 step (1~90) — I.1 골격
            // I.2 가 매 분 이벤트 추첨 (Shot / Foul / Injury / KeyPass / Cross / Pass / Tackle / Interception)
            // + 주체 선수 추첨 (Role 가중치 + Stat) + 결과 분기 (Shot → Goal/Save/Miss/Block) 채움.
            // I.3 부상·카드, I.4 평점, I.5 텍스트, I.6 SubstitutionAI 후속.
            for (int minute = 1; minute <= 90; minute++)
            {
                // 후속 Task 진입점. I.1 = 빈 루프 (rng 소비 X — 결정성 영향 X).
            }

            // 5단계: 최종 누적 = MatchResult
            // I.1 골격: homeScore/awayScore = 0, playerStats 22명 minutesPlayed=90, 나머지 필드 0.
            var playerStats = BuildPlayerStats(homeStarting11);
            playerStats.AddRange(BuildPlayerStats(awayStarting11));

            return new MatchResult
            {
                homeScore = homeScore,
                awayScore = awayScore,
                homeStarting11 = homeStarting11,
                awayStarting11 = awayStarting11,
                playerStats = playerStats,
            };
        }

        // ── starting11 자동 선정 ──────────────────────────────────────
        // Tactic 도입 (Stage J) 시 Tactic.slots.assignedPlayerId 사용으로 교체.
        // 부상자 / suspendedMatches > 0 제외 (algorithms.md V1.0-2 2단계).
        private static List<int> SelectStartingEleven(Club club, GameState state)
        {
            return club
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .Where(p =>
                    p != null && p.state.injury.injuryTypeId == -1 && p.state.suspendedMatches <= 0
                )
                .OrderByDescending(p => p.currentAbility)
                .Take(11)
                .Select(p => p.id)
                .ToList();
        }

        // ── PlayerMatchStat 빌드 ──────────────────────────────────────
        // I.1 골격: 모든 누적 필드 0. I.2 (goals/assists/shots/passes) / I.3 (yellow/red) / I.4 (rating) 가 점진적 채움.
        private static List<PlayerMatchStat> BuildPlayerStats(List<int> starting11)
        {
            var stats = new List<PlayerMatchStat>(starting11.Count);
            for (int i = 0; i < starting11.Count; i++)
            {
                stats.Add(
                    new PlayerMatchStat
                    {
                        playerId = starting11[i],
                        minutesPlayed = 90, // I.6 SubstitutionAI 도입 시 교체/퇴장 반영 (가변)
                        goals = 0,
                        assists = 0,
                        rating = 0f,
                        yellowCards = 0,
                        redCards = 0,
                    }
                );
            }
            return stats;
        }
    }
}
