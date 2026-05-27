// MatchSimulator.cs
// algorithms.md V1.0-2 Match Simulation V1.0 — 5-Zone Markov (Stage I.1' 상태 머신 + I.2' zone resolution).
// 인터페이스 (Simulate(match, state, balance) → MatchResult) 유지 (design-decisions.md #44 / #55).
// 상태: ballZone + possession. 매 분 1~3 ResolveAction(zone 분기) + possession contest. forward simulation (결과 미리 산출 폐기, #17 V0.1).
// 후속: Foul/Card/Penalty/Injury = I.3 / 평점 = I.4 / 텍스트 events = I.5 / SubstitutionAI = I.6 / background collectEvents = I.7 / fatigue·form·morale = I.8 / strengthExponent 폐기 = I.9 / 세트피스 = I.10 / 연장 = I.11.

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
        // 피치 5-zone (Home 기준 방향). Home 공격 = AwayBox 향함.
        private enum Zone
        {
            HomeBox,
            HomeDefense,
            Midfield,
            AwayDefense,
            AwayBox,
        }

        private enum Side
        {
            Home,
            Away,
        }

        // 매치 시뮬레이션 내부 상태 (직렬화 X — 매치 종료 시 MatchResult 로 변환).
        private sealed class SimState
        {
            public Random rng;
            public GameState gameState;
            public GameBalanceSO balance;
            public List<int> homeXI;
            public List<int> awayXI;

            public Zone ballZone = Zone.Midfield;
            public Side possession = Side.Home;
            public int homeScore;
            public int awayScore;
            public int homePossessionTicks;
            public int awayPossessionTicks;

            // 직전 KeyPass(슛 연결 패스) 발행자 — Goal 시 assist 카운트.
            public int homePendingAssist = -1;
            public int awayPendingAssist = -1;

            public Dictionary<int, PlayerMatchStat> stats;
        }

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

            // 1단계: 시드 고정 (forward simulation — 결정성은 시드에서만, #17 V1.0)
            var rng = new Random(match.id ^ state.randomSeed);

            // 2단계: starting11 자동 선정 (Tactic = Stage J. 부상/정지 제외)
            var homeXI = SelectStartingEleven(home, state);
            var awayXI = SelectStartingEleven(away, state);

            // 3단계: 상태 초기화
            var sim = new SimState
            {
                rng = rng,
                gameState = state,
                balance = balance,
                homeXI = homeXI,
                awayXI = awayXI,
                ballZone = Zone.Midfield,
                possession = Side.Home,
                stats = InitStatsMap(homeXI, awayXI),
            };

            // 4단계: 분 단위 step (1~90). 연장/stoppage = I.11.
            for (int minute = 1; minute <= 90; minute++)
            {
                if (minute == 46)
                {
                    // 후반 킥오프 — possession 교대 + ball Midfield
                    sim.ballZone = Zone.Midfield;
                    sim.possession = Side.Away;
                }
                PlayMinute(sim);
            }

            // 5단계: MatchResult (rating = I.4, minutesPlayed 가변 = I.6)
            int totalTicks = sim.homePossessionTicks + sim.awayPossessionTicks;
            float homePct =
                totalTicks > 0 ? (float)sim.homePossessionTicks / totalTicks * 100f : 50f;

            return new MatchResult
            {
                homeScore = sim.homeScore,
                awayScore = sim.awayScore,
                homeStarting11 = homeXI,
                awayStarting11 = awayXI,
                playerStats = sim.stats.Values.ToList(),
                homePossessionPct = homePct,
                awayPossessionPct = 100f - homePct,
            };
        }

        // ── 매 분 (PlayMinute) ────────────────────────────────────────

        private static void PlayMinute(SimState sim)
        {
            // possession 누적
            if (sim.possession == Side.Home)
                sim.homePossessionTicks++;
            else
                sim.awayPossessionTicks++;

            // 1~3 actions
            int actions = sim.rng.Next(
                sim.balance.actionsPerMinuteMin,
                sim.balance.actionsPerMinuteMax + 1
            );
            for (int i = 0; i < actions; i++)
                ResolveAction(sim);

            // Possession contest (midfield 대결)
            double midAtt = EffectiveMidfield(sim, sim.possession);
            double midDef = EffectiveMidfield(sim, Opposite(sim.possession));
            double total = midAtt + midDef;
            double retain = total > 0 ? midAtt / total : 0.5;
            if (sim.rng.NextDouble() > retain)
            {
                sim.possession = Opposite(sim.possession);
                sim.ballZone = Zone.Midfield;
            }
        }

        // ── ResolveAction (zone 분기) ─────────────────────────────────

        private static void ResolveAction(SimState sim)
        {
            Side att = sim.possession;
            if (sim.ballZone == AttackingBox(att))
                ResolveShot(sim, att);
            else if (sim.ballZone == AttackingThird(att))
                ResolveAttackingThird(sim, att);
            else if (sim.ballZone == Zone.Midfield)
                ResolveMidfield(sim, att);
            else
                ResolveBuildup(sim, att);
        }

        // 수비 third — 빌드업 패스. 성공 → Midfield / 실패 → Interception + 점유 전환.
        private static void ResolveBuildup(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var passer = SnapPlayer(sim, att, Line.DF);
            var interceptor = SnapPlayer(sim, def, Line.MF);
            if (passer == null)
                return;

            double attEff = Eff(BuildupAtt(passer), att, sim) * 1.3; // 빌드업은 패스 우위
            double defEff = interceptor != null ? Eff(Press(interceptor), def, sim) : 40.0;
            double success = attEff / (attEff + defEff);

            sim.stats[passer.id].passes++;
            if (sim.rng.NextDouble() < success)
            {
                sim.stats[passer.id].passesCompleted++;
                sim.ballZone = Zone.Midfield;
            }
            else
            {
                if (interceptor != null)
                    sim.stats[interceptor.id].interceptions++;
                TurnOver(sim, att, Zone.Midfield);
            }
        }

        // 미드필드 대결. 성공 → AttackingThird / 실패 → Tackle·Interception + 점유 전환.
        private static void ResolveMidfield(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var attacker = SnapPlayer(sim, att, Line.MF);
            var defender = SnapPlayer(sim, def, Line.MF);
            if (attacker == null)
                return;

            double attEff = Eff(MidfieldAtt(attacker), att, sim);
            double defEff = defender != null ? Eff(MidfieldDef(defender), def, sim) : 40.0;
            double success = attEff / (attEff + defEff);

            sim.stats[attacker.id].passes++;
            if (sim.rng.NextDouble() < success)
            {
                sim.stats[attacker.id].passesCompleted++;
                sim.ballZone = AttackingThird(att);
            }
            else
            {
                if (defender != null)
                {
                    if (sim.rng.NextDouble() < sim.balance.midfieldTackleRatio)
                        sim.stats[defender.id].tackles++;
                    else
                        sim.stats[defender.id].interceptions++;
                }
                TurnOver(sim, att, Zone.Midfield);
            }
        }

        // 공격 third — 드리블 돌파. 성공 → Box / 실패 → Tackle·Clearance + Corner(25%) + 점유 전환.
        private static void ResolveAttackingThird(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var attacker = SnapPlayer(sim, att, Line.AT);
            var defender = SnapPlayer(sim, def, Line.DF);
            if (attacker == null)
                return;

            double attEff = Eff(AttackingThirdAtt(attacker), att, sim);
            double defEff = defender != null ? Eff(AttackingThirdDef(defender), def, sim) : 40.0;
            double success = attEff / (attEff + defEff);

            if (sim.rng.NextDouble() < success)
            {
                // 드리블 성공 → box 진입. 직전 패스 = keyPass 후보 (assist 추적).
                SetPendingAssist(sim, att, attacker.id);
                sim.stats[attacker.id].keyPasses++;
                sim.ballZone = AttackingBox(att);
            }
            else
            {
                if (defender != null)
                {
                    if (sim.rng.NextDouble() < sim.balance.attackingThirdTackleRatio)
                        sim.stats[defender.id].tackles++;
                    // else Clearance (통계 X)
                }
                // Corner 기회 (25%) → box 재진입 (30%)
                if (
                    sim.rng.NextDouble() < sim.balance.zoneCornerChance
                    && sim.rng.NextDouble() < sim.balance.zoneCornerToBoxChance
                )
                {
                    sim.ballZone = AttackingBox(att);
                    return;
                }
                TurnOver(sim, att, DefensiveThird(att));
            }
        }

        // 박스 — 슈팅. on-target → GK save 판정 → Goal/Saved. off → block/miss.
        private static void ResolveShot(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var shooter = SnapPlayer(sim, att, Line.AT);
            var gk = FindGoalkeeper(sim, def);
            if (shooter == null)
            {
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            double shootRating = Eff(ShotRating(shooter), att, sim);
            sim.stats[shooter.id].shots++;

            // On-target 판정
            double accuracy = Clamp(
                sim.balance.shotAccuracyBase
                    + (shootRating - 50.0) / sim.balance.shotAccuracyDivisor,
                0.15,
                0.85
            );
            if (sim.rng.NextDouble() > accuracy)
            {
                // off-target (block / miss — 통계는 shots 만)
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            sim.stats[shooter.id].shotsOnTarget++;

            // GK save 판정
            double gkRating = gk != null ? Eff(GkRating(gk), def, sim) : 40.0;
            double conversion = Clamp(
                sim.balance.goalConversionBase
                    + (shootRating - gkRating) / sim.balance.goalConversionDivisor,
                0.10,
                0.70
            );
            if (sim.rng.NextDouble() < conversion)
            {
                // GOAL
                if (att == Side.Home)
                    sim.homeScore++;
                else
                    sim.awayScore++;
                sim.stats[shooter.id].goals++;

                int assister = GetPendingAssist(sim, att);
                if (assister != -1 && assister != shooter.id && sim.stats.ContainsKey(assister))
                    sim.stats[assister].assists++;
                ClearPendingAssist(sim, att);
            }
            // else Saved (GK 평점 = I.4)

            TurnOver(sim, att, Zone.Midfield);
        }

        // ── 헬퍼: 상태 전이 ───────────────────────────────────────────

        private static void TurnOver(SimState sim, Side att, Zone newZone)
        {
            sim.possession = Opposite(att);
            sim.ballZone = newZone;
            ClearPendingAssist(sim, att);
        }

        private static Side Opposite(Side s) => s == Side.Home ? Side.Away : Side.Home;

        private static Zone AttackingBox(Side s) => s == Side.Home ? Zone.AwayBox : Zone.HomeBox;

        private static Zone AttackingThird(Side s) =>
            s == Side.Home ? Zone.AwayDefense : Zone.HomeDefense;

        private static Zone DefensiveThird(Side s) =>
            s == Side.Home ? Zone.HomeDefense : Zone.AwayDefense;

        private static void SetPendingAssist(SimState sim, Side att, int playerId)
        {
            if (att == Side.Home)
                sim.homePendingAssist = playerId;
            else
                sim.awayPendingAssist = playerId;
        }

        private static int GetPendingAssist(SimState sim, Side att) =>
            att == Side.Home ? sim.homePendingAssist : sim.awayPendingAssist;

        private static void ClearPendingAssist(SimState sim, Side att)
        {
            if (att == Side.Home)
                sim.homePendingAssist = -1;
            else
                sim.awayPendingAssist = -1;
        }

        // ── 헬퍼: 선수 선정 ───────────────────────────────────────────

        private static List<int> XIof(SimState sim, Side s) =>
            s == Side.Home ? sim.homeXI : sim.awayXI;

        // 해당 Line 의 선수 중 랜덤 1명. 없으면 XI 전체에서 랜덤 (fallback).
        private static Player SnapPlayer(SimState sim, Side s, Line line)
        {
            var xi = XIof(sim, s);
            if (xi.Count == 0)
                return null;
            var candidates = xi.Where(id =>
                {
                    var p = sim.gameState.GetPlayer(id);
                    return p != null && StartingSquadGacha.LineOf(p.info.primaryPosition) == line;
                })
                .ToList();
            if (candidates.Count == 0)
                candidates = xi;
            int pid = candidates[sim.rng.Next(candidates.Count)];
            return sim.gameState.GetPlayer(pid);
        }

        private static Player FindGoalkeeper(SimState sim, Side s)
        {
            var xi = XIof(sim, s);
            for (int i = 0; i < xi.Count; i++)
            {
                var p = sim.gameState.GetPlayer(xi[i]);
                if (p != null && p.info.primaryPosition == Position.GK)
                    return p;
            }
            return null;
        }

        // MF 라인 평균 rating × homeMod (possession contest 용).
        private static double EffectiveMidfield(SimState sim, Side s)
        {
            var xi = XIof(sim, s);
            double sum = 0;
            int count = 0;
            foreach (var id in xi)
            {
                var p = sim.gameState.GetPlayer(id);
                if (p == null || StartingSquadGacha.LineOf(p.info.primaryPosition) != Line.MF)
                    continue;
                sum += MidfieldAtt(p);
                count++;
            }
            double avg = count > 0 ? sum / count : 40.0;
            return Eff(avg, s, sim);
        }

        // ── 헬퍼: stat 조합 (49 stat zone별 매핑, algorithms.md V1.0-2) ──

        private static double BuildupAtt(Player p) =>
            (
                p.stats.technical.passing
                + p.stats.mental.vision
                + p.stats.mental.composure
                + p.stats.mental.teamwork
            ) / 4.0;

        private static double Press(Player p) =>
            (p.stats.mental.workRate + p.stats.mental.aggression + p.stats.mental.positioning)
            / 3.0;

        private static double MidfieldAtt(Player p) =>
            (
                p.stats.technical.dribbling
                + p.stats.technical.passing
                + p.stats.mental.vision
                + p.stats.mental.teamwork
            ) / 4.0;

        private static double MidfieldDef(Player p) =>
            (
                p.stats.technical.tackling
                + p.stats.mental.positioning
                + p.stats.mental.decisions
                + p.stats.mental.teamwork
            ) / 4.0;

        private static double AttackingThirdAtt(Player p) =>
            (
                p.stats.technical.dribbling
                + p.stats.physical.pace
                + p.stats.physical.agility
                + p.stats.mental.composure
            ) / 4.0;

        private static double AttackingThirdDef(Player p) =>
            (
                p.stats.technical.marking
                + p.stats.technical.tackling
                + p.stats.mental.positioning
                + p.stats.technical.heading
            ) / 4.0;

        private static double ShotRating(Player p) =>
            (p.stats.technical.finishing + p.stats.mental.composure + p.stats.mental.decisions)
            / 3.0;

        private static double GkRating(Player p) =>
            (p.stats.gk.handling + p.stats.gk.reflexes + p.stats.mental.positioning) / 3.0;

        // raw stat × homeMod (fatigue/form/morale/mentality/trait 보정 = I.8 / J).
        private static double Eff(double raw, Side s, SimState sim)
        {
            double homeMod = s == Side.Home ? sim.balance.homeAdvantageMultiplier : 1.0;
            return raw * homeMod;
        }

        private static double Clamp(double v, double lo, double hi) =>
            v < lo ? lo : (v > hi ? hi : v);

        // ── 헬퍼: starting11 / stats ──────────────────────────────────

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

        private static Dictionary<int, PlayerMatchStat> InitStatsMap(
            List<int> homeXI,
            List<int> awayXI
        )
        {
            var map = new Dictionary<int, PlayerMatchStat>(homeXI.Count + awayXI.Count);
            foreach (var id in homeXI)
                map[id] = new PlayerMatchStat { playerId = id, minutesPlayed = 90 };
            foreach (var id in awayXI)
                map[id] = new PlayerMatchStat { playerId = id, minutesPlayed = 90 };
            return map;
        }
    }
}
