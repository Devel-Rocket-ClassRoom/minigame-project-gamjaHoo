// MatchSimulator.cs
// algorithms.md V1.0-2 Match Simulation V1.0 (분 단위 이벤트 시퀀스) — Stage I.1 골격 + I.2 이벤트 종류.
// 인터페이스 (Simulate(match, state, balance) → MatchResult) 유지 (design-decisions.md #34 / #44).
// I.2: Shot/KeyPass/Cross/Foul/Injury/Pass/Tackle/Interception 이벤트 + Goal/Save 결과 + assist 추적.
// 후속: suspendedMatches 누적 = I.3 / 평점 = I.4 / 텍스트 = I.5 / SubstitutionAI = I.6 / SimulateLite = I.7 / 외부 영향 = I.8 / strengthExponent 폐기 = I.9.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Core;
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

            // 1단계: 시드 고정 (algorithms.md V1.0-2 1단계 / design-decisions.md #17)
            var rng = new Random(match.id ^ state.randomSeed);

            // 2단계: starting11 결정 (Tactic 도입 = Stage J. 자동 라인업 + 부상/정지 제외)
            var homeStarting11 = SelectStartingEleven(home, state);
            var awayStarting11 = SelectStartingEleven(away, state);

            // 3단계: 경기 상태 초기화
            int homeScore = 0;
            int awayScore = 0;
            var stats = InitStatsMap(homeStarting11, awayStarting11);

            // 양 팀 직전 KeyPass 발행자 (다음 골 발생 시 assist 카운트).
            int homePendingAssist = -1;
            int awayPendingAssist = -1;

            // 4단계: 분 단위 step (1~90) — 양 팀 독립 이벤트 추첨
            for (int minute = 1; minute <= 90; minute++)
            {
                (homeScore, homePendingAssist) = SimulateTeamMinute(
                    home,
                    homeStarting11,
                    awayStarting11,
                    state,
                    balance,
                    rng,
                    stats,
                    homeScore,
                    homePendingAssist
                );
                (awayScore, awayPendingAssist) = SimulateTeamMinute(
                    away,
                    awayStarting11,
                    homeStarting11,
                    state,
                    balance,
                    rng,
                    stats,
                    awayScore,
                    awayPendingAssist
                );
            }

            // 5단계: 최종 누적 = MatchResult (I.4 평점 / I.6 minutesPlayed 가변 후속)
            return new MatchResult
            {
                homeScore = homeScore,
                awayScore = awayScore,
                homeStarting11 = homeStarting11,
                awayStarting11 = awayStarting11,
                playerStats = stats.Values.ToList(),
            };
        }

        // ── starting11 자동 선정 (Tactic 도입 = Stage J) ─────────────

        private static List<int> SelectStartingEleven(Club club, GameState state)
        {
            return club
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .Where(p =>
                    p != null
                    && p.state.injury.injuryTypeId == -1
                    && p.state.suspendedMatches <= 0
                )
                .OrderByDescending(p => p.currentAbility)
                .Take(11)
                .Select(p => p.id)
                .ToList();
        }

        // ── 매 분 한 팀 이벤트 추첨 ──────────────────────────────────

        private static (int newScore, int newPendingAssist) SimulateTeamMinute(
            Club attackerClub,
            List<int> attackers,
            List<int> defenders,
            GameState state,
            GameBalanceSO balance,
            Random rng,
            Dictionary<int, PlayerMatchStat> stats,
            int currentScore,
            int pendingAssist
        )
        {
            // Mentality — Tactic 도입 (J) 전이라 Balanced (인덱스 3) 디폴트
            float mentalityMod = balance.mentalityShotMultiplier[3];

            // ─ Shot ─
            int avgCA = AvgCA(attackers, state);
            double shotChance = (double)avgCA / balance.shotChanceBaseDivisor * mentalityMod;
            if (rng.NextDouble() < shotChance)
            {
                int shooterId = PickShooter(attackers, state, balance, rng);
                if (shooterId != -1)
                {
                    var shooter = state.GetPlayer(shooterId);
                    stats[shooterId].shots++;

                    // On-target 판정
                    double onTargetProb =
                        shooter.stats.technical.finishing
                        * shooter.stats.mental.composure
                        / balance.shotOnTargetDivisor;
                    if (rng.NextDouble() * 100 < onTargetProb)
                    {
                        stats[shooterId].shotsOnTarget++;

                        // GK Save 판정
                        var gk = FindGoalkeeper(defenders, state);
                        double saveProb =
                            gk != null
                                ? (gk.stats.gk.reflexes * gk.stats.gk.handling)
                                    / balance.shotSaveDivisor
                                : 50.0;

                        if (rng.NextDouble() * 100 >= saveProb)
                        {
                            // GOAL!
                            currentScore++;
                            stats[shooterId].goals++;

                            // Assist 처리 (직전 KeyPass 가 있고 슈터 본인 아닌 경우)
                            if (
                                pendingAssist != -1
                                && pendingAssist != shooterId
                                && stats.ContainsKey(pendingAssist)
                            )
                            {
                                stats[pendingAssist].assists++;
                            }
                            pendingAssist = -1;
                        }
                        // else Save — GK 평점 가산 = I.4
                    }
                    // else Miss
                }
            }

            // ─ KeyPass (한 분 최대 1명/팀) ─
            foreach (var pid in attackers)
            {
                var p = state.GetPlayer(pid);
                if (p == null)
                    continue;
                double chance =
                    (double)(p.stats.mental.vision * p.stats.technical.passing)
                    / balance.keyPassChanceDivisor;
                if (rng.NextDouble() < chance)
                {
                    stats[pid].keyPasses++;
                    pendingAssist = pid;
                    break;
                }
            }

            // ─ Cross (LW/RW 만, 한 분 최대 1명/팀) ─
            foreach (var pid in attackers)
            {
                var p = state.GetPlayer(pid);
                if (p == null)
                    continue;
                if (
                    p.info.primaryPosition != Position.LW
                    && p.info.primaryPosition != Position.RW
                )
                    continue;
                double chance =
                    (double)(p.stats.technical.crossing * p.stats.technical.technique)
                    / balance.crossChanceDivisor;
                if (rng.NextDouble() < chance)
                {
                    // Cross = pass 시도 가산. 헤딩 슛 변환은 V1.x.
                    stats[pid].passes++;
                    if (rng.NextDouble() * 100 < p.stats.technical.crossing)
                        stats[pid].passesCompleted++;
                    break;
                }
            }

            // ─ Foul (DF/MF 만, 한 분 최대 1명/팀) ─
            foreach (var pid in attackers)
            {
                var p = state.GetPlayer(pid);
                if (p == null)
                    continue;
                var line = StartingSquadGacha.LineOf(p.info.primaryPosition);
                if (line != Line.DF && line != Line.MF)
                    continue;
                double chance = (double)p.stats.mental.aggression / balance.foulChanceDivisor;
                if (rng.NextDouble() < chance)
                {
                    stats[pid].foulsCommitted++;

                    // Y/R 분기 (suspendedMatches 누적 = I.3)
                    double r = rng.NextDouble();
                    if (r < balance.foulRedRatio)
                        stats[pid].redCards++;
                    else if (r < balance.foulRedRatio + balance.foulYellowRatio)
                        stats[pid].yellowCards++;

                    // 파울 당한 선수 (상대 starting11 중 랜덤)
                    if (defenders.Count > 0)
                    {
                        int victim = defenders[rng.Next(defenders.Count)];
                        if (stats.ContainsKey(victim))
                            stats[victim].foulsSuffered++;
                    }
                    break;
                }
            }

            // ─ Injury (한 분 최대 1명/팀) ─
            float injRateFactor = InjurySystem.ComputeInjuryRate(
                attackerClub.facilities?.medicalLevel ?? 1,
                balance
            );
            foreach (var pid in attackers)
            {
                var p = state.GetPlayer(pid);
                if (p == null)
                    continue;
                if (p.state.injury.injuryTypeId != -1)
                    continue; // 이미 부상 중

                int proneness = p.hiddenAttrs?.injuryProneness ?? 50;
                double rate =
                    balance.injuryBaseRate
                    * injRateFactor
                    * (proneness / balance.injuryProneRefDivisor);
                if (rng.NextDouble() < rate)
                {
                    var injuryType = PickInjuryType(rng);
                    if (injuryType == null)
                        break; // 카탈로그 없으면 스킵 (시드 자산 부재 환경)
                    int baseDays = rng.Next(injuryType.minDays, injuryType.maxDays + 1);
                    int recoveryDays = InjurySystem.ComputeRecoveryDays(
                        baseDays,
                        attackerClub.facilities?.medicalLevel ?? 1,
                        attackerClub.facilities?.gymLevel ?? 1,
                        balance
                    );
                    var injuryInfo = new InjuryInfo
                    {
                        injuryTypeId = injuryType.id,
                        startDate = state.currentDate,
                        expectedReturn = state.currentDate.AddDays(recoveryDays),
                        isCareerThreatening = recoveryDays >= balance.injuryCareerThreateningDays,
                    };
                    p.state.injury = injuryInfo;
                    EventBus.Publish(
                        new PlayerInjuredEvent { playerId = pid, injury = injuryInfo }
                    );
                    break;
                }
            }

            // ─ Pass / Tackle / Interception 누적 (매 분 각 선수 독립) ─
            foreach (var pid in attackers)
            {
                var p = state.GetPlayer(pid);
                if (p == null)
                    continue;

                // Pass — 모든 선수
                double passChance = (double)p.stats.technical.passing / balance.passChanceDivisor;
                if (rng.NextDouble() < passChance)
                {
                    stats[pid].passes++;
                    if (rng.NextDouble() * 100 < p.stats.technical.passing)
                        stats[pid].passesCompleted++;
                }

                // Tackle / Interception — DF/MF 만
                var line = StartingSquadGacha.LineOf(p.info.primaryPosition);
                if (line == Line.DF || line == Line.MF)
                {
                    double tackleChance =
                        (double)p.stats.technical.tackling / balance.tackleChanceDivisor;
                    if (rng.NextDouble() < tackleChance)
                        stats[pid].tackles++;

                    double intChance =
                        (double)p.stats.mental.anticipation / balance.interceptionChanceDivisor;
                    if (rng.NextDouble() < intChance)
                        stats[pid].interceptions++;
                }
            }

            return (currentScore, pendingAssist);
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────

        private static Dictionary<int, PlayerMatchStat> InitStatsMap(
            List<int> homeStarting11,
            List<int> awayStarting11
        )
        {
            var map = new Dictionary<int, PlayerMatchStat>(
                homeStarting11.Count + awayStarting11.Count
            );
            foreach (var id in homeStarting11)
                map[id] = NewStat(id);
            foreach (var id in awayStarting11)
                map[id] = NewStat(id);
            return map;
        }

        private static PlayerMatchStat NewStat(int id) =>
            new PlayerMatchStat
            {
                playerId = id,
                minutesPlayed = 90, // I.6 SubstitutionAI 도입 시 가변
                rating = 0f, // I.4
            };

        private static int AvgCA(List<int> playerIds, GameState state)
        {
            if (playerIds.Count == 0)
                return 0;
            int sum = 0;
            int count = 0;
            for (int i = 0; i < playerIds.Count; i++)
            {
                var p = state.GetPlayer(playerIds[i]);
                if (p != null)
                {
                    sum += p.currentAbility;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0;
        }

        // 슈터 선정 — shotPositionWeights[Line] × (finishing × offTheBall / 100) 비례 WeightedSample.
        private static int PickShooter(
            List<int> attackers,
            GameState state,
            GameBalanceSO balance,
            Random rng
        )
        {
            if (attackers.Count == 0)
                return -1;
            double total = 0;
            var weights = new double[attackers.Count];
            for (int i = 0; i < attackers.Count; i++)
            {
                var p = state.GetPlayer(attackers[i]);
                if (p == null)
                    continue;
                int lineIdx = (int)StartingSquadGacha.LineOf(p.info.primaryPosition);
                if (lineIdx < 0 || lineIdx >= balance.shotPositionWeights.Length)
                    continue;
                double posWeight = balance.shotPositionWeights[lineIdx];
                double statWeight =
                    (p.stats.technical.finishing * p.stats.mental.offTheBall) / 100.0;
                weights[i] = posWeight * statWeight;
                total += weights[i];
            }
            if (total <= 0)
                return -1;

            double r = rng.NextDouble() * total;
            double acc = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r < acc)
                    return attackers[i];
            }
            return attackers[attackers.Count - 1];
        }

        private static Player FindGoalkeeper(List<int> playerIds, GameState state)
        {
            for (int i = 0; i < playerIds.Count; i++)
            {
                var p = state.GetPlayer(playerIds[i]);
                if (p != null && p.info.primaryPosition == Position.GK)
                    return p;
            }
            return null;
        }

        // InjuryTypeSO 카탈로그 추첨 (weight 비례). 카탈로그 비어있으면 null.
        private static InjuryTypeSO PickInjuryType(Random rng)
        {
            var all = GameDatabase.AllInjuryTypes.ToList();
            if (all.Count == 0)
                return null;
            double total = all.Sum(t => Math.Max(0f, t.weight));
            if (total <= 0)
                return all[rng.Next(all.Count)];
            double r = rng.NextDouble() * total;
            double acc = 0;
            foreach (var t in all)
            {
                acc += Math.Max(0f, t.weight);
                if (r < acc)
                    return t;
            }
            return all[all.Count - 1];
        }
    }
}
