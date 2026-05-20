// MatchPostProcessorTests.cs
// DoD: data-flows.md #3 [4] V0.1 책임 검증. 이슈 #117 — Task 9.2.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Application;

namespace FMLite.Tests
{
    public class MatchPostProcessorTests
    {
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.fatigueGainPerMatch = 30;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        // ── T1. 결과 적용 ────────────────────────────────────────────

        [Test]
        public void T1_AppliesResultToMatch()
        {
            var (state, match, result) = BuildScenario(homeScore: 2, awayScore: 1);
            Assert.IsNull(match.result, "T1: 사전 — match.result null");

            MatchPostProcessor.Process(match, result, state, _balance);

            Assert.AreSame(result, match.result, "T1: match.result == result (참조 동일)");
        }

        // ── T2. 피로 갱신 ────────────────────────────────────────────

        [Test]
        public void T2_FatigueAddedToStarting11Only_WithClamp()
        {
            var (state, match, result) = BuildScenario(homeScore: 1, awayScore: 0);
            var home = state.GetClub(match.homeClubId);
            // 벤치 = 스쿼드 - starting11
            var benchIds = home.seniorSquadIds.Where(id => !result.homeStarting11.Contains(id)).ToList();
            Assert.IsNotEmpty(benchIds, "T2 사전: 벤치가 존재해야 함 (25명 - 11명 = 14)");

            // starting11 중 1명을 fatigue=85 로 초기화 → Clamp 100 검증용
            int highFatigueId = result.homeStarting11[0];
            state.GetPlayer(highFatigueId).state.fatigue = 85;

            MatchPostProcessor.Process(match, result, state, _balance);

            // starting11 22명 모두 +30 (Clamp 100)
            foreach (var id in result.homeStarting11.Concat(result.awayStarting11))
            {
                int expected = (id == highFatigueId) ? 100 : 30;
                Assert.AreEqual(expected, state.GetPlayer(id).state.fatigue,
                    $"T2: starting11 id={id} fatigue 예상 {expected}");
            }

            // 벤치 = 변화 없음
            foreach (var id in benchIds)
                Assert.AreEqual(0, state.GetPlayer(id).state.fatigue,
                    $"T2: 벤치 id={id} fatigue 0 유지");
        }

        // ── T3. 순위 갱신 ────────────────────────────────────────────

        [Test]
        public void T3_StandingsUpdated_HomeWin()
        {
            var (state, match, result) = BuildScenario(homeScore: 3, awayScore: 1);
            MatchPostProcessor.Process(match, result, state, _balance);

            var (homeE, awayE) = GetEntries(state, match);

            Assert.AreEqual(1, homeE.played);
            Assert.AreEqual(1, homeE.won);
            Assert.AreEqual(0, homeE.drawn);
            Assert.AreEqual(0, homeE.lost);
            Assert.AreEqual(3, homeE.goalsFor);
            Assert.AreEqual(1, homeE.goalsAgainst);
            Assert.AreEqual(3, homeE.points);

            Assert.AreEqual(1, awayE.played);
            Assert.AreEqual(0, awayE.won);
            Assert.AreEqual(0, awayE.drawn);
            Assert.AreEqual(1, awayE.lost);
            Assert.AreEqual(1, awayE.goalsFor);
            Assert.AreEqual(3, awayE.goalsAgainst);
            Assert.AreEqual(0, awayE.points);
        }

        [Test]
        public void T3b_StandingsUpdated_Draw()
        {
            var (state, match, result) = BuildScenario(homeScore: 2, awayScore: 2);
            MatchPostProcessor.Process(match, result, state, _balance);

            var (homeE, awayE) = GetEntries(state, match);
            Assert.AreEqual(1, homeE.drawn);
            Assert.AreEqual(1, awayE.drawn);
            Assert.AreEqual(1, homeE.points, "T3b: 무승부 home 1점");
            Assert.AreEqual(1, awayE.points, "T3b: 무승부 away 1점");
            Assert.AreEqual(2, homeE.goalsFor);
            Assert.AreEqual(2, homeE.goalsAgainst);
        }

        [Test]
        public void T3c_StandingsUpdated_AwayWin()
        {
            var (state, match, result) = BuildScenario(homeScore: 0, awayScore: 2);
            MatchPostProcessor.Process(match, result, state, _balance);

            var (homeE, awayE) = GetEntries(state, match);
            Assert.AreEqual(1, homeE.lost);
            Assert.AreEqual(1, awayE.won);
            Assert.AreEqual(0, homeE.points);
            Assert.AreEqual(3, awayE.points, "T3c: 원정 승 away 3점");
        }

        // ── T4. MatchFinishedEvent 발행 ──────────────────────────────

        [Test]
        public void T4_PublishesMatchFinishedEvent()
        {
            var (state, match, result) = BuildScenario(homeScore: 1, awayScore: 0);
            int eventCount = 0;
            MatchFinishedEvent captured = null;
            Action<MatchFinishedEvent> handler = e =>
            {
                eventCount++;
                captured = e;
            };
            EventBus.Subscribe(handler);

            MatchPostProcessor.Process(match, result, state, _balance);

            Assert.AreEqual(1, eventCount, "T4: MatchFinishedEvent 정확히 1회 발행");
            Assert.IsNotNull(captured, "T4: 페이로드 non-null");
            Assert.AreEqual(match.id, captured.matchId, "T4: matchId 일치");
            Assert.AreSame(result, captured.result, "T4: result 참조 일치");

            EventBus.Unsubscribe(handler);
        }

