// MatchSimulatorTests.cs
// DoD: algorithms.md V1.0-2 Test Scenarios — Stage I.1' (5-zone 상태 머신) + I.2' (zone resolution).
// 후속: Foul/Card/Injury = I.3 / 평점 = I.4 / 텍스트 = I.5 / SubstitutionAI = I.6 / background = I.7 / fatigue·form·morale = I.8 / 연장 = I.11.

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class MatchSimulatorTests
    {
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
        }

        // ── T1. 결정성 ────────────────────────────────────────────────

        [Test]
        public void T1_Determinism_SameSeedSameResult()
        {
            var (s1, m1) = BuildState(seed: 42, matchId: 1);
            var (s2, m2) = BuildState(seed: 42, matchId: 1);

            var r1 = MatchSimulator.Simulate(m1, s1, _balance);
            var r2 = MatchSimulator.Simulate(m2, s2, _balance);

            Assert.AreEqual(r1.homeScore, r2.homeScore, "T1: homeScore 결정적");
            Assert.AreEqual(r1.awayScore, r2.awayScore, "T1: awayScore 결정적");
            Assert.AreEqual(
                r1.homePossessionPct,
                r2.homePossessionPct,
                "T1: 점유율 결정적"
            );
            Assert.AreEqual(r1.playerStats.Count, r2.playerStats.Count);
            for (int i = 0; i < r1.playerStats.Count; i++)
            {
                Assert.AreEqual(r1.playerStats[i].playerId, r2.playerStats[i].playerId);
                Assert.AreEqual(r1.playerStats[i].goals, r2.playerStats[i].goals);
                Assert.AreEqual(r1.playerStats[i].shots, r2.playerStats[i].shots);
                Assert.AreEqual(r1.playerStats[i].passes, r2.playerStats[i].passes);
            }
        }

        // ── T2. 인터페이스 호환 ──────────────────────────────────────

        [Test]
        public void T2_InterfaceCompatibility_SignatureUnchanged()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            MatchResult result = MatchSimulator.Simulate(match, state, _balance);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.homeStarting11);
            Assert.IsNotNull(result.awayStarting11);
            Assert.IsNotNull(result.playerStats);
        }

        // ── starting11 자동 선정 ──────────────────────────────────────

        [Test]
        public void StartingEleven_TopByCAExcludingInjured()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            var result = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(11, result.homeStarting11.Count);
            var homeClub = state.GetClub(match.homeClubId);
            var bench = homeClub
                .seniorSquadIds.Where(id => !result.homeStarting11.Contains(id))
                .Select(id => state.GetPlayer(id))
                .ToList();
            int xiMin = result.homeStarting11.Select(id => state.GetPlayer(id).currentAbility).Min();
            int benchMax = bench.Count > 0 ? bench.Max(p => p.currentAbility) : 0;
            Assert.GreaterOrEqual(xiMin, benchMax);
        }

        [Test]
        public void StartingEleven_ExcludesInjuredPlayers()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            var home = state.GetClub(match.homeClubId);
            var top5 = home
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .OrderByDescending(p => p.currentAbility)
                .Take(5)
                .ToList();
            foreach (var p in top5)
                p.state.injury.injuryTypeId = 1;

            var result = MatchSimulator.Simulate(match, state, _balance);
            foreach (var p in top5)
                Assert.IsFalse(result.homeStarting11.Contains(p.id));
            Assert.AreEqual(11, result.homeStarting11.Count);
        }

        [Test]
        public void StartingEleven_ExcludesSuspendedPlayers()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            var home = state.GetClub(match.homeClubId);
            var top5 = home
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .OrderByDescending(p => p.currentAbility)
                .Take(5)
                .ToList();
            foreach (var p in top5)
                p.state.suspendedMatches = 1;

            var result = MatchSimulator.Simulate(match, state, _balance);
            foreach (var p in top5)
                Assert.IsFalse(result.homeStarting11.Contains(p.id));
            Assert.AreEqual(11, result.homeStarting11.Count);
        }

        [Test]
        public void StartingEleven_FewerThan11WhenSquadShort()
        {
            var state = new GameState
            {
                randomSeed = 1,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, "Home");
            var away = NewClub(2, "Away");
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddSquad(state, home, statVal: 50, nextId: nextId, count: 5);
            nextId = AddSquad(state, away, statVal: 50, nextId: nextId, count: 25);
            var match = new Match
            {
                id = 1,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };

            var result = MatchSimulator.Simulate(match, state, _balance);
            Assert.AreEqual(5, result.homeStarting11.Count);
            Assert.AreEqual(11, result.awayStarting11.Count);
        }

        // ── T3. 골 분포 (200 매치 평균) ──────────────────────────────

        [Test]
        public void T3_Goals_DistributionReasonable()
        {
            var seedGen = new System.Random(42);
            int totalGoals = 0;
            const int N = 200;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i + 1);
                var r = MatchSimulator.Simulate(match, state, _balance);
                totalGoals += r.homeScore + r.awayScore;
            }
            double avg = (double)totalGoals / N;
            Assert.Greater(totalGoals, 0, "T3: 골 발생");
            // 5-zone — 균등팀 평균 골수 합리 범위 (튜닝 여지 ±). EPL ~2.7.
            Assert.That(avg, Is.InRange(1.0, 6.0), $"T3: 평균 골수 (실측 {avg:F2}/매치)");
        }

        // ── T4. 점유율 ───────────────────────────────────────────────

        [Test]
        public void T4_Possession_SumsTo100AndBalancedForEqualTeams()
        {
            var seedGen = new System.Random(77);
            double homeSum = 0;
            const int N = 100;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                Assert.That(
                    r.homePossessionPct + r.awayPossessionPct,
                    Is.EqualTo(100f).Within(0.1f),
                    "T4: 점유율 합 = 100"
                );
                homeSum += r.homePossessionPct;
            }
            double homeAvg = homeSum / N;
            // 균등팀 (stat 동일) — home advantage 로 home 약간 우세하나 ~40-60 범위
            Assert.That(
                homeAvg,
                Is.InRange(40.0, 65.0),
                $"T4: 균등팀 home 점유율 ~50 근방 (실측 {homeAvg:F1})"
            );
        }

        // ── T5. 강팀 우세 (stat 차등) ────────────────────────────────

        [Test]
        public void T5_StrongerTeamScoresMore()
        {
            // home stat 75 vs away stat 35 — home 골 우세 검증 (100 매치)
            var seedGen = new System.Random(500);
            int homeGoals = 0,
                awayGoals = 0;
            const int N = 100;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(
                    seed: seedGen.Next(),
                    matchId: i,
                    homeStat: 75,
                    awayStat: 35
                );
                var r = MatchSimulator.Simulate(match, state, _balance);
                homeGoals += r.homeScore;
                awayGoals += r.awayScore;
            }
            Assert.Greater(
                homeGoals,
                awayGoals,
                $"T5: 강팀(home stat 75) 골 > 약팀(away 35). home={homeGoals} away={awayGoals}"
            );
        }

        // ── T6. 슛 분포 ──────────────────────────────────────────────

        [Test]
        public void T6_Shots_DistributionInRange()
        {
            var seedGen = new System.Random(100);
            int totalShots = 0;
            const int N = 100;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                totalShots += r.playerStats.Sum(ps => ps.shots);
            }
            double perTeam = (double)totalShots / N / 2;
            // 5-zone box 도달 빈도 기반 — 넓은 범위 허용 (튜닝 여지).
            Assert.That(
                perTeam,
                Is.InRange(3.0, 30.0),
                $"T6: 슛/팀/매치 (실측 {perTeam:F1})"
            );
        }

        // ── 누적 통계 채워짐 ──────────────────────────────────────────

        [Test]
        public void PlayerMatchStat_AccumulatesCoreFields()
        {
            var seedGen = new System.Random(200);
            int passes = 0,
                tackles = 0,
                keyPasses = 0,
                shotsOnTarget = 0;
            const int N = 50;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                passes += r.playerStats.Sum(ps => ps.passes);
                tackles += r.playerStats.Sum(ps => ps.tackles);
                keyPasses += r.playerStats.Sum(ps => ps.keyPasses);
                shotsOnTarget += r.playerStats.Sum(ps => ps.shotsOnTarget);
            }
            Assert.Greater(passes, 0, "passes > 0");
            Assert.Greater(tackles, 0, "tackles > 0");
            Assert.Greater(keyPasses, 0, "keyPasses > 0");
            Assert.Greater(shotsOnTarget, 0, "shotsOnTarget > 0");
        }

        [Test]
        public void PlayerMatchStat_MinutesPlayedIs90AndRatingZero()
        {
            var (state, match) = BuildState(seed: 5, matchId: 1);
            var r = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(
                r.homeStarting11.Count + r.awayStarting11.Count,
                r.playerStats.Count
            );
            foreach (var ps in r.playerStats)
            {
                Assert.AreEqual(90, ps.minutesPlayed, $"minutesPlayed=90 (id={ps.playerId})");
                Assert.AreEqual(0f, ps.rating, $"rating=0 (I.4) (id={ps.playerId})");
                Assert.AreEqual(0, ps.foulsCommitted, $"foulsCommitted=0 (I.3) (id={ps.playerId})");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private (GameState, Match) BuildState(
            int seed,
            int matchId,
            int homeStat = 50,
            int awayStat = 50
        )
        {
            var state = new GameState
            {
                randomSeed = seed,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, "Home");
            var away = NewClub(2, "Away");
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddSquad(state, home, statVal: homeStat, nextId: nextId, count: 25);
            nextId = AddSquad(state, away, statVal: awayStat, nextId: nextId, count: 25);
            var match = new Match
            {
                id = matchId,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };
            return (state, match);
        }

        private static Club NewClub(int id, string name) =>
            new Club
            {
                id = id,
                name = name,
                reputation = 60,
                facilities = new Facilities { medicalLevel = 1, gymLevel = 1 },
            };

        // 라인 분포 GK 3 / DF 8 / MF 8 / AT 6 (25명) — 5-zone snap 이 라인별 선수 필요.
        private static int AddSquad(
            GameState state,
            Club club,
            int statVal,
            int nextId,
            int count
        )
        {
            var composition = new (Position pos, int n)[]
            {
                (Position.GK, 3),
                (Position.CB, 4),
                (Position.LB, 2),
                (Position.RB, 2),
                (Position.DM, 2),
                (Position.CM, 4),
                (Position.LM, 1),
                (Position.RM, 1),
                (Position.ST, 4),
                (Position.CF, 2),
            };
            int added = 0;
            int idx = 0;
            foreach (var (pos, n) in composition)
            {
                for (int i = 0; i < n && added < count; i++)
                {
                    // CA 약간 다양화 (starting11 정렬 안정성)
                    int ca = 100 + ((idx % 5) - 2);
                    state.AddPlayer(NewPlayer(nextId, pos, ca, statVal));
                    club.seniorSquadIds.Add(nextId);
                    nextId++;
                    added++;
                    idx++;
                }
            }
            return nextId;
        }

        private static Player NewPlayer(int id, Position pos, int ca, int statVal) =>
            new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = ca + 10,
                info = new PersonalInfo
                {
                    primaryPosition = pos,
                    firstName = "F",
                    lastName = "L",
                },
                state = new PlayerState
                {
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    fatigue = 0,
                    morale = 50,
                    form = 50,
                    suspendedMatches = 0,
                },
                stats = NewBalancedStats(statVal),
                hiddenAttrs = new HiddenAttributes { injuryProneness = 50 },
            };

        private static Stats NewBalancedStats(int v)
        {
            var s = new Stats();
            s.technical.ApplyToAll(_ => v);
            s.mental.ApplyToAll(_ => v);
            s.physical.ApplyToAll(_ => v);
            s.gk.ApplyToAll(_ => v);
            return s;
        }
    }
}
