// BackgroundSimulatorTests.cs
// DoD: 이슈 #119 — data-flows.md #2 [4] / #3 [6] 라운드 일괄 시뮬레이션 검증.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class BackgroundSimulatorTests
    {
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.avgGoalsPerMatch = 2.70f;
            _balance.homeAdvantageGoalBonus = 0.30f;
            _balance.strengthExponent = 1.5f;
            _balance.scoringWeightByLine = new[] { 0.0f, 0.4f, 1.5f, 5.0f };
            _balance.fatigueGainPerMatch = 30;
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        // ── T1. 1라운드 일괄 시뮬 ────────────────────────────────────

        [Test]
        public void T1_OneRound_AllMatchesProcessedAndStandingsUpdated()
        {
            // 4팀 / 2매치 (1라운드) 시나리오, currentDate = 매치 데이
            var state = BuildLeague(clubCount: 4, matchDate: new DateTime(2025, 8, 15), seed: 42);
            state.currentDate = new DateTime(2025, 8, 15);

            BackgroundSimulator.SimulateDay(state, _balance);

            var league = state.leagues[0];
            var todays = league.schedule.Where(m => m.date.Date == state.currentDate.Date).ToList();
            Assert.AreEqual(2, todays.Count, "T1: 1라운드 2매치 (4팀)");

            foreach (var match in todays)
            {
                Assert.IsNotNull(match.result, $"T1: match.id={match.id} result 채워짐");
                Assert.GreaterOrEqual(match.result.homeScore, 0);
                Assert.GreaterOrEqual(match.result.awayScore, 0);
            }

            // 순위 — 4팀 각 1경기씩 played
            foreach (var entry in league.standings.entries)
                Assert.AreEqual(1, entry.played, $"T1: clubId={entry.clubId} played=1");

            // 총 점수 합 일관성 (승+승=6 / 무무=4 또는 그 조합)
            int totalPoints = league.standings.entries.Sum(e => e.points);
            Assert.IsTrue(
                totalPoints == 6 || totalPoints == 4 || totalPoints == 2,
                $"T1: 2매치 → 가능한 총점 6/4/2 (무 0개/1개/2개) (실측 {totalPoints})"
            );
        }

        // ── T2. 결정성 (Stateless) ───────────────────────────────────

        [Test]
        public void T2_Determinism_SameSeedSameOutcome()
        {
            var date = new DateTime(2025, 8, 15);
            var s1 = BuildLeague(clubCount: 4, matchDate: date, seed: 100);
            var s2 = BuildLeague(clubCount: 4, matchDate: date, seed: 100);
            s1.currentDate = s2.currentDate = date;

            BackgroundSimulator.SimulateDay(s1, _balance);
            BackgroundSimulator.SimulateDay(s2, _balance);

            var matches1 = s1.leagues[0].schedule.Where(m => m.date.Date == date).ToList();
            var matches2 = s2.leagues[0].schedule.Where(m => m.date.Date == date).ToList();

            for (int i = 0; i < matches1.Count; i++)
            {
                Assert.AreEqual(
                    matches1[i].result.homeScore,
                    matches2[i].result.homeScore,
                    $"T2: match[{i}] homeScore 결정적"
                );
                Assert.AreEqual(
                    matches1[i].result.awayScore,
                    matches2[i].result.awayScore,
                    $"T2: match[{i}] awayScore 결정적"
                );
            }
            // 순위 entry 별 points 일치
            for (int i = 0; i < s1.leagues[0].standings.entries.Count; i++)
                Assert.AreEqual(
                    s1.leagues[0].standings.entries[i].points,
                    s2.leagues[0].standings.entries[i].points,
                    $"T2: entry[{i}] points 결정적"
                );
        }

        // ── T3. 이미 처리된 매치 스킵 ────────────────────────────────

        [Test]
        public void T3_AlreadyProcessedMatch_Skipped()
        {
            var date = new DateTime(2025, 8, 15);
            var state = BuildLeague(clubCount: 4, matchDate: date, seed: 42);
            state.currentDate = date;

            // 첫 번째 매치를 미리 결과 채움
            var league = state.leagues[0];
            var first = league.schedule.First(m => m.date.Date == date);
            var preset = new MatchResult
            {
                homeScore = 9,
                awayScore = 9,
                homeStarting11 = new List<int>(),
                awayStarting11 = new List<int>(),
                playerStats = new List<PlayerMatchStat>(),
            };
            first.result = preset;

            // SimulateDay 호출 — preset 매치는 throw 없이 스킵돼야 함
            Assert.DoesNotThrow(() => BackgroundSimulator.SimulateDay(state, _balance));

            // preset 매치는 그대로
            Assert.AreSame(preset, first.result, "T3: 이미 처리된 매치 result 그대로 (재처리 X)");
            Assert.AreEqual(9, first.result.homeScore, "T3: preset 점수 유지");

            // 나머지 매치는 처리됨
            var others = league
                .schedule.Where(m => m.date.Date == date && m.id != first.id)
                .ToList();
            foreach (var m in others)
                Assert.IsNotNull(m.result, $"T3: 다른 매치 id={m.id} 정상 처리");
        }

        // ── T4. 빈 날짜 — no-op ─────────────────────────────────────

        [Test]
        public void T4_EmptyDay_NoOp()
        {
            var date = new DateTime(2025, 8, 15);
            var state = BuildLeague(clubCount: 4, matchDate: date, seed: 42);
            state.currentDate = new DateTime(2025, 8, 16); // 매치 없는 날

            Assert.DoesNotThrow(() => BackgroundSimulator.SimulateDay(state, _balance));

            // 매치들 result 여전히 null (시뮬 안 됨)
            foreach (var m in state.leagues[0].schedule)
                Assert.IsNull(m.result, $"T4: match.id={m.id} 처리 안 됨");

            foreach (var e in state.leagues[0].standings.entries)
                Assert.AreEqual(0, e.played, $"T4: clubId={e.clubId} played=0 유지");
        }

        // ── T5. GameLoop 통합 ───────────────────────────────────────

        [Test]
        public void T5_GameLoopIntegration_BackgroundSimulatorCalledOnAdvance()
        {
            // 매치 데이가 currentDate +1일 (Advance 후) 인 시나리오
            var matchDate = new DateTime(2025, 8, 16);
            var state = BuildLeague(clubCount: 4, matchDate: matchDate, seed: 42);
            state.currentDate = new DateTime(2025, 8, 15);
            GameTime.Reset(state.currentDate);

            // 사전: 매치 result 없음
            var league = state.leagues[0];
            Assert.IsTrue(league.schedule.All(m => m.result == null), "T5 사전: 모든 매치 미처리");

            // AdvanceDay → currentDate 가 8/16 으로 진행 → BackgroundSimulator 가 호출되어 매치 처리
            GameLoop.AdvanceDay(state, _balance);

            // 8/16 매치들이 모두 처리됨
            var todays = league.schedule.Where(m => m.date.Date == matchDate).ToList();
            Assert.IsTrue(todays.Count > 0, "T5: 8/16 에 매치 존재");
            foreach (var m in todays)
                Assert.IsNotNull(m.result, $"T5: GameLoop.AdvanceDay 가 match.id={m.id} 처리");
        }

        // ── Helpers ───────────────────────────────────────────────────

        // N팀 리그, 모든 매치 date 동일 (단일 라운드 시나리오용 단순화).
        // 실제 경기 수: floor(N/2) 매치 (1라운드).
        private static GameState BuildLeague(int clubCount, DateTime matchDate, int seed)
        {
            var state = new GameState { randomSeed = seed, currentDate = matchDate };

            // Clubs
            var clubs = new List<Club>();
            int nextPlayerId = 1;
            for (int i = 0; i < clubCount; i++)
            {
                var club = new Club
                {
                    id = i + 1,
                    name = $"Club{i + 1}",
                    reputation = 60,
                    leagueId = 1,
                };
                state.AddClub(club);
                clubs.Add(club);

                // 25명 스쿼드 (CA 100 균등)
                for (int p = 0; p < 25; p++)
                {
                    var pl = NewPlayer(nextPlayerId, ca: 100);
                    state.AddPlayer(pl);
                    club.seniorSquadIds.Add(nextPlayerId);
                    nextPlayerId++;
                }
            }

            // League + Standings + Schedule (1라운드: floor(N/2) 매치)
            var league = new League
            {
                id = 1,
                clubIds = clubs.Select(c => c.id).ToList(),
                schedule = new List<Match>(),
                standings = new Standings
                {
                    entries = clubs.Select(c => new StandingEntry { clubId = c.id }).ToList(),
                },
            };
            int nextMatchId = 1;
            for (int i = 0; i < clubCount; i += 2)
            {
                if (i + 1 >= clubCount)
                    break;
                league.schedule.Add(
                    new Match
                    {
                        id = nextMatchId++,
                        date = matchDate,
                        type = CompetitionType.League,
                        homeClubId = clubs[i].id,
                        awayClubId = clubs[i + 1].id,
                    }
                );
            }
            state.leagues.Add(league);

            return state;
        }

        private static Player NewPlayer(int id, int ca) =>
            new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = ca + 10,
                info = new PersonalInfo
                {
                    primaryPosition = Position.CM,
                    firstName = "F",
                    lastName = "L",
                },
                state = new PlayerState
                {
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    fatigue = 0,
                    morale = 50,
                    form = 50,
                },
                // V1.0 I.2 매치 엔진이 stat 직접 참조 (finishing × composure 등). 평균 stat 50.
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
