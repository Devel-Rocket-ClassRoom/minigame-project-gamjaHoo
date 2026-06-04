// MatchSimulator.cs
// algorithms.md V0.5-2 Match Simulation V0.5 — 5-Zone Markov (Stage I.1' 상태 머신 + I.2' zone resolution).
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

        // V1.0-1 — 찬스 유형 (xG 찬스-퀄리티 레이어, #474). 박스 진입 경로로 결정.
        private enum ChanceType
        {
            ClearChance, // 스루패스 1:1 결정적
            OpenPlay, // 드리블 박스 진입 (기본)
            Header, // 크로스/코너/FK간접
            LongShot, // box 밖 중거리
            DirectFreeKick, // 직접 FK
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

            // V1.0-1 — 다음 ResolveShot 이 소비할 찬스 유형 (박스 진입 시 설정).
            public ChanceType homePendingChance = ChanceType.OpenPlay;
            public ChanceType awayPendingChance = ChanceType.OpenPlay;

            // V1.0 — AA.2/AA.4 선당김. ballZone 점유 누적 [HomeBox..AwayBox].
            public int[] zoneOccupancy = new int[5];

            // V1.0 — AA.5 선당김. 슛별 xG/위치/결과.
            public List<ShotPin> shotMap = new List<ShotPin>();

            // V1.0 G.3/G.4 — 팀 시너지×포메이션 매치업 곱셈 보정 (매치 시작 1회).
            public double homeTeamMod = 1.0;
            public double awayTeamMod = 1.0;
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

            // 1단계: 시드 고정 (forward simulation — 결정성은 시드에서만, #17 V0.5)
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

            // G.3/G.4 — 시너지(활성 strengthBonus product) × 포메이션 매치업 보너스 (매치 시작 1회)
            sim.homeTeamMod = ComputeTeamMod(state, home, away);
            sim.awayTeamMod = ComputeTeamMod(state, away, home);

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
                shotMap = sim.shotMap,
                zoneOccupancy = sim.zoneOccupancy,
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

            // V1.0 — zone 점유 누적 (히트맵 AA.4). Zone enum 순서 = int[5] 인덱스.
            sim.zoneOccupancy[(int)sim.ballZone]++;

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

        // 공격 third — 드리블 돌파. 성공 → Box(찬스 유형 결정) / 중거리슛 / 실패 → Tackle·Clearance + Corner + 점유 전환.
        private static void ResolveAttackingThird(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var attacker = SnapPlayer(sim, att, Line.AT, TacticImpact.EventKeyPass);
            var defender = SnapPlayer(sim, def, Line.DF, TacticImpact.EventTackle);
            if (attacker == null)
                return;

            // V1.0 G.1 — 중거리 슛 (box 미진입 즉시 슛). LongShot = 낮은 xG.
            // 멘탈리티 가중 (공격적 팀이 중거리도 더 시도 — 멘탈리티 슛빈도 격차 반영).
            if (sim.rng.NextDouble() < sim.balance.longShotProb * MentalityShotMult(sim, att))
            {
                ResolveShotXg(sim, att, attacker, FindGoalkeeper(sim, def), ChanceType.LongShot);
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

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
                // V1.0-1 — 찬스 유형 결정. 창의적 패서일수록 결정적 스루패스(ClearChance)↑.
                double creative =
                    (
                        attacker.stats.mental.vision
                        + attacker.stats.technical.passing
                        + attacker.stats.mental.flair
                    ) / 3.0;
                double clearProb = Clamp(
                    sim.balance.clearChanceBase
                        + (creative - 50.0) / sim.balance.clearChanceDivisor,
                    sim.balance.clearChanceProbMin,
                    sim.balance.clearChanceProbMax
                );
                bool isClear = sim.rng.NextDouble() < clearProb;

                // V1.0 G.1 — 결정적 스루패스는 오프사이드로 무산될 수 있음.
                if (isClear && sim.rng.NextDouble() < sim.balance.offsideProb)
                {
                    if (sim.collectEvents)
                        EmitEvent(
                            sim,
                            MatchEventType.Offside,
                            att,
                            attacker.id,
                            0,
                            "match_offside_fmt",
                            MakeArgs(sim, attacker.id, 0)
                        );
                    TurnOver(sim, att, DefensiveThird(att));
                    return;
                }

                // 드리블/스루패스 성공 → box 진입. 직전 패스 = keyPass(assist 추적).
                SetPendingAssist(sim, att, attacker.id);
                sim.stats[attacker.id].keyPasses++;
                SetPendingChance(sim, att, isClear ? ChanceType.ClearChance : ChanceType.OpenPlay);
                sim.ballZone = AttackingBox(att);

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
                    else
                    {
                        // V1.0 — Clearance 통계 (평점 수비 기여)
                        sim.stats[defender.id].clearances++;
                    }
                }
                if (foulOccurred)
                {
                    ResolveFreeKick(sim, att); // I.10 — 위험지역 FK
                    return;
                }
                // I.10 — Corner / LongThrow / TurnOver
                double roll = sim.rng.NextDouble();
                if (roll < sim.balance.zoneCornerChance)
                {
                    ResolveCorner(sim, att);
                    return;
                }
                if (roll < sim.balance.zoneCornerChance + sim.balance.longThrowChance)
                {
                    ResolveLongThrow(sim, att);
                    return;
                }
                // V1.0 G.1 — 스로인 (flavor, 메커닉 영향 X). rng 는 항상 소비 (collectEvents 결정성 보존).
                bool throwIn = sim.rng.NextDouble() < sim.balance.throwInChance;
                if (throwIn && sim.collectEvents)
                    EmitEvent(sim, MatchEventType.ThrowIn, att, 0, 0, "match_throw_in_fmt", null);
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

            // V1.0-1 — 박스 진입 시 설정된 찬스 유형 소비 (기본 OpenPlay).
            ChanceType type = ConsumePendingChance(sim, att);
            ResolveShotXg(sim, att, shooter, gk, type);
            TurnOver(sim, att, Zone.Midfield);
        }

        // ── V1.0-1: xG 찬스-퀄리티 슛 해결 (모든 슛 경로 공통, #474) ──────────
        // 기록 xG = 찬스 품질(situation, 슈터 무관). 실제 골 = xG × finishMod × gkMod.
        // Header 시 shooter = 헤더 선수. xgMultiplier = 세트피스 딜리버리 보정 (코너/FK 크로스).
        private static void ResolveShotXg(
            SimState sim,
            Side att,
            Player shooter,
            Player gk,
            ChanceType type,
            double xgMultiplier = 1.0
        )
        {
            if (shooter == null)
                return;
            Side def = Opposite(att);
            var b = sim.balance;

            // (1) 찬스 품질 xG (슈터 실력 무관)
            double xg = BaseXg(b, type) * xgMultiplier;
            if (type == ChanceType.Header)
                xg *= HeaderMod(b, shooter); // G.2 (1) 키×헤딩×점프
            // G.3/G.4 — 공격팀 시너지·매치업으로 찬스 품질↑, 수비팀 mod로 ↓ (비율, clamp bounded).
            // 점유/box 진입이 아닌 xG 에만 적용 → 단조·비폭주 (blanket Eff 곱셈의 degenerate 회피).
            double attMod = att == Side.Home ? sim.homeTeamMod : sim.awayTeamMod;
            double defMod = att == Side.Home ? sim.awayTeamMod : sim.homeTeamMod;
            xg *= attMod / defMod;
            xg = Clamp(xg, b.xgFloor, b.xgCeil);

            // G.2 (4) — 약발: 주발 아닌 발 슈팅 시 finishing 감점 (Header 제외)
            bool footMismatch = false;
            if (
                type != ChanceType.Header
                && shooter.physical != null
                && shooter.physical.preferredFoot != Foot.Both
            )
            {
                int wf = Math.Min(5, Math.Max(1, shooter.physical.weakFootAbility));
                if (sim.rng.NextDouble() < (5 - wf) / 5.0 * 0.5)
                    footMismatch = true;
            }

            sim.stats[shooter.id].shots++;
            sim.stats[shooter.id].xg += (float)xg;

            // (2) 실제 골 확률 = xG × 슈터 finishing 보정 × GK 보정
            double finishEff =
                Eff(FinishingEff(shooter, type, footMismatch, b), att, sim, shooter)
                * MentalityShotMult(sim, att);
            double finishMod = Clamp(
                1.0 + (finishEff - 50.0) / b.finishingXgDivisor,
                b.finishModMin,
                b.finishModMax
            );
            double gkRating = gk != null ? Eff(GkRating(gk), def, sim, gk) : 40.0;
            double gkMod = Clamp(
                1.0 - (gkRating - 50.0) / b.gkXgDivisor,
                b.gkModFloor,
                b.gkModCeil
            );
            double conversion = Clamp(xg * finishMod * gkMod, b.conversionFloor, b.conversionCeil);

            // 결정성: outcome 과 무관하게 rng 2회 소비 (conversion / accuracy).
            // → xg(시너지·매치업) 변경 시 rng 스트림 불변 → 페어드 비교 정상 + 결정성 강화.
            double convRoll = sim.rng.NextDouble();
            double accRoll = sim.rng.NextDouble();
            double accuracy = Clamp(
                b.shotAccuracyBase + (finishEff - 50.0) / b.shotAccuracyDivisor,
                0.15,
                0.85
            );

            ShotOutcome outcome;
            if (convRoll < conversion)
            {
                // GOAL
                outcome = ShotOutcome.Goal;
                sim.stats[shooter.id].shotsOnTarget++;
                if (att == Side.Home)
                    sim.homeScore++;
                else
                    sim.awayScore++;
                sim.stats[shooter.id].goals++;

                int assister = GetPendingAssist(sim, att);
                if (assister != -1 && assister != shooter.id && sim.stats.ContainsKey(assister))
                    sim.stats[assister].assists++;
                ClearPendingAssist(sim, att);

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
            else
            {
                // 빅찬스 미스 (평점 #74)
                if (xg >= b.bigChanceThreshold)
                    sim.stats[shooter.id].bigChancesMissed++;

                if (accRoll < accuracy)
                {
                    // on-target → GK 선방
                    outcome = ShotOutcome.Saved;
                    sim.stats[shooter.id].shotsOnTarget++;
                    if (gk != null && sim.stats.ContainsKey(gk.id))
                        sim.stats[gk.id].saves++;
                    if (sim.collectEvents)
                    {
                        // G.1 — 헤더 선방 = GK 펀칭
                        var et =
                            type == ChanceType.Header
                                ? MatchEventType.KeeperPunch
                                : MatchEventType.ShotSaved;
                        string key =
                            type == ChanceType.Header
                                ? "match_keeper_punch_fmt"
                                : "match_shot_saved_fmt";
                        EmitEvent(
                            sim,
                            et,
                            att,
                            shooter.id,
                            gk?.id ?? 0,
                            key,
                            MakeArgs(sim, gk?.id ?? 0, shooter.id)
                        );
                    }
                }
                else
                {
                    // off-target / block
                    outcome = ShotOutcome.Off;
                    if (sim.collectEvents)
                    {
                        var et =
                            type == ChanceType.LongShot
                                ? MatchEventType.LongShot
                                : MatchEventType.ShotOffTarget;
                        string key =
                            type == ChanceType.LongShot
                                ? "match_long_shot_fmt"
                                : "match_shot_off_target_fmt";
                        EmitEvent(sim, et, att, shooter.id, 0, key, MakeArgs(sim, shooter.id, 0));
                    }
                }
                ClearPendingAssist(sim, att);
            }

            RecordShotPin(sim, att, type, xg, outcome);
        }

        // chanceType별 기본 xG (찬스 품질).
        private static double BaseXg(GameBalanceSO b, ChanceType t)
        {
            switch (t)
            {
                case ChanceType.ClearChance:
                    return b.xgClearChance;
                case ChanceType.OpenPlay:
                    return b.xgOpenPlay;
                case ChanceType.Header:
                    return b.xgHeader;
                case ChanceType.LongShot:
                    return b.xgLongShot;
                case ChanceType.DirectFreeKick:
                    return b.xgDirectFreeKick;
                default:
                    return b.xgOpenPlay;
            }
        }

        // G.2 (1) — 헤더 보정. 평균(heading50/jump50/height180)≈1.0, 큰·잘하는 선수 ↑. clamp 0.5~1.8.
        private static double HeaderMod(GameBalanceSO b, Player p)
        {
            double h = p.physical?.height ?? 180;
            double mod =
                (p.stats.technical.heading / 50.0)
                * (p.stats.physical.jumpingReach / 50.0)
                * (h / 180.0)
                / b.headerModNormalizer;
            return Clamp(mod, 0.5, 1.8);
        }

        // 슈터 마무리 능력 (찬스 유형별 stat). footMismatch 시 약발 감점.
        private static double FinishingEff(
            Player p,
            ChanceType type,
            bool footMismatch,
            GameBalanceSO b
        )
        {
            double raw;
            if (type == ChanceType.Header)
                raw = (p.stats.mental.composure + p.stats.mental.decisions) / 2.0;
            else if (type == ChanceType.DirectFreeKick)
                raw = (p.stats.technical.freeKickTaking + p.stats.mental.composure) / 2.0;
            else
                raw =
                    (
                        p.stats.technical.finishing
                        + p.stats.mental.composure
                        + p.stats.mental.decisions
                    ) / 3.0;
            if (footMismatch)
                raw *= b.footMismatchPenalty;
            return raw;
        }

        // 슛별 위치(x,y) + xG + 결과 기록 (AA.5 슛맵).
        private static void RecordShotPin(
            SimState sim,
            Side att,
            ChanceType type,
            double xg,
            ShotOutcome outcome
        )
        {
            float x,
                range;
            switch (type)
            {
                case ChanceType.ClearChance:
                    x = 0.90f;
                    range = 0.08f;
                    break;
                case ChanceType.Header:
                    x = 0.94f;
                    range = 0.10f;
                    break;
                case ChanceType.LongShot:
                    x = 0.72f;
                    range = 0.22f;
                    break;
                case ChanceType.DirectFreeKick:
                    x = 0.75f;
                    range = 0.20f;
                    break;
                default: // OpenPlay
                    x = 0.84f;
                    range = 0.18f;
                    break;
            }
            float y = 0.5f + (float)((sim.rng.NextDouble() - 0.5) * 2.0 * range);
            sim.shotMap.Add(
                new ShotPin
                {
                    side = (int)att,
                    x = x,
                    y = Clamp01(y),
                    xg = (float)xg,
                    outcome = outcome,
                }
            );
        }

        private static void SetPendingChance(SimState sim, Side att, ChanceType t)
        {
            if (att == Side.Home)
                sim.homePendingChance = t;
            else
                sim.awayPendingChance = t;
        }

        // 박스 진입 시 설정된 찬스 유형 소비 후 기본값(OpenPlay) 리셋.
        private static ChanceType ConsumePendingChance(SimState sim, Side att)
        {
            ChanceType t = att == Side.Home ? sim.homePendingChance : sim.awayPendingChance;
            if (att == Side.Home)
                sim.homePendingChance = ChanceType.OpenPlay;
            else
                sim.awayPendingChance = ChanceType.OpenPlay;
            return t;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

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

        // 인-매치 페널티 — penaltyTaking vs GK. 지정 PK 키커 우선, 폴백 = 파울 얻은 선수.
        private static void ResolvePenalty(SimState sim, Side att, Player taker, Player gk)
        {
            var designatedKicker = FindSetPieceTaker(
                sim,
                att,
                p => p.stats.technical.penaltyTaking,
                0
            );
            if (designatedKicker != null)
                taker = designatedKicker;
            if (taker == null)
                return;
            sim.stats[taker.id].shots++;
            sim.stats[taker.id].shotsOnTarget++;

            double gkRating = gk != null ? GkRating(gk) : 40.0;
            // G.2 (4) — PK 는 주발로 차므로 약발 미스매치 없음 (foot match 항상 1.0).
            double conv = Clamp(
                sim.balance.penaltyConversion
                    + (taker.stats.technical.penaltyTaking - gkRating) / 300.0,
                0.5,
                0.95
            );
            sim.stats[taker.id].xg += (float)conv; // PK = 고 xG 슛
            bool scored = sim.rng.NextDouble() < conv;
            if (scored)
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

            // V1.0 — 슛맵 핀 (PK = 박스 중앙 근거리)
            sim.shotMap.Add(
                new ShotPin
                {
                    side = (int)att,
                    x = 0.88f,
                    y = 0.5f,
                    xg = (float)conv,
                    outcome = scored ? ShotOutcome.Goal : ShotOutcome.Saved,
                }
            );
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

        // 이벤트 누적 통계 → rating (base 6.5, clamp 1.0~10.0). pressureHandling 빅매치 가산 = V1.0.
        private static void ComputeRatings(SimState sim)
        {
            var homeSet = new HashSet<int>(sim.homeXI);
            foreach (var stat in sim.stats.Values)
            {
                var p = sim.gameState.GetPlayer(stat.playerId);
                bool isHome = homeSet.Contains(stat.playerId);
                Line line = p != null ? StartingSquadGacha.LineOf(p.info.primaryPosition) : Line.MF;
                int teamScore = isHome ? sim.homeScore : sim.awayScore;
                int oppScore = isHome ? sim.awayScore : sim.homeScore;
                stat.rating = ComputePlayerRating(stat, line, teamScore, oppScore, sim.balance);
            }
        }

        // V1.0 평점 재설계 (#70, FM 정합) — 순수 함수 (테스트 직접 호출). base 6.5, clamp 1~10.
        // 포지션이 평점을 만든다: 공격 기여(전원) + 수비 기여(수비수 누적) + 패스성공률 + 무실점/실점(GK·DF) + xG 보정.
        public static float ComputePlayerRating(
            PlayerMatchStat stat,
            Line line,
            int teamScore,
            int oppScore,
            GameBalanceSO b
        )
        {
            double r = b.ratingBase;

            // ── 공격 기여 (전 포지션 — 골/어시는 누구든 가치) ──
            r += stat.goals * b.ratingGoalBonus;
            r += stat.assists * b.ratingAssistBonus;
            r += stat.shotsOnTarget * b.ratingShotOnTargetBonus;
            r += stat.keyPasses * b.ratingKeyPassBonus;
            // V1.0 xG 보정 (#74) — clinical finish 가산 / 낭비·빅찬스 미스 감점
            r += (stat.goals - stat.xg) * b.ratingXgPerformanceCoeff;
            r += stat.bigChancesMissed * b.ratingBigChanceMissPenalty;

            // ── 수비 기여 (전 포지션 — 수비수가 자연히 더 누적) ──
            r += (stat.tackles + stat.interceptions) * b.ratingDefActionBonus;
            r += stat.clearances * b.ratingClearanceBonus;

            // ── 패스/점유 기여 (시도수 ≥ 임계 시 성공률 티어) ──
            if (stat.passes >= b.ratingPassMinAttempts)
            {
                double pct = (double)stat.passesCompleted / stat.passes;
                if (pct >= 0.90)
                    r += b.ratingPassHighBonus;
                else if (pct >= 0.80)
                    r += b.ratingPassMidBonus;
                else if (pct < 0.70)
                    r += b.ratingPassLowPenalty;
            }

            // ── 규율 ──
            r += stat.yellowCards * b.ratingYellowPenalty;
            r += stat.redCards * b.ratingRedPenalty;

            // ── 무실점 / 실점 (GK + DF 라인 한정, V1.0 — 수비 책임 공유) ──
            if (line == Line.GK)
            {
                r += stat.saves * b.ratingSaveBonus;
                r += oppScore == 0 ? b.ratingCleanSheetBonus : oppScore * b.ratingConcededPenalty;
            }
            else if (line == Line.DF)
            {
                r +=
                    oppScore == 0
                        ? b.ratingCleanSheetBonusDef
                        : oppScore * b.ratingConcededPenaltyDef;
            }

            // ── 팀 승/패 전원 가감 ──
            if (teamScore > oppScore)
                r += b.ratingWinBonus;
            else if (teamScore < oppScore)
                r += b.ratingLossPenalty;

            r = Math.Round(r, 1);
            return (float)Clamp(r, b.ratingMin, b.ratingMax);
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

        // ── 헬퍼: stat 조합 (49 stat zone별 매핑, algorithms.md V0.5-2) ──

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

        // G.2 (2)(3) — agility 키 역상관 (작을수록 ↑) / pace + 약발 미세 보정.
        private static double AttackingThirdAtt(Player p)
        {
            double height = p.physical?.height ?? 180;
            double agilityEff = p.stats.physical.agility * (180.0 / Math.Max(height, 165));
            double sprintEff = p.stats.physical.pace + (p.physical?.weakFootAbility ?? 3) * 0.5;
            return (p.stats.technical.dribbling + sprintEff + agilityEff + p.stats.mental.composure)
                / 4.0;
        }

        private static double AttackingThirdDef(Player p) =>
            (
                p.stats.technical.marking
                + p.stats.technical.tackling
                + p.stats.mental.positioning
                + p.stats.technical.heading
            ) / 4.0;

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

        // G.3/G.4 — 팀 strength 보정 = 활성 시너지 strengthBonus product × 포메이션 매치업 homeBonus.
        // 시너지/매치업 데이터 미등록 또는 tactic null → 1.0 (무영향, 회귀 없음).
        private static double ComputeTeamMod(GameState state, Club club, Club opp)
        {
            double mod = 1.0;
            foreach (var syn in TacticImpact.ComputeSynergies(club?.tactic, state))
                mod *= syn.strengthBonus;
            var matchup = GameDatabase.FormationMatchup;
            if (matchup != null && club?.tactic != null && opp?.tactic != null)
                mod *= matchup.Get(club.tactic.formationId, opp.tactic.formationId);
            return mod;
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

        // setPieceTakers[typeIndex] 우선, 미지정(-1) 또는 비출전 시 stat 최상위 폴백.
        // typeIndex: 0=Penalty, 1=FreeKick, 2=Corner, 3=ThrowIn (LineupController 상수와 동기화)
        private static Player FindSetPieceTaker(
            SimState sim,
            Side att,
            System.Func<Player, int> statSelector,
            int typeIndex = -1
        )
        {
            var club = att == Side.Home ? sim.homeClub : sim.awayClub;
            var xi = att == Side.Home ? sim.homeXI : sim.awayXI;
            if (
                typeIndex >= 0
                && club?.tactic?.setPieceTakers != null
                && typeIndex < club.tactic.setPieceTakers.Count
            )
            {
                var pid = club.tactic.setPieceTakers[typeIndex];
                if (pid >= 0 && xi.Contains(pid))
                {
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

        // Corner (V1.0 xG): 딜리버리(corners) × 헤더 표적 → Header xG. cornerToBox 게이트로 슛 과다 방지.
        private static void ResolveCorner(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var taker = FindSetPieceTaker(sim, att, p => p.stats.technical.corners, 2);
            var target = BestHeaderTarget(sim, att);
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

            // 코너가 헤더 슛으로 연결되는 비율 (나머지는 클리어/방어).
            if (sim.rng.NextDouble() < sim.balance.zoneCornerToBoxChance)
            {
                double delivery = Clamp(
                    0.7 + (taker.stats.technical.corners - 50.0) / 200.0,
                    0.6,
                    1.3
                );
                SetPendingAssist(sim, att, taker.id); // 헤더 골 시 코너 키커 어시
                ResolveShotXg(sim, att, target, gk, ChanceType.Header, delivery);
            }
            TurnOver(sim, att, Zone.Midfield);
        }

        // 출전 중 최고 공중 표적 (heading × jumpingReach).
        private static Player BestHeaderTarget(SimState sim, Side att)
        {
            var xi = att == Side.Home ? sim.homeXI : sim.awayXI;
            return xi.Select(id => sim.gameState.GetPlayer(id))
                .Where(p => p != null && !sim.sentOff.Contains(p.id))
                .OrderByDescending(p => p.stats.technical.heading * p.stats.physical.jumpingReach)
                .FirstOrDefault();
        }

        // FreeKick (V1.0 xG): 직접(DirectFreeKick xG, 약발 적용) / 간접 크로스→헤더(Header xG).
        // FreeKick 이벤트는 MaybeFoul 에서 이미 발행됨 (중복 방지로 여기선 미발행).
        private static void ResolveFreeKick(SimState sim, Side att)
        {
            Side def = Opposite(att);
            var taker = FindSetPieceTaker(sim, att, p => p.stats.technical.freeKickTaking, 1);
            var gk = FindGoalkeeper(sim, def);

            if (taker == null)
            {
                TurnOver(sim, att, Zone.Midfield);
                return;
            }

            if (sim.rng.NextDouble() < sim.balance.freeKickDirectProb)
            {
                // 직접 슛
                ResolveShotXg(sim, att, taker, gk, ChanceType.DirectFreeKick);
            }
            else
            {
                // 간접 — 크로스 → 헤더 (도달 게이트)
                var target = BestHeaderTarget(sim, att);
                if (target != null && sim.rng.NextDouble() < sim.balance.zoneCornerToBoxChance)
                {
                    double delivery = Clamp(
                        0.7 + (taker.stats.technical.freeKickTaking - 50.0) / 200.0,
                        0.6,
                        1.3
                    );
                    SetPendingAssist(sim, att, taker.id);
                    ResolveShotXg(sim, att, target, gk, ChanceType.Header, delivery);
                }
            }
            TurnOver(sim, att, Zone.Midfield);
        }

        // LongThrow: longThrows → box 진입 (헤더 찬스). 진입 시 다음 슛 = Header.
        private static void ResolveLongThrow(SimState sim, Side att)
        {
            var taker = FindSetPieceTaker(sim, att, p => p.stats.technical.longThrows, 3);

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
            {
                SetPendingChance(sim, att, ChanceType.Header); // 롱스로 → box 공중볼
                sim.ballZone = AttackingBox(att);
            }
            else
                TurnOver(sim, att, DefensiveThird(att));
        }

        // ── 헬퍼: starting11 / stats ──────────────────────────────────

        private static List<int> SelectStartingEleven(Club club, GameState state)
        {
            if (HasLineup(club.tactic))
            {
                var result = new List<int>(11);
                var used = new HashSet<int>();
                foreach (var slot in club.tactic.slots)
                {
                    if (result.Count >= 11)
                        break;
                    var pid = slot.assignedPlayerId;
                    if (pid < 0 || used.Contains(pid))
                        continue;
                    var p = state.GetPlayer(pid);
                    if (
                        p == null
                        || p.state.injury.injuryTypeId != -1
                        || p.state.suspendedMatches > 0
                    )
                        continue;
                    result.Add(pid);
                    used.Add(pid);
                }
                // 부상/정지로 빈 슬롯은 스쿼드 CA 최상위로 채움
                if (result.Count < 11)
                {
                    var fallback = club
                        .seniorSquadIds.Where(id => !used.Contains(id))
                        .Select(id => state.GetPlayer(id))
                        .Where(p =>
                            p != null
                            && p.state.injury.injuryTypeId == -1
                            && p.state.suspendedMatches <= 0
                        )
                        .OrderByDescending(p => p.currentAbility)
                        .Take(11 - result.Count);
                    foreach (var p in fallback)
                        result.Add(p.id);
                }
                return result;
            }

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
