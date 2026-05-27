// MatchSimulatorTests.cs
// DoD: algorithms.md V1.0-2 Test Scenarios — Stage I.1 골격 + I.2 이벤트 종류.
// 후속: T2 부상 누적 = I.3 / T3 카드 누적 = I.3 / T4 SimulateLite = I.7 / T5 평점 = I.4 /
//      T6 Mentality = I.2+J.3 / T7 form/morale/fatigue = I.8 / T8 Role 가중치 = J.2 / T9 텍스트 = I.5.

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

        // ── T1. 결정성 (algorithms.md V1.0-2 T1) ──────────────────────

        [Test]
        public void T1_Determinism_SameSeedSameResult()
        {
            var (state1, match1) = BuildState(homeCA: 110, awayCA: 110, seed: 42, matchId: 1);
            var (state2, match2) = BuildState(homeCA: 110, awayCA: 110, seed: 42, matchId: 1);

            var r1 = MatchSimulator.Simulate(match1, state1, _balance);
            var r2 = MatchSimulator.Simulate(match2, state2, _balance);

            Assert.AreEqual(r1.homeScore, r2.homeScore, "T1: homeScore 결정적");
            Assert.AreEqual(r1.awayScore, r2.awayScore, "T1: awayScore 결정적");
            CollectionAssert.AreEqual(r1.homeStarting11, r2.homeStarting11);
            CollectionAssert.AreEqual(r1.awayStarting11, r2.awayStarting11);
            Assert.AreEqual(r1.playerStats.Count, r2.playerStats.Count);
            for (int i = 0; i < r1.playerStats.Count; i++)
            {
                Assert.AreEqual(r1.playerStats[i].playerId, r2.playerStats[i].playerId);
                Assert.AreEqual(r1.playerStats[i].goals, r2.playerStats[i].goals);
                Assert.AreEqual(r1.playerStats[i].shots, r2.playerStats[i].shots);
                Assert.AreEqual(r1.playerStats[i].passes, r2.playerStats[i].passes);
            }
        }

        // ── T10. 인터페이스 호환 ──────────────────────────────────────

        [Test]
        public void T10_InterfaceCompatibility_SignatureUnchanged()
        {
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
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
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
            var result = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(11, result.homeStarting11.Count);
            var homeClub = state.GetClub(match.homeClubId);
            var bench = homeClub
                .seniorSquadIds.Where(id => !result.homeStarting11.Contains(id))
                .Select(id => state.GetPlayer(id))
                .ToList();
            int starting11MinCA = result
                .homeStarting11.Select(id => state.GetPlayer(id).currentAbility)
                .Min();
            int benchMaxCA = bench.Count > 0 ? bench.Max(p => p.currentAbility) : 0;
            Assert.GreaterOrEqual(starting11MinCA, benchMaxCA);
        }

        [Test]
        public void StartingEleven_ExcludesInjuredPlayers()
        {
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
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
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
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
            nextId = AddPlayersToClub(state, home, ca: 100, nextId: nextId, count: 5);
            nextId = AddPlayersToClub(state, away, ca: 100, nextId: nextId, count: 25);
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

        // ── I.2 골 생성 (200 매치 평균 골수) ─────────────────────────

        [Test]
        public void I2_Goals_GeneratedAcrossMultipleMatches()
        {
            var seedGen = new System.Random(42);
            int totalGoals = 0;
            const int N = 200;
            for (int i = 0; i < N; i++)
            {
                int seed = seedGen.Next();
                var (state, match) = BuildState(
                    homeCA: 100,
                    awayCA: 100,
                    seed: seed,
                    matchId: i + 1
                );
                var r = MatchSimulator.Simulate(match, state, _balance);
                totalGoals += r.homeScore + r.awayScore;
            }
            double avgGoals = (double)totalGoals / N;
            Assert.Greater(totalGoals, 0, "I.2: 200 매치 중 적어도 1골 발생");
            // EPL 평균 ~2.7 골/매치. 외부화 분모 조정 시 ±50% 허용.
            Assert.That(
                avgGoals,
                Is.InRange(0.5, 6.0),
                $"I.2: 평균 골수 합리 범위 (실측 {avgGoals:F2}/매치)"
            );
        }

        // ── I.2 슛 분포 (100 매치) ────────────────────────────────────

        [Test]
        public void I2_Shots_DistributionInRange()
        {
            var seedGen = new System.Random(100);
            int totalShots = 0;
            const int N = 100;
            for (int i = 0; i < N; i++)
            {
                int seed = seedGen.Next();
                var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: seed, matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                totalShots += r.playerStats.Sum(ps => ps.shots);
            }
            double avgShotsPerTeamPerMatch = (double)totalShots / N / 2;
            Assert.That(
                avgShotsPerTeamPerMatch,
                Is.InRange(6.0, 20.0),
                $"I.2: 평균 슛/팀/매치 ~6-20 (target 12, 실측 {avgShotsPerTeamPerMatch:F1})"
            );
        }

        // ── I.2 누적 통계 채워짐 (50 매치) ────────────────────────────

        [Test]
        public void I2_PlayerMatchStat_AccumulatesAllFields()
        {
            var seedGen = new System.Random(200);
            int totalPasses = 0,
                totalTackles = 0,
                totalKeyPasses = 0,
                totalFouls = 0,
                totalShotsOnTarget = 0;
            const int N = 50;
            for (int i = 0; i < N; i++)
            {
                int seed = seedGen.Next();
                var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: seed, matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                totalPasses += r.playerStats.Sum(ps => ps.passes);
                totalTackles += r.playerStats.Sum(ps => ps.tackles);
                totalKeyPasses += r.playerStats.Sum(ps => ps.keyPasses);
                totalFouls += r.playerStats.Sum(ps => ps.foulsCommitted);
                totalShotsOnTarget += r.playerStats.Sum(ps => ps.shotsOnTarget);
            }
            Assert.Greater(totalPasses, 0, "I.2: passes > 0");
            Assert.Greater(totalTackles, 0, "I.2: tackles > 0");
            Assert.Greater(totalKeyPasses, 0, "I.2: keyPasses > 0");
            Assert.Greater(totalFouls, 0, "I.2: foulsCommitted > 0");
            Assert.Greater(totalShotsOnTarget, 0, "I.2: shotsOnTarget > 0");
        }

        // ── I.2 minutesPlayed 기본 90 (I.6 가변 도입 전) ──────────────

        [Test]
        public void I2_PlayerMatchStat_MinutesPlayedIs90()
        {
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 5, matchId: 1);
            var r = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(
                r.homeStarting11.Count + r.awayStarting11.Count,
                r.playerStats.Count
            );
            foreach (var ps in r.playerStats)
            {
                Assert.AreEqual(90, ps.minutesPlayed, $"minutesPlayed=90 (id={ps.playerId})");
                Assert.AreEqual(0f, ps.rating, $"rating=0 (I.4) (id={ps.playerId})");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private (GameState, Match) BuildState(int homeCA, int awayCA, int seed, int matchId)
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
            nextId = AddPlayersToClub(state, home, ca: homeCA, nextId: nextId, count: 25);
            nextId = AddPlayersToClub(state, away, ca: awayCA, nextId: nextId, count: 25);
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

        private static int AddPlayersToClub(
            GameState state,
            Club club,
            int ca,
            int nextId,
            int count
        )
        {
            for (int i = 0; i < count; i++)
            {
                int variedCA = ca + ((i % 5) - 2);
                Position pos = (i % 5) switch
                {
                    0 => Position.GK,
                    1 => Position.CB,
                    2 => Position.CM,
                    3 => Position.ST,
                    _ => Position.LM,
                };
                state.AddPlayer(NewPlayer(nextId, pos, variedCA));
                club.seniorSquadIds.Add(nextId);
                nextId++;
            }
            return nextId;
        }

        private static Player NewPlayer(int id, Position pos, int ca) =>
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
                // I.2 매치 엔진이 stat 직접 참조 (finishing × composure 등). 평균 stat 50 으로 채움.
                stats = NewBalancedStats(50),
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
