// MatchSimulatorTests.cs
// DoD: algorithms.md V1.0-2 Test Scenarios — Stage I.1 골격 범위 (T1 결정성 / T10 인터페이스 호환).
// 후속: T2 부상 발생 = I.3 / T3 카드 = I.3 / T4 SimulateLite = I.7 / T5 평점 = I.4 / T6 Mentality = I.2+J.3 /
//      T7 form/morale/fatigue = I.8 / T8 Role 가중치 = I.2+J.2 / T9 텍스트 = I.5.

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
            CollectionAssert.AreEqual(
                r1.homeStarting11,
                r2.homeStarting11,
                "T1: homeStarting11 결정적"
            );
            CollectionAssert.AreEqual(
                r1.awayStarting11,
                r2.awayStarting11,
                "T1: awayStarting11 결정적"
            );
            Assert.AreEqual(
                r1.playerStats.Count,
                r2.playerStats.Count,
                "T1: playerStats count 결정적"
            );
            for (int i = 0; i < r1.playerStats.Count; i++)
            {
                Assert.AreEqual(
                    r1.playerStats[i].playerId,
                    r2.playerStats[i].playerId,
                    $"T1: playerStats[{i}].playerId"
                );
            }
        }

        // ── T10. 인터페이스 호환 (algorithms.md V1.0-2 T10) ───────────

        [Test]
        public void T10_InterfaceCompatibility_SignatureUnchanged()
        {
            // Simulate(match, state, balance) → MatchResult 시그니처 호출 가능 + 정상 반환.
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
            MatchResult result = MatchSimulator.Simulate(match, state, _balance);

            Assert.IsNotNull(result, "T10: MatchResult 반환");
            Assert.IsNotNull(result.homeStarting11, "T10: homeStarting11 채워짐");
            Assert.IsNotNull(result.awayStarting11, "T10: awayStarting11 채워짐");
            Assert.IsNotNull(result.playerStats, "T10: playerStats 채워짐");
        }

        // ── starting11 자동 선정 (Tactic 도입 = Stage J) ──────────────

        [Test]
        public void StartingEleven_TopByCAExcludingInjured()
        {
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
            var result = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(11, result.homeStarting11.Count, "25명 스쿼드 → starting11 = 11");
            var homeClub = state.GetClub(match.homeClubId);
            var benchPlayers = homeClub
                .seniorSquadIds.Where(id => !result.homeStarting11.Contains(id))
                .Select(id => state.GetPlayer(id))
                .ToList();
            int starting11MinCA = result
                .homeStarting11.Select(id => state.GetPlayer(id).currentAbility)
                .Min();
            int benchMaxCA = benchPlayers.Count > 0 ? benchPlayers.Max(p => p.currentAbility) : 0;
            Assert.GreaterOrEqual(
                starting11MinCA,
                benchMaxCA,
                "starting11 의 최저 CA >= 벤치 최고 CA (top-by-CA 정렬)"
            );
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
                p.state.injury.injuryTypeId = 1; // 부상 상태

            var result = MatchSimulator.Simulate(match, state, _balance);
            foreach (var p in top5)
                Assert.IsFalse(
                    result.homeStarting11.Contains(p.id),
                    $"부상자 (id={p.id}) 는 starting11 에 포함되면 안 됨"
                );
            Assert.AreEqual(11, result.homeStarting11.Count, "가용 인원 충분 → 여전히 11명");
        }

        [Test]
        public void StartingEleven_ExcludesSuspendedPlayers()
        {
            // V1.0 신규 — algorithms.md V1.0-2 2단계 "suspendedMatches > 0 제외".
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
            var home = state.GetClub(match.homeClubId);
            var top5 = home
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .OrderByDescending(p => p.currentAbility)
                .Take(5)
                .ToList();
            foreach (var p in top5)
                p.state.suspendedMatches = 1; // 출전 정지 상태

            var result = MatchSimulator.Simulate(match, state, _balance);
            foreach (var p in top5)
                Assert.IsFalse(
                    result.homeStarting11.Contains(p.id),
                    $"정지자 (id={p.id}) 는 starting11 에 포함되면 안 됨"
                );
            Assert.AreEqual(11, result.homeStarting11.Count, "가용 인원 충분 → 여전히 11명");
        }

        [Test]
        public void StartingEleven_FewerThan11WhenSquadShort()
        {
            // 스쿼드 5명만 — starting11.Count = 5 가 정상 (Edge case)
            var state = new GameState
            {
                randomSeed = 1,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, name: "Home");
            var away = NewClub(2, name: "Away");
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
            Assert.AreEqual(5, result.homeStarting11.Count, "스쿼드 5명 → starting11 5명");
            Assert.AreEqual(11, result.awayStarting11.Count, "away 정상 11명");
        }

        // ── PlayerMatchStat (I.1 골격 — 모든 누적 필드 0) ─────────────

        [Test]
        public void PlayerMatchStat_FilledAsSkeleton()
        {
            // I.1 골격: homeScore/awayScore = 0, playerStats 모든 누적 필드 0, minutesPlayed=90.
            // 후속: I.2 goals/shots/passes / I.3 yellow/red / I.4 rating / I.6 minutesPlayed 가변.
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 5, matchId: 1);
            var r = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(0, r.homeScore, "I.1 골격: homeScore = 0");
            Assert.AreEqual(0, r.awayScore, "I.1 골격: awayScore = 0");
            Assert.AreEqual(
                r.homeStarting11.Count + r.awayStarting11.Count,
                r.playerStats.Count,
                "starting11 합 == playerStats.Count"
            );

            foreach (var ps in r.playerStats)
            {
                Assert.AreEqual(90, ps.minutesPlayed, $"minutesPlayed=90 (id={ps.playerId})");
                Assert.AreEqual(0, ps.goals, $"I.1 골격: goals=0 (id={ps.playerId})");
                Assert.AreEqual(0, ps.assists, $"I.1 골격: assists=0 (id={ps.playerId})");
                Assert.AreEqual(0f, ps.rating, $"I.1 골격: rating=0 (id={ps.playerId})");
                Assert.AreEqual(0, ps.yellowCards, $"I.1 골격: yellowCards=0 (id={ps.playerId})");
                Assert.AreEqual(0, ps.redCards, $"I.1 골격: redCards=0 (id={ps.playerId})");
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
            var home = NewClub(1, name: "Home");
            var away = NewClub(2, name: "Away");
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
                // 같은 CA 면 정렬 시 안정성 의존 — 약간씩 다양화 (-2..+2)
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
                    injury = new InjuryInfo { injuryTypeId = -1 }, // 부상 없음
                    fatigue = 0,
                    morale = 50,
                    form = 50,
                    suspendedMatches = 0,
                },
            };
    }
}
