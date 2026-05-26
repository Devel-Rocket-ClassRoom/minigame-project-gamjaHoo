// MatchSimulator.cs
// algorithms.md #2 Match Simulation V0.1 구현.
// 결과 우선 모델 (시드 고정 → starting11 → 전력 → λ Poisson → 득점자 → playerStats).
// design-decisions.md #17 (시드 고정 → 결과 미리 산출) / #24 (CA 합 기반) / #33 (V0.1 정책).
// V1.0+ 이벤트 시퀀스 전환 (#34) 시 인터페이스 유지하고 내부만 교체.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Domain;
using FMLite.Utils;
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
                    $"[MatchSimulator] V0.1 호출 경로 없음 — match.type={match.type}. League 와 동일 처리."
                );

            if (balance.scoringWeightByLine == null || balance.scoringWeightByLine.Length != 4)
                throw new ArgumentException(
                    "scoringWeightByLine 은 4-length 배열이어야 함 (GK/DF/MF/AT)"
                );

            // 1단계: 시드 고정 (design-decisions.md #17)
            var rng = new Random(match.id ^ state.randomSeed);

            // 2단계: starting11 자동 선정 (V0.1 — UI 라인업 결정 시스템 부재 임시 단순화)
            var homeStarting11 = SelectStartingEleven(home, state);
            var awayStarting11 = SelectStartingEleven(away, state);

            // 3단계: 전력 계산 (단순 CA 합, design-decisions.md #24)
            // V1.0 G.3: 라커룸 분위기 < 임계 시 strength 곱셈 (≈ 폼 -5 효과).
            int homeStrength = (int)(SumCA(homeStarting11, state) * MoodFactor(home, balance));
            int awayStrength = (int)(SumCA(awayStarting11, state) * MoodFactor(away, balance));

            // 4단계: λ 계산 + Poisson 골수 결정
            // strengthExponent (k) 로 비선형화 — CA 차이를 골수 차이로 증폭.
            // k=1 이면 선형 (양 팀 합으로 골 나눠가짐). k=1.5 (기본) 면 강팀이 더 큰 비율 차지.
            // V1.0+ 매치 엔진 재작성 시 폐기 — finishing 등 개별 stats 가 결정력 표현 (#33 V1.0 트리거).
            int totalStrength = homeStrength + awayStrength;
            double strengthRatio;
            if (totalStrength == 0)
            {
                strengthRatio = 0.5;
            }
            else
            {
                double k = balance.strengthExponent;
                double sh = Math.Pow(homeStrength, k);
                double sa = Math.Pow(awayStrength, k);
                strengthRatio = sh / (sh + sa);
            }

            double homeLambda =
                balance.avgGoalsPerMatch * strengthRatio + balance.homeAdvantageGoalBonus;
            double awayLambda = balance.avgGoalsPerMatch * (1.0 - strengthRatio);

            // 음수 λ 방어 (avgGoalsPerMatch 가 비정상으로 0 이거나 ratio 가 극단인 경우)
            if (homeLambda < 0)
                homeLambda = 0;
            if (awayLambda < 0)
                awayLambda = 0;

            int homeScore = rng.NextPoisson(homeLambda);
            int awayScore = rng.NextPoisson(awayLambda);

            // 5단계: 득점자 선정 (라인 가중치 × CA/100)
            var homeGoalsByPlayer = PickScorers(homeStarting11, state, homeScore, balance, rng);
            var awayGoalsByPlayer = PickScorers(awayStarting11, state, awayScore, balance, rng);

            // 6단계: PlayerMatchStat 빌드 (V0.1 — goals + minutesPlayed=90 만)
            var playerStats = BuildPlayerStats(homeStarting11, homeGoalsByPlayer);
            playerStats.AddRange(BuildPlayerStats(awayStarting11, awayGoalsByPlayer));

            return new MatchResult
            {
                homeScore = homeScore,
                awayScore = awayScore,
                homeStarting11 = homeStarting11,
                awayStarting11 = awayStarting11,
                playerStats = playerStats,
            };
        }

        // ── 2단계: starting11 자동 선정 ────────────────────────────────

        // V0.1: top-11 by CA, 부상자 제외, 포지션 무시.
        // V1.0+ 라인업 결정 시스템 도입 시 호출자가 starting11 을 인자로 전달하고
        // 이 메서드는 폐기 또는 폴백으로 격하 (algorithms.md #2 V1.0 Migration Notes).
        private static List<int> SelectStartingEleven(Club club, GameState state)
        {
            return club
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .Where(p => p != null && p.state.injury.injuryTypeId == -1)
                .OrderByDescending(p => p.currentAbility)
                .Take(11)
                .Select(p => p.id)
                .ToList();
        }

        private static int SumCA(List<int> playerIds, GameState state)
        {
            int sum = 0;
            for (int i = 0; i < playerIds.Count; i++)
            {
                var p = state.GetPlayer(playerIds[i]);
                if (p != null)
                    sum += p.currentAbility;
            }
            return sum;
        }

        // ── 5단계: 득점자 선정 ────────────────────────────────────────

        private static Dictionary<int, int> PickScorers(
            List<int> starting11,
            GameState state,
            int totalGoals,
            GameBalanceSO balance,
            Random rng
        )
        {
            var result = new Dictionary<int, int>();
            if (totalGoals == 0 || starting11.Count == 0)
                return result;

            // WeightedSample 는 IList<T> 받음. starting11 자체가 List<int> 이므로 그대로 사용.
            // weightFn 은 매 호출마다 같은 ID 에 같은 weight 반환 — Player lookup 캐시는 V1.0+.
            for (int g = 0; g < totalGoals; g++)
            {
                int scorerId = rng.WeightedSample(
                    starting11,
                    id =>
                    {
                        var p = state.GetPlayer(id);
                        if (p == null)
                            return 0.0;
                        int lineIdx = (int)StartingSquadGacha.LineOf(p.info.primaryPosition);
                        double lineWeight = balance.scoringWeightByLine[lineIdx];
                        return lineWeight * (p.currentAbility / 100.0);
                    }
                );
                result[scorerId] = result.TryGetValue(scorerId, out var c) ? c + 1 : 1;
            }
            return result;
        }

        // ── 라커룸 분위기 영향 (V1.0 G.3) ─────────────────────────────

        // mood < lowThreshold 시 strength 곱셈 (≈ 폼 -5 효과). 그 외 1.0.
        private static float MoodFactor(Club club, GameBalanceSO balance)
        {
            if (club?.season == null)
                return 1.0f;
            return club.season.dressingRoomMood < balance.dressingRoomMoodLowThreshold
                ? balance.dressingRoomLowMoodStrengthFactor
                : 1.0f;
        }

        // ── 6단계: PlayerMatchStat 빌드 ────────────────────────────────

        private static List<PlayerMatchStat> BuildPlayerStats(
            List<int> starting11,
            Dictionary<int, int> goalsByPlayer
        )
        {
            var stats = new List<PlayerMatchStat>(starting11.Count);
            for (int i = 0; i < starting11.Count; i++)
            {
                int id = starting11[i];
                stats.Add(
                    new PlayerMatchStat
                    {
                        playerId = id,
                        minutesPlayed = 90, // V0.1: 교체 X
                        goals = goalsByPlayer.TryGetValue(id, out var g) ? g : 0,
                        assists = 0, // V1.0+
                        rating = 0f, // V1.0+
                        yellowCards = 0, // V1.0+
                        redCards = 0, // V1.0+
                    }
                );
            }
            return stats;
        }
    }
}
