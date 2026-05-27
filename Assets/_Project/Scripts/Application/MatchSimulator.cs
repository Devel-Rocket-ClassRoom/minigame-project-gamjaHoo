// MatchSimulator.cs
// algorithms.md V1.0-2 Match Simulation V1.0 — 5-Zone Markov (Stage I.1' 상태 머신 + I.2' zone resolution).
// 인터페이스 (Simulate(match, state, balance) → MatchResult) 유지 (design-decisions.md #44 / #55).
// 상태: ballZone + possession. 매 분 1~3 ResolveAction(zone 분기) + possession contest. forward simulation (결과 미리 산출 폐기, #17 V0.1).
// I.3: Foul/Card/Penalty/Injury — Tackle 시 maybeFoul → box penalty / 2옐로 퇴장 / Injury + PlayerInjuredEvent + sentOff.
// I.5: collectEvents=true → Match.events 핵심 이벤트 (Goal/Shot/Card/Injury/KeyPass) 채움. textKey/textArgs 포함.
// 후속: 세트피스 = I.10 / 연장 = I.11.

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
            public int currentMinute;

            // 직전 KeyPass(슛 연결 패스) 발행자 — Goal 시 assist 카운트.
            public int homePendingAssist = -1;
            public int awayPendingAssist = -1;

            public Dictionary<int, PlayerMatchStat> stats;

            // I.3 — 카드/퇴장. yellows = 이 매치 누적 옐로. sentOff = 퇴장 (snap 제외).
            public Dictionary<int, int> yellows = new Dictionary<int, int>();
            public HashSet<int> sentOff = new HashSet<int>();

            // I.5 — 텍스트 이벤트 (collectEvents=true 한정).
            public bool collectEvents;

            // I.6 — 교체 잔여 횟수 (팀별 최대 3).
            public int homeSubsRemaining;
            public int awaySubsRemaining;
            public Club homeClub;
            public Club awayClub;
            public List<MatchEvent> events = new List<MatchEvent>();
        }

        public static MatchResult Simulate(
            Match match,
            GameState state,
            GameBalanceSO balance,
            bool collectEvents = false
        )
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
                collectEvents = collectEvents,
                homeSubsRemaining = balance.maxSubstitutionsPerTeam,
                awaySubsRemaining = balance.maxSubstitutionsPerTeam,
                homeClub = home,
                awayClub = away,
            };

            // 킥오프 이벤트
            if (collectEvents)
                EmitEvent(sim, MatchEventType.KickOff, Side.Home, 0, 0, "match_kickoff", null);

            // 4단계: 분 단위 step (1~90). 연장/stoppage = I.11.
            for (int minute = 1; minute <= 90; minute++)
            {
                sim.currentMinute = minute;
                if (minute == 46)
                {
                    // 후반 킥오프 — possession 교대 + ball Midfield
                    sim.ballZone = Zone.Midfield;
                    sim.possession = Side.Away;
                    if (collectEvents)
                        EmitEvent(
                            sim,
                            MatchEventType.HalfTime,
                            Side.Home,
                            0,
                            0,
                            "match_halftime",
                            null
                        );
                }
                PlayMinute(sim);
            }

            // 전체 종료
            if (collectEvents)
                EmitEvent(sim, MatchEventType.FullTime, Side.Home, 0, 0, "match_fulltime", null);

            // 5단계: 평점 계산 (I.4) + MatchResult (minutesPlayed 가변 = I.6)
            ComputeRatings(sim);

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
                events = sim.events,
            };
        }

        // ── 매 분 (PlayMinute) ────────────────────────────────────────

        private static void PlayMinute(SimState sim)
        {
            // I.6 — 전술 교체 체크 포인트 (45/60/75분 초입).
            int m = sim.currentMinute;
            if (m == 45 || m == 60 || m == 75)
            {
                TryTacticalSubs(sim, Side.Home);
                TryTacticalSubs(sim, Side.Away);
            }

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

            double attEff = Eff(BuildupAtt(passer), att, sim, passer) * 1.3; // 빌드업은 패스 우위
            double defEff =
                interceptor != null ? Eff(Press(interceptor), def, sim, interceptor) : 40.0;
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

            double attEff = Eff(MidfieldAtt(attacker), att, sim, attacker);
            double defEff =
                defender != null ? Eff(MidfieldDef(defender), def, sim, defender) : 40.0;
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
                    {
                        sim.stats[defender.id].tackles++;
                        MaybeFoul(sim, attacker, defender); // I.3 — 일반 파울 (FreeKick, penalty X)
                    }
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

            double attEff = Eff(AttackingThirdAtt(attacker), att, sim, attacker);
            double defEff =
                defender != null ? Eff(AttackingThirdDef(defender), def, sim, defender) : 40.0;
            double success = attEff / (attEff + defEff);

            if (sim.rng.NextDouble() < success)
            {
                // 드리블 성공 → box 진입. 직전 패스 = keyPass 후보 (assist 추적).
                SetPendingAssist(sim, att, attacker.id);
                sim.stats[attacker.id].keyPasses++;
                sim.ballZone = AttackingBox(att);

                // I.5 — 드리블 성공 (KeyPass) 이벤트
                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.Dribble,
                        att,
                        attacker.id,
                        0,
                        "match_dribble_fmt",
                        MakeArgs(sim, attacker.id, 0)
                    );
            }
            else
            {
                if (defender != null)
                {
                    if (sim.rng.NextDouble() < sim.balance.attackingThirdTackleRatio)
                    {
                        sim.stats[defender.id].tackles++;
                        MaybeFoul(sim, attacker, defender); // I.3 — 위험지역 파울
                    }
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

            // I.3 — 박스 안 수비 파울 → 페널티 (penaltyProbability)
            if (sim.rng.NextDouble() < sim.balance.penaltyProbability)
            {
                var fouler = SnapPlayer(sim, def, Line.DF);
                if (fouler != null)
                {
                    sim.stats[fouler.id].foulsCommitted++;
                    sim.stats[shooter.id].foulsSuffered++;
                    MaybeCard(sim, fouler.id);
                    MaybeInjury(sim, shooter);
                }
                // I.5 — 페널티 선언
                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.PenaltyAwarded,
                        att,
                        shooter.id,
                        fouler?.id ?? 0,
                        "match_penalty_awarded_fmt",
                        MakeArgs(sim, shooter.id, 0)
                    );
                ResolvePenalty(sim, att, shooter, gk);
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            double shootRating = Eff(ShotRating(shooter), att, sim, shooter);
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
                // off-target (block / miss)
                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.ShotOffTarget,
                        att,
                        shooter.id,
                        0,
                        "match_shot_off_target_fmt",
                        MakeArgs(sim, shooter.id, 0)
                    );
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            sim.stats[shooter.id].shotsOnTarget++;

            // I.5 — 유효슛
            if (sim.collectEvents)
                EmitEvent(
                    sim,
                    MatchEventType.ShotOnTarget,
                    att,
                    shooter.id,
                    0,
                    "match_shot_on_target_fmt",
                    MakeArgs(sim, shooter.id, 0)
                );

            // GK save 판정
            double gkRating = gk != null ? Eff(GkRating(gk), def, sim, gk) : 40.0;
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

                // I.5 — 골 이벤트
                if (sim.collectEvents)
                {
                    string key =
                        assister != -1 && assister != shooter.id
                            ? "match_goal_assist_fmt"
                            : "match_goal_fmt";
                    EmitEvent(
                        sim,
                        MatchEventType.Goal,
                        att,
                        shooter.id,
                        assister != -1 ? assister : 0,
                        key,
                        MakeArgs(sim, shooter.id, assister != -1 ? assister : 0)
                    );
                }
            }
            else if (gk != null && sim.stats.ContainsKey(gk.id))
            {
                sim.stats[gk.id].saves++; // I.4 — GK 선방

                // I.5 — 선방 이벤트
                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.ShotSaved,
                        att,
                        shooter.id,
                        gk.id,
                        "match_shot_saved_fmt",
                        MakeArgs(sim, gk.id, shooter.id)
                    );
            }

            TurnOver(sim, att, Zone.Midfield);
        }

        // ── I.3: Foul / Card / Penalty / Injury ──────────────────────

        // Tackle 시 파울 판정 (일반 파울 — FreeKick). foulsCommitted/Suffered + 카드 + 부상.
        private static void MaybeFoul(SimState sim, Player fouled, Player fouler)
        {
            if (fouler == null)
                return;
            double foulChance =
                sim.balance.foulProbability * (0.6 + fouler.stats.mental.aggression / 100.0 * 0.8);
            if (sim.rng.NextDouble() >= foulChance)
                return;

            sim.stats[fouler.id].foulsCommitted++;
            if (fouled != null && sim.stats.ContainsKey(fouled.id))
                sim.stats[fouled.id].foulsSuffered++;

            // I.5 — 파울 + 프리킥
            if (sim.collectEvents && fouled != null)
            {
                EmitEvent(
                    sim,
                    MatchEventType.Foul,
                    Opposite(sim.possession),
                    fouler.id,
                    fouled.id,
                    "match_foul_fmt",
                    MakeArgs(sim, fouler.id, fouled.id)
                );
                EmitEvent(
                    sim,
                    MatchEventType.FreeKick,
                    sim.possession,
                    0,
                    0,
                    "match_free_kick_fmt",
                    null
                );
            }

            MaybeCard(sim, fouler.id);
            MaybeInjury(sim, fouled);
        }

        // 카드 — yellow / direct red / 2옐로 퇴장. sentOff 갱신.
        private static void MaybeCard(SimState sim, int foulerId)
        {
            if (sim.rng.NextDouble() >= sim.balance.yellowCardProbability)
                return;

            // 파울러 소속팀 (카드 이벤트 side 정확성 — 파울러는 항상 수비팀).
            Side foulerSide = sim.homeXI.Contains(foulerId) ? Side.Home : Side.Away;

            if (sim.rng.NextDouble() < sim.balance.redCardProbability)
            {
                // 다이렉트 레드
                sim.stats[foulerId].redCards++;
                sim.sentOff.Add(foulerId);

                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.RedCard,
                        foulerSide,
                        foulerId,
                        0,
                        "match_red_card_fmt",
                        MakeArgs(sim, foulerId, 0)
                    );
                return;
            }

            int y = sim.yellows.TryGetValue(foulerId, out var c) ? c + 1 : 1;
            sim.yellows[foulerId] = y;
            sim.stats[foulerId].yellowCards++;
            if (y >= 2)
            {
                // 2옐로 = 퇴장 (redCards 로도 표기 → suspendedMatches 트리거)
                sim.stats[foulerId].redCards++;
                sim.sentOff.Add(foulerId);

                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.SecondYellow,
                        foulerSide,
                        foulerId,
                        0,
                        "match_second_yellow_fmt",
                        MakeArgs(sim, foulerId, 0)
                    );
            }
            else
            {
                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.YellowCard,
                        foulerSide,
                        foulerId,
                        0,
                        "match_yellow_card_fmt",
                        MakeArgs(sim, foulerId, 0)
                    );
            }
        }

        // 부상 — InjuryTypeSO 추첨 + InjuryInfo + PlayerInjuredEvent. 즉시 교체는 I.6.
        private static void MaybeInjury(SimState sim, Player fouled)
        {
            if (fouled == null || fouled.state?.injury == null)
                return;
            // injuryProneness (Hidden) 비례 + fatigue 임계 보정 (I.8)
            int proneness = fouled.hiddenAttrs?.injuryProneness ?? 50;
            double fatigueMult =
                fouled.state.fatigue > sim.balance.fatigueInjuryThreshold
                    ? sim.balance.fatigueInjuryMultiplier
                    : 1.0;
            double rate =
                sim.balance.matchInjuryProbability
                * (proneness / sim.balance.injuryProneRefDivisor)
                * fatigueMult;
            if (sim.rng.NextDouble() >= rate)
                return;
            if (fouled.state.injury.injuryTypeId != -1)
                return; // 이미 부상

            var type = PickInjuryType(sim.rng);
            if (type == null)
                return; // 카탈로그 부재 환경

            int baseDays = sim.rng.Next(type.minDays, type.maxDays + 1);
            var club = sim.gameState.GetClub(fouled.currentClubId);
            int medical = club?.facilities?.medicalLevel ?? 1;
            int gym = club?.facilities?.gymLevel ?? 1;
            int recoveryDays = InjurySystem.ComputeRecoveryDays(
                baseDays,
                medical,
                gym,
                sim.balance
            );

            fouled.state.injury = new InjuryInfo
            {
                injuryTypeId = type.id,
                startDate = sim.gameState.currentDate,
                expectedReturn = sim.gameState.currentDate.AddDays(recoveryDays),
                isCareerThreatening = recoveryDays >= sim.balance.injuryCareerThreateningDays,
            };
            EventBus.Publish(
                new PlayerInjuredEvent { playerId = fouled.id, injury = fouled.state.injury }
            );

            // I.5 — 부상 이벤트
            if (sim.collectEvents)
                EmitEvent(
                    sim,
                    MatchEventType.Injury,
                    sim.possession,
                    fouled.id,
                    0,
                    "match_injury_fmt",
                    MakeArgs(sim, fouled.id, 0)
                );

            // I.6 — 부상 즉시 교체 시도
            bool isHome = sim.homeXI.Contains(fouled.id);
            var injCtx = BuildSubContext(sim, isHome ? Side.Home : Side.Away);
            SubstitutionAI.TrySubstituteForInjury(injCtx, fouled.id);
            if (isHome)
                sim.homeSubsRemaining = injCtx.subsRemaining;
            else
                sim.awaySubsRemaining = injCtx.subsRemaining;
        }

        // 인-매치 페널티 — penaltyTaking vs GK. taker = 파울 얻은 선수 (단순; 지정 키커 = I.10).
        private static void ResolvePenalty(SimState sim, Side att, Player taker, Player gk)
        {
            if (taker == null)
                return;
            sim.stats[taker.id].shots++;
            sim.stats[taker.id].shotsOnTarget++;

            double gkRating = gk != null ? GkRating(gk) : 40.0;
            double conv = Clamp(
                sim.balance.penaltyConversion
                    + (taker.stats.technical.penaltyTaking - gkRating) / 300.0,
                0.5,
                0.95
            );
            if (sim.rng.NextDouble() < conv)
            {
                if (att == Side.Home)
                    sim.homeScore++;
                else
                    sim.awayScore++;
                sim.stats[taker.id].goals++;

                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.PenaltyGoal,
                        att,
                        taker.id,
                        0,
                        "match_penalty_goal_fmt",
                        MakeArgs(sim, taker.id, 0)
                    );
            }
            else
            {
                if (gk != null && sim.stats.ContainsKey(gk.id))
                    sim.stats[gk.id].saves++; // I.4 — 페널티 선방

                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.PenaltyMiss,
                        att,
                        taker.id,
                        0,
                        "match_penalty_miss_fmt",
                        MakeArgs(sim, taker.id, 0)
                    );
            }
        }

        // InjuryTypeSO 카탈로그 weight 비례 추첨.
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

        // ── I.4: 평점 계산 (매치 종료 시) ────────────────────────────

        // 이벤트 누적 통계 → rating (base 6.5, clamp 1.0~10.0). pressureHandling 빅매치 가산 = V1.x.
        private static void ComputeRatings(SimState sim)
        {
            var b = sim.balance;
            var homeSet = new HashSet<int>(sim.homeXI);
            foreach (var stat in sim.stats.Values)
            {
                var p = sim.gameState.GetPlayer(stat.playerId);
                bool isHome = homeSet.Contains(stat.playerId);

                double r = b.ratingBase;
                r += stat.goals * b.ratingGoalBonus;
                r += stat.assists * b.ratingAssistBonus;
                r += stat.keyPasses * b.ratingKeyPassBonus;
                r += (stat.tackles + stat.interceptions) * b.ratingDefActionBonus;
                r += stat.shotsOnTarget * b.ratingShotOnTargetBonus;
                r += stat.yellowCards * b.ratingYellowPenalty; // penalty 음수
                r += stat.redCards * b.ratingRedPenalty;

                // GK — 선방 / 무실점 / 실점
                if (p != null && p.info.primaryPosition == Position.GK)
                {
                    r += stat.saves * b.ratingSaveBonus;
                    int conceded = isHome ? sim.awayScore : sim.homeScore;
                    if (conceded == 0)
                        r += b.ratingCleanSheetBonus;
                    else
                        r += conceded * b.ratingConcededPenalty;
                }

                // 팀 승/패 전원 가감
                int teamScore = isHome ? sim.homeScore : sim.awayScore;
                int oppScore = isHome ? sim.awayScore : sim.homeScore;
                if (teamScore > oppScore)
                    r += b.ratingWinBonus;
                else if (teamScore < oppScore)
                    r += b.ratingLossPenalty;

                r = Math.Round(r, 1);
                stat.rating = (float)Clamp(r, b.ratingMin, b.ratingMax);
            }
        }

        // ── I.5: 이벤트 발행 헬퍼 ────────────────────────────────────

        private static void EmitEvent(
            SimState sim,
            MatchEventType type,
            Side side,
            int actorId,
            int targetId,
            string textKey,
            Dictionary<string, string> textArgs
        )
        {
            sim.events.Add(
                new MatchEvent
                {
                    minute = sim.currentMinute,
                    type = type,
                    side = (int)side,
                    actorPlayerId = actorId,
                    targetPlayerId = targetId,
                    textKey = textKey,
                    textArgs = textArgs,
                }
            );
        }

        // textArgs 빌더 — actorId/targetId → 선수 이름 (UI 표시용).
        private static Dictionary<string, string> MakeArgs(SimState sim, int actorId, int targetId)
        {
            var args = new Dictionary<string, string> { ["minute"] = sim.currentMinute.ToString() };
            if (actorId > 0)
            {
                var p = sim.gameState.GetPlayer(actorId);
                args["playerName"] = PlayerDisplayName(p, actorId);
            }
            if (targetId > 0)
            {
                var p = sim.gameState.GetPlayer(targetId);
                string targetName = PlayerDisplayName(p, targetId);
                args["targetName"] = targetName;
                args["gkName"] = targetName;
                args["assistName"] = targetName;
                args["foulerName"] = PlayerDisplayName(sim.gameState.GetPlayer(actorId), actorId);
                args["fouledName"] = targetName;
            }
            return args;
        }

        private static string PlayerDisplayName(Player p, int fallbackId)
        {
            if (p == null)
                return fallbackId.ToString();
            var info = p.info;
            if (!string.IsNullOrEmpty(info.lastName))
                return string.IsNullOrEmpty(info.firstName)
                    ? info.lastName
                    : $"{info.firstName[0]}. {info.lastName}";
            return fallbackId.ToString();
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
                    if (sim.sentOff.Contains(id))
                        return false; // I.3 — 퇴장 선수 제외
                    var p = sim.gameState.GetPlayer(id);
                    return p != null && StartingSquadGacha.LineOf(p.info.primaryPosition) == line;
                })
                .ToList();
            if (candidates.Count == 0)
                candidates = xi.Where(id => !sim.sentOff.Contains(id)).ToList();
            if (candidates.Count == 0)
                return null;
            int pid = candidates[sim.rng.Next(candidates.Count)];
            return sim.gameState.GetPlayer(pid);
        }

        private static Player FindGoalkeeper(SimState sim, Side s)
        {
            var xi = XIof(sim, s);
            for (int i = 0; i < xi.Count; i++)
            {
                if (sim.sentOff.Contains(xi[i]))
                    continue; // I.3 — 퇴장 GK 제외 (필드 플레이어 골문, 드묾)
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
                if (sim.sentOff.Contains(id))
                    continue; // I.3 — 퇴장 제외 (수적 열세 → 점유 contest 약화)
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

        // raw stat × homeMod × fatigue/form/morale/mood (I.8). player=null → 팀 평균 경로 (homeMod 만).
        private static double Eff(double raw, Side s, SimState sim, Player player = null)
        {
            double homeMod = s == Side.Home ? sim.balance.homeAdvantageMultiplier : 1.0;
            if (player?.state == null)
                return raw * homeMod;

            // fatigue 임계 — > 50 → 1pt 당 -1%, floor 0.6
            double perf = 1.0;
            int fatigue = player.state.fatigue;
            if (fatigue > sim.balance.fatiguePerfThreshold)
                perf = System.Math.Max(
                    sim.balance.fatiguePerfFloor,
                    1.0
                        - (fatigue - sim.balance.fatiguePerfThreshold)
                            * sim.balance.fatiguePerfPenaltyPerPoint
                );

            // form / morale 곱셈
            double formMod =
                (1.0 + (player.state.form - 50.0) / sim.balance.formCoeff)
                * (1.0 + (player.state.morale - 50.0) / sim.balance.moraleCoeff);

            // dressingRoomMood (G.3)
            var club = sim.gameState.GetClub(player.currentClubId);
            double moodMod =
                club?.season != null
                && club.season.dressingRoomMood < sim.balance.dressingRoomMoodLowThreshold
                    ? sim.balance.dressingRoomLowMoodStrengthFactor
                    : 1.0;

            return raw * perf * formMod * moodMod * homeMod;
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

        // ── I.6 교체 헬퍼 ────────────────────────────────────────────────

        private static SubstitutionAI.SubContext BuildSubContext(SimState sim, Side side)
        {
            bool isHome = side == Side.Home;
            return new SubstitutionAI.SubContext
            {
                xi = isHome ? sim.homeXI : sim.awayXI,
                subsRemaining = isHome ? sim.homeSubsRemaining : sim.awaySubsRemaining,
                club = isHome ? sim.homeClub : sim.awayClub,
                state = sim.gameState,
                balance = sim.balance,
                sentOff = sim.sentOff,
                stats = sim.stats,
                currentMinute = sim.currentMinute,
                homeScore = sim.homeScore,
                awayScore = sim.awayScore,
                isHome = isHome,
                events = sim.collectEvents ? sim.events : null,
            };
        }

        private static void TryTacticalSubs(SimState sim, Side side)
        {
            var ctx = BuildSubContext(sim, side);
            SubstitutionAI.TryTacticalSubstitution(ctx);
            if (side == Side.Home)
                sim.homeSubsRemaining = ctx.subsRemaining;
            else
                sim.awaySubsRemaining = ctx.subsRemaining;
        }
    }
}