        // ── T5. 결정성 (Stateless 검증) ──────────────────────────────

        [Test]
        public void T5_Stateless_SameInputSameOutput()
        {
            // 두 별개 GameState 에 같은 시나리오 → 같은 결과
            var (s1, m1, r1) = BuildScenario(homeScore: 2, awayScore: 1);
            var (s2, m2, r2) = BuildScenario(homeScore: 2, awayScore: 1);

            MatchPostProcessor.Process(m1, r1, s1, _balance);
            MatchPostProcessor.Process(m2, r2, s2, _balance);

            var (h1, a1) = GetEntries(s1, m1);
            var (h2, a2) = GetEntries(s2, m2);
            Assert.AreEqual(h1.points,       h2.points,       "T5: home points 결정적");
            Assert.AreEqual(h1.goalsFor,     h2.goalsFor,     "T5: home goalsFor 결정적");
            Assert.AreEqual(a1.points,       a2.points,       "T5: away points 결정적");
            Assert.AreEqual(a1.goalsAgainst, a2.goalsAgainst, "T5: away goalsAgainst 결정적");

            // starting11 모든 선수 fatigue 동일 (peer id 비교)
            foreach (var id in r1.homeStarting11)
                Assert.AreEqual(s1.GetPlayer(id).state.fatigue, s2.GetPlayer(id).state.fatigue,
                    $"T5: id={id} fatigue 결정적");
        }

        // ── T6. 이미 처리된 매치 재처리 → throw ──────────────────────

        [Test]
        public void T6_AlreadyProcessedMatch_Throws()
        {
            var (state, match, result) = BuildScenario(homeScore: 1, awayScore: 1);
            MatchPostProcessor.Process(match, result, state, _balance);
            // 두 번째 호출 — match.result 가 이미 채워졌으므로 InvalidOperationException
            Assert.Throws<InvalidOperationException>(() =>
                MatchPostProcessor.Process(match, result, state, _balance));
        }

        // ── Helpers ───────────────────────────────────────────────────

        // 단일 매치 시나리오 생성 — League 1개, 클럽 2개, 각 클럽 25명 (CA 100 균등),
        // Standings 초기화, MatchResult 합성 (starting11 = 양 팀 첫 11명).
        private (GameState state, Match match, MatchResult result) BuildScenario(int homeScore, int awayScore)
        {
            var state = new GameState
            {
                randomSeed = 42,
                currentDate = new DateTime(2025, 8, 15),
            };

            var home = new Club { id = 1, name = "Home", reputation = 60, leagueId = 1 };
            var away = new Club { id = 2, name = "Away", reputation = 60, leagueId = 1 };
            state.AddClub(home);
            state.AddClub(away);

            int nextId = 1;
            for (int i = 0; i < 25; i++)
            {
                var p = NewPlayer(nextId, ca: 100);
                state.AddPlayer(p);
                home.seniorSquadIds.Add(nextId);
                nextId++;
            }
            for (int i = 0; i < 25; i++)
            {
                var p = NewPlayer(nextId, ca: 100);
                state.AddPlayer(p);
                away.seniorSquadIds.Add(nextId);
                nextId++;
            }

            var league = new League
            {
                id = 1,
                clubIds = new List<int> { 1, 2 },
                standings = new Standings
                {
                    entries = new List<StandingEntry>
                    {
                        new StandingEntry { clubId = 1 },
                        new StandingEntry { clubId = 2 },
                    },
                },
            };
            state.leagues.Add(league);

            var match = new Match
            {
                id = 100,
                date = new DateTime(2025, 8, 15),
                type = CompetitionType.League,
                homeClubId = 1,
                awayClubId = 2,
            };

            var homeStarting11 = home.seniorSquadIds.Take(11).ToList();
            var awayStarting11 = away.seniorSquadIds.Take(11).ToList();
            var playerStats = new List<PlayerMatchStat>();
            foreach (var id in homeStarting11.Concat(awayStarting11))
            {
                playerStats.Add(new PlayerMatchStat { playerId = id, minutesPlayed = 90 });
            }
            var result = new MatchResult
            {
                homeScore      = homeScore,
                awayScore      = awayScore,
                homeStarting11 = homeStarting11,
                awayStarting11 = awayStarting11,
                playerStats    = playerStats,
            };

            return (state, match, result);
        }

        private static Player NewPlayer(int id, int ca) => new Player
        {
            id = id,
            currentAbility = ca,
            potentialAbility = ca + 10,
            info = new PersonalInfo { primaryPosition = Position.CM, firstName = "F", lastName = "L" },
            state = new PlayerState
            {
                injury = new InjuryInfo { injuryTypeId = -1 },
                fatigue = 0, morale = 50, form = 50,
            },
        };

        private static (StandingEntry home, StandingEntry away) GetEntries(GameState state, Match match)
        {
            var league = state.leagues[0];
            var home = league.standings.entries.First(e => e.clubId == match.homeClubId);
            var away = league.standings.entries.First(e => e.clubId == match.awayClubId);
            return (home, away);
        }
    }
}
