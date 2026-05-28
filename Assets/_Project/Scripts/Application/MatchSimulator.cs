// MatchSimulator.cs
// algorithms.md V1.0-2 Match Simulation V1.0 — 5-Zone Markov (Stage I.1' 상태 머신 + I.2' zone resolution).
// 인터페이스 (Simulate(match, state, balance) → MatchResult) 유지 (design-decisions.md #44 / #55).
// 상태: ballZone + possession. 매 분 1~3 ResolveAction(zone 분기) + possession contest. forward simulation (결과 미리 산출 폐기, #17 V0.1).
// I.3: Foul/Card/Penalty/Injury — Tackle 시 maybeFoul → box penalty / 2옐로 퇴장 / Injury + PlayerInjuredEvent + sentOff.
// I.5: collectEvents=true → Match.events 핵심 이벤트 (Goal/Shot/Card/Injury/KeyPass) 채움. textKey/textArgs 포함.
// I.10: 세트피스 (Corner/FreeKick/LongThrow). I.11: 연장/승부차기.

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

            // 컵 매치: allowsExtraTime=true 로 설정. League는 무승부 허용.
            // V0.1 경고 제거 — I.11 에서 컵 연장/승부차기 지원.

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

            // 4단계: 전반 (1~45+stoppage) — I.11 stoppage 추가.
            int firstHalfStoppage = sim.rng.Next(0, sim.balance.stoppageTimeMax + 1);
            for (int minute = 1; minute <= 45 + firstHalfStoppage; minute++)
            {
                sim.currentMinute = minute;
                PlayMinute(sim);
            }

            // 하프타임 — possession 교대 + ball Midfield
            sim.ballZone = Zone.Midfield;
            sim.possession = Side.Away;
            if (collectEvents)
                EmitEvent(sim, MatchEventType.HalfTime, Side.Home, 0, 0, "match_halftime", null);

            // 후반 (46~90+stoppage)
            int secondHalfStoppage = sim.rng.Next(0, sim.balance.stoppageTimeMax + 1);
            for (int minute = 46; minute <= 90 + secondHalfStoppage; minute++)
            {
                sim.currentMinute = minute;
                PlayMinute(sim);
            }

            // 정규시간 종료
            if (collectEvents)
                EmitEvent(sim, MatchEventType.FullTime, Side.Home, 0, 0, "match_fulltime", null);

            // I.11 — 컵 동점 시 연장전 + 승부차기
            int penaltyHomeScore = 0,
                penaltyAwayScore = 0;
            bool decidedByPenalties = false;

            if (match.allowsExtraTime && sim.homeScore == sim.awayScore)
            {
                if (collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.ExtraTimeKickOff,
                        Side.Home,
                        0,
                        0,
                        "match_extra_time_kickoff",
                        null
                    );
                sim.ballZone = Zone.Midfield;
                sim.possession = Side.Home;

                // 연장 전반 (91~105+stoppage)
                int etFirstStoppage = sim.rng.Next(0, sim.balance.extraTimeStoppageMax + 1);
                for (int minute = 91; minute <= 105 + etFirstStoppage; minute++)
                {
                    sim.currentMinute = minute;
                    PlayMinute(sim);
                }

                sim.ballZone = Zone.Midfield;
                sim.possession = Side.Away;
                if (collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.ExtraTimeHalfTime,
                        Side.Home,
                        0,
                        0,
                        "match_extra_time_halftime",
                        null
                    );

                // 연장 후반 (106~120+stoppage)
                int etSecondStoppage = sim.rng.Next(0, sim.balance.extraTimeStoppageMax + 1);
                for (int minute = 106; minute <= 120 + etSecondStoppage; minute++)
                {
                    sim.currentMinute = minute;
                    PlayMinute(sim);
                }

                // 연장 후에도 동점 → 승부차기
                if (sim.homeScore == sim.awayScore)
                {
                    if (collectEvents)
                        EmitEvent(
                            sim,
                            MatchEventType.ExtraTimeEnd,
                            Side.Home,
                            0,
                            0,
                            "match_extra_time_end",
                            null
                        );
                    (penaltyHomeScore, penaltyAwayScore) = SimulatePenaltyShootout(sim);
                    decidedByPenalties = true;
                }
            }

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
                penaltyHomeScore = penaltyHomeScore,
                penaltyAwayScore = penaltyAwayScore,
                decidedByPenalties = decidedByPenalties,
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
            var attacker = SnapPlayer(sim, att, Line.MF, TacticImpact.EventKeyPass);
            var defender = SnapPlayer(sim, def, Line.MF, TacticImpact.EventTackle);
            if (attacker == null)
                return;

            double attEff =
                Eff(MidfieldAtt(attacker), att, sim, attacker) * MentalityKeyPassMult(sim, att);
            double defEff =
                defender != null
                    ? Eff(MidfieldDef(defender), def, sim, defender) * MentalityPressMult(sim, def)
                    : 40.0;
            double success = attEff / (attEff + defEff);

            sim.stats[attacker.id].passes++;
            if (sim.rng.NextDouble() < success)
            {
                sim.stats[attacker.id].passesCompleted++;
                sim.ballZone = AttackingThird(att);
            }
            else
            {
                bool foulOccurred = false;
                if (defender != null)
                {
                    if (sim.rng.NextDouble() < sim.balance.midfieldTackleRatio)
                    {
                        sim.stats[defender.id].tackles++;
                        foulOccurred = MaybeFoul(sim, attacker, defender);
                    }
                    else
                        sim.stats[defender.id].interceptions++;
                }
                if (foulOccurred)
                    ResolveFreeKick(sim, att); // I.10 — FK 스탯 해결 (att 팀이 FK 수혜)
                else
                    TurnOver(sim, att, Zone.Midfield);
            }
        }

        // 공격 third — 드리블 돌파. 성공 → Box / 실패 → Tackle·Clearance + Corner(25%) + 점유 전환.
        private static void ResolveAttackingThird(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var attacker = SnapPlayer(sim, att, Line.AT, TacticImpact.EventKeyPass);
            var defender = SnapPlayer(sim, def, Line.DF, TacticImpact.EventTackle);
            if (attacker == null)
                return;

            double attEff =
                Eff(AttackingThirdAtt(attacker), att, sim, attacker) * MentalityShotMult(sim, att);
            double defEff =
                defender != null
                    ? Eff(AttackingThirdDef(defender), def, sim, defender)
                        * MentalityPressMult(sim, def)
                    : 40.0;
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
                bool foulOccurred = false;
                if (defender != null)
                {
                    if (sim.rng.NextDouble() < sim.balance.attackingThirdTackleRatio)
                    {
                        sim.stats[defender.id].tackles++;
                        foulOccurred = MaybeFoul(sim, attacker, defender);
                    }
                    // else Clearance (통계 X)
                }
                if (foulOccurred)
                {
                    ResolveFreeKick(sim, att); // I.10 — 위험지역 FK
                    return;
                }
                // I.10 — Corner (25%) / LongThrow (10%) / TurnOver
                double cornerRoll = sim.rng.NextDouble();
                if (cornerRoll < sim.balance.zoneCornerChance)
                {
                    ResolveCorner(sim, att);
                    return;
                }
                if (cornerRoll < sim.balance.zoneCornerChance + sim.balance.longThrowChance)
                {
                    ResolveLongThrow(sim, att);
                    return;
                }
                TurnOver(sim, att, DefensiveThird(att));
            }
        }

        // 박스 — 슈팅. on-target → GK save 판정 → Goal/Saved. off → block/miss.
        private static void ResolveShot(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var shooter = SnapPlayer(sim, att, Line.AT, TacticImpact.EventShot);
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

            double shootRating =
                Eff(ShotRating(shooter), att, sim, shooter) * MentalityShotMult(sim, att);
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

        // Tackle 시 파울 판정 (일반 파울 — FreeKick). foulsCommitted/Suffered + 카드 + 부상. 파울 발생 시 true 반환.
        private static bool MaybeFoul(SimState sim, Player fouled, Player fouler)
        {
            if (fouler == null)
                return false;
            double foulChance =
                sim.balance.foulProbability * (0.6 + fouler.stats.mental.aggression / 100.0 * 0.8);
            if (sim.rng.NextDouble() >= foulChance)
                return false;

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
            return true;
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

        // 해당 Line 의 선수 중 1명. eventType 지정 + 라인업 배정 시 Tactic 가중 추첨 (J.4), 아니면 균등 랜덤. 없으면 XI 전체에서 (fallback).
        private static Player SnapPlayer(SimState sim, Side s, Line line, string eventType = null)
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

            // J.4 — Tactic 가중 선택 (라인업 배정 + 후보 ≥ 2). 미배정/tactic null → 균등 랜덤 (회귀 없음).
            var tactic = (s == Side.Home ? sim.homeClub : sim.awayClub)?.tactic;
            if (eventType != null && candidates.Count > 1 && HasLineup(tactic))
            {
                double total = 0;
                var weights = new double[candidates.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    double w = TacticImpact.ComputeEventWeight(
                        tactic,
                        candidates[i],
                        sim.gameState,
                        eventType,
                        sim.balance
                    );
                    if (w < 0)
                        w = 0;
                    weights[i] = w;
                    total += w;
                }
                if (total > 0)
                {
                    double r = sim.rng.NextDouble() * total;
                    double acc = 0;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        acc += weights[i];
                        if (r < acc)
                            return sim.gameState.GetPlayer(candidates[i]);
                    }
                    return sim.gameState.GetPlayer(candidates[candidates.Count - 1]);
                }
            }

            int pid = candidates[sim.rng.Next(candidates.Count)];
            return sim.gameState.GetPlayer(pid);
        }

        // J.4 — 슬롯에 선수가 배정됐는지 (assignedPlayerId >= 0). 미배정(-1)이면 Tactic 가중 비활성 (J.5 LineupScene 이후 활성).
        private static bool HasLineup(Tactic tactic) =>
            tactic?.slots != null && tactic.slots.Any(sl => sl.assignedPlayerId >= 0);

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

        // J.3 — 팀 Mentality 에 따른 이벤트 가중치 (tactic null 이면 Balanced=1.0 폴백).
        private static float MentalityShotMult(SimState sim, Side s)
        {
            var club = s == Side.Home ? sim.homeClub : sim.awayClub;
            int m = (int)(club?.tactic?.mentality ?? Mentality.Balanced);
            var mults = sim.balance.mentalityShotMultiplier;
            return mults != null && m < mults.Length ? mults[m] : 1.0f;
        }

        private static float MentalityPressMult(SimState sim, Side s)
        {
            var club = s == Side.Home ? sim.homeClub : sim.awayClub;
            int m = (int)(club?.tactic?.mentality ?? Mentality.Balanced);
            var mults = sim.balance.mentalityPressureMultiplier;
            return mults != null && m < mults.Length ? mults[m] : 1.0f;
        }

        private static float MentalityKeyPassMult(SimState sim, Side s)
        {
            var club = s == Side.Home ? sim.homeClub : sim.awayClub;
            int m = (int)(club?.tactic?.mentality ?? Mentality.Balanced);
            var mults = sim.balance.mentalityKeyPassMultiplier;
            return mults != null && m < mults.Length ? mults[m] : 1.0f;
        }

        // ── I.11: 승부차기 ────────────────────────────────────────────

        private static (int homePen, int awayPen) SimulatePenaltyShootout(SimState sim)
        {
            int homePen = 0,
                awayPen = 0;
            var homeQueue = BuildShootoutQueue(sim, Side.Home);
            var awayQueue = BuildShootoutQueue(sim, Side.Away);
            int homeIdx = 0,
                awayIdx = 0;

            // 5 라운드 + 조기 종료 + sudden death
            int round = 0;
            bool finished = false;
            while (!finished)
            {
                bool isRegular = round < sim.balance.penaltyShootoutRounds;

                // Home 킥
                var homeTaker = homeQueue.Count > 0 ? homeQueue[homeIdx % homeQueue.Count] : null;
                bool homeScored = SimulatePenaltyKick(
                    sim,
                    Side.Home,
                    homeTaker,
                    FindGoalkeeper(sim, Side.Away)
                );
                if (homeScored)
                    homePen++;
                homeIdx++;

                // Away 킥
                var awayTaker = awayQueue.Count > 0 ? awayQueue[awayIdx % awayQueue.Count] : null;
                bool awayScored = SimulatePenaltyKick(
                    sim,
                    Side.Away,
                    awayTaker,
                    FindGoalkeeper(sim, Side.Home)
                );
                if (awayScored)
                    awayPen++;
                awayIdx++;

                round++;

                if (isRegular)
                {
                    // 조기 종료: 남은 라운드 기준으로 한 쪽이 수학적으로 따라잡을 수 없으면 종료
                    int remaining = sim.balance.penaltyShootoutRounds - round;
                    if (homePen > awayPen + remaining || awayPen > homePen + remaining)
                        finished = true;
                    else if (round >= sim.balance.penaltyShootoutRounds)
                        finished = homePen != awayPen; // 5라운드 후 동점이면 sudden death 진입
                }
                else
                {
                    // sudden death: 이번 라운드에서 승부 갈렸으면 종료
                    finished = homePen != awayPen;
                }
            }

            return (homePen, awayPen);
        }

        private static bool SimulatePenaltyKick(SimState sim, Side att, Player taker, Player gk)
        {
            if (taker == null)
                return sim.rng.NextDouble() < sim.balance.penaltyShootoutConversionBase;

            double gkRating = gk != null ? GkRating(gk) : 40.0;
            double conv = Clamp(
                sim.balance.penaltyShootoutConversionBase
                    + (taker.stats.technical.penaltyTaking - gkRating)
                        / sim.balance.penaltyShootoutDivisor,
                0.5,
                0.95
            );
            bool scored = sim.rng.NextDouble() < conv;

            if (sim.collectEvents)
                EmitEvent(
                    sim,
                    MatchEventType.PenaltyShootoutKick,
                    att,
                    taker.id,
                    0,
                    scored ? "match_penalty_shootout_goal_fmt" : "match_penalty_shootout_miss_fmt",
                    MakeArgs(sim, taker.id, 0)
                );

            return scored;
        }

        private static List<Player> BuildShootoutQueue(SimState sim, Side s)
        {
            var xi = XIof(sim, s);
            // GK 제외, sentOff 제외, penaltyTaking 내림차순
            return xi.Where(id => !sim.sentOff.Contains(id))
                .Select(id => sim.gameState.GetPlayer(id))
                .Where(p => p != null && p.info.primaryPosition != Position.GK)
                .OrderByDescending(p => p.stats.technical.penaltyTaking)
                .ToList();
        }

        // ── I.10: 세트피스 해결 ───────────────────────────────────────

        // setPieceTakers 우선, 미지정 시 stat 최상위 폴백.
        private static Player FindSetPieceTaker(
            SimState sim,
            Side att,
            System.Func<Player, int> statSelector
        )
        {
            var club = att == Side.Home ? sim.homeClub : sim.awayClub;
            var xi = att == Side.Home ? sim.homeXI : sim.awayXI;
            if (club?.tactic?.setPieceTakers != null)
            {
                foreach (var pid in club.tactic.setPieceTakers)
                {
                    if (!xi.Contains(pid))
                        continue;
                    var p = sim.gameState.GetPlayer(pid);
                    if (p != null)
                        return p;
                }
            }
            return xi.Select(id => sim.gameState.GetPlayer(id))
                .Where(p => p != null)
                .OrderByDescending(statSelector)
                .FirstOrDefault();
        }

        // Corner: taker.corners + target.heading×jumpingReach → 헤더 슛.
        private static void ResolveCorner(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var taker = FindSetPieceTaker(sim, att, p => p.stats.technical.corners);
            var xi = att == Side.Home ? sim.homeXI : sim.awayXI;
            var target = xi.Select(id => sim.gameState.GetPlayer(id))
                .Where(p => p != null)
                .OrderByDescending(p => p.stats.technical.heading * p.stats.physical.jumpingReach)
                .FirstOrDefault();
            var gk = FindGoalkeeper(sim, def);

            if (taker == null || target == null)
            {
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            if (sim.collectEvents)
                EmitEvent(
                    sim,
                    MatchEventType.Corner,
                    att,
                    taker.id,
                    0,
                    "match_corner_fmt",
                    MakeArgs(sim, taker.id, 0)
                );

            double headingScore =
                target.stats.technical.heading * target.stats.physical.jumpingReach / 100.0;
            double accuracy = Clamp(
                sim.balance.cornerConversionBase
                    + (taker.stats.technical.corners + headingScore)
                        / sim.balance.cornerHeadingDivisor,
                0.05,
                0.40
            );

            if (sim.rng.NextDouble() > accuracy)
            {
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            sim.stats[target.id].shots++;
            sim.stats[target.id].shotsOnTarget++;
            double gkRating = gk != null ? Eff(GkRating(gk), def, sim, gk) : 40.0;
            double conversion = Clamp(
                sim.balance.goalConversionBase
                    + (headingScore - gkRating) / sim.balance.goalConversionDivisor,
                0.10,
                0.60
            );

            if (sim.rng.NextDouble() < conversion)
            {
                if (att == Side.Home)
                    sim.homeScore++;
                else
                    sim.awayScore++;
                sim.stats[target.id].goals++;
                if (sim.collectEvents)
                    EmitEvent(
                        sim,
                        MatchEventType.Goal,
                        att,
                        target.id,
                        taker.id,
                        "match_goal_fmt",
                        MakeArgs(sim, target.id, taker.id)
                    );
                TurnOver(sim, att, Zone.Midfield); // kickoff
            }
            else
            {
                if (gk != null && sim.stats.ContainsKey(gk.id))
                    sim.stats[gk.id].saves++;
                TurnOver(sim, att, Zone.Midfield);
            }
        }

        // FreeKick: freeKickTaking vs GK (직접 50%) / cross→헤더 (간접 50%).
        private static void ResolveFreeKick(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var taker = FindSetPieceTaker(sim, att, p => p.stats.technical.freeKickTaking);
            var gk = FindGoalkeeper(sim, def);

            if (taker == null)
            {
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            if (sim.rng.NextDouble() < sim.balance.freeKickDirectProb)
            {
                // 직접 슛
                sim.stats[taker.id].shots++;
                double takerRating = Eff(taker.stats.technical.freeKickTaking, att, sim, taker);
                double accuracy = Clamp(
                    sim.balance.freeKickConversionBase
                        + takerRating / sim.balance.freeKickDirectDivisor,
                    0.05,
                    0.35
                );
                if (sim.rng.NextDouble() > accuracy)
                {
                    TurnOver(sim, att, Zone.Midfield);
                    return;
                }
                sim.stats[taker.id].shotsOnTarget++;
                double gkRating = gk != null ? Eff(GkRating(gk), def, sim, gk) : 40.0;
                double conversion = Clamp(
                    sim.balance.goalConversionBase
                        + (takerRating - gkRating) / sim.balance.goalConversionDivisor,
                    0.10,
                    0.60
                );
                if (sim.rng.NextDouble() < conversion)
                {
                    if (att == Side.Home)
                        sim.homeScore++;
                    else
                        sim.awayScore++;
                    sim.stats[taker.id].goals++;
                    if (sim.collectEvents)
                        EmitEvent(
                            sim,
                            MatchEventType.Goal,
                            att,
                            taker.id,
                            0,
                            "match_goal_fmt",
                            MakeArgs(sim, taker.id, 0)
                        );
                    TurnOver(sim, att, Zone.Midfield);
                }
                else
                {
                    if (gk != null && sim.stats.ContainsKey(gk.id))
                        sim.stats[gk.id].saves++;
                    TurnOver(sim, att, Zone.Midfield);
                }
            }
            else
            {
                // 간접 — 크로스 → 헤더 (corner 로직 재활용, taker stat = freeKickTaking)
                var xi = att == Side.Home ? sim.homeXI : sim.awayXI;
                var target = xi.Select(id => sim.gameState.GetPlayer(id))
                    .Where(p => p != null)
                    .OrderByDescending(p =>
                        p.stats.technical.heading * p.stats.physical.jumpingReach
                    )
                    .FirstOrDefault();
                if (target == null)
                {
                    TurnOver(sim, att, Zone.Midfield);
                    return;
                }
                double headingScore =
                    target.stats.technical.heading * target.stats.physical.jumpingReach / 100.0;
                double accuracy = Clamp(
                    sim.balance.cornerConversionBase
                        + (taker.stats.technical.freeKickTaking + headingScore)
                            / sim.balance.cornerHeadingDivisor,
                    0.05,
                    0.35
                );
                if (sim.rng.NextDouble() > accuracy)
                {
                    TurnOver(sim, att, Zone.Midfield);
                    return;
                }
                sim.stats[target.id].shots++;
                sim.stats[target.id].shotsOnTarget++;
                double gkRating2 = gk != null ? Eff(GkRating(gk), def, sim, gk) : 40.0;
                double conversion2 = Clamp(
                    sim.balance.goalConversionBase
                        + (headingScore - gkRating2) / sim.balance.goalConversionDivisor,
                    0.10,
                    0.60
                );
                if (sim.rng.NextDouble() < conversion2)
                {
                    if (att == Side.Home)
                        sim.homeScore++;
                    else
                        sim.awayScore++;
                    sim.stats[target.id].goals++;
                    if (sim.collectEvents)
                        EmitEvent(
                            sim,
                            MatchEventType.Goal,
                            att,
                            target.id,
                            taker.id,
                            "match_goal_fmt",
                            MakeArgs(sim, target.id, taker.id)
                        );
                    TurnOver(sim, att, Zone.Midfield);
                }
                else
                {
                    if (gk != null && sim.stats.ContainsKey(gk.id))
                        sim.stats[gk.id].saves++;
                    TurnOver(sim, att, Zone.Midfield);
                }
            }
        }

        // LongThrow: longThrows + target.heading → box 진입.
        private static void ResolveLongThrow(SimState sim, Side att)
        {
            var taker = FindSetPieceTaker(sim, att, p => p.stats.technical.longThrows);

            if (sim.collectEvents && taker != null)
                EmitEvent(
                    sim,
                    MatchEventType.LongThrow,
                    att,
                    taker.id,
                    0,
                    "match_long_throw_fmt",
                    MakeArgs(sim, taker.id, 0)
                );

            double boxChance = Clamp(
                sim.balance.longThrowBoxChance
                    + (taker != null ? (taker.stats.technical.longThrows - 50) / 200.0 : 0),
                0.20,
                0.80
            );

            if (sim.rng.NextDouble() < boxChance)
                sim.ballZone = AttackingBox(att);
            else
                TurnOver(sim, att, DefensiveThird(att));
        }

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
