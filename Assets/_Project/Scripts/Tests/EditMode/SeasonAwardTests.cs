// SeasonAwardTests.cs
// DoD: algorithms.md V1.0-9 T1~T4, T6

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
    public class SeasonAwardTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 5, 15);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.mvpChampionBonus = 0.5f;
            _balance.awardMoraleBonus = 10;
            _balance.awardHappinessBonus = 10;
            _balance.youngPlayerMaxAge = 21;
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. TopScorer ─────────────────────────────────────────────────

        [Test]
        public void T1_TopScorer_HighestGoalsWins()
        {
            var (state, league, club) = BuildBasicState();
            var p1 = AddPlayer(state, club, id: 1, position: Position.ST);
            var p2 = AddPlayer(state, club, id: 2, position: Position.CM);

            AddMatch(
                league,
                club.id,
                club.id,
                new[] { (p1.id, goals: 3, assists: 0, rating: 7f) },
                new[] { (p2.id, goals: 1, assists: 0, rating: 6f) }
            );

            var awards = SeasonAwardSystem.ComputeSeasonAwards(state, _balance);

            var ts = awards.First(a => a.type == AwardType.TopScorer);
            Assert.AreEqual(p1.id, ts.playerIds[0], "T1: 골 3개 선수 TopScorer");
        }

        // ── T2. LeagueMVP 우승팀 가산 ─────────────────────────────────────

        [Test]
        public void T2_LeagueMVP_ChampionBonus()
        {
            var state = new GameState { currentDate = _today };
            var clubA = MakeClub(1);
            var clubB = MakeClub(2);
            state.AddClub(clubA);
            state.AddClub(clubB);

            var league = new League { id = 1, seasonYear = 2025 };
            league.clubIds.Add(clubA.id);
            league.clubIds.Add(clubB.id);
            // 우승팀 = clubA
            league.standings = new Standings();
            league.standings.entries.Add(new StandingEntry { clubId = clubA.id, points = 80 });
            league.standings.entries.Add(new StandingEntry { clubId = clubB.id, points = 60 });
            state.leagues.Add(league);

            // pA (clubA, rating=7.0) vs pB (clubB, rating=7.1) — mvpChampionBonus=0.5 → pA wins
            var pA = AddPlayer(state, clubA, id: 10, position: Position.CM);
            var pB = AddPlayer(state, clubB, id: 20, position: Position.CM);
            pA.state = new PlayerState();
            pB.state = new PlayerState();

            AddMatch(
                league,
                clubA.id,
                clubB.id,
                new[] { (pA.id, goals: 0, assists: 0, rating: 7.0f) },
                new[] { (pB.id, goals: 0, assists: 0, rating: 7.1f) }
            );

            var awards = SeasonAwardSystem.ComputeSeasonAwards(state, _balance);

            var mvp = awards.First(a => a.type == AwardType.LeagueMVP);
            Assert.AreEqual(pA.id, mvp.playerIds[0], "T2: 우승팀 선수가 MVP (champion bonus 적용)");
        }

        // ── T3. YoungPlayer 나이 필터 ─────────────────────────────────────

        [Test]
        public void T3_YoungPlayer_AgeFilter()
        {
            var (state, league, club) = BuildBasicState();
            // p22: 22세 — 자격 없음
            var p22 = AddPlayer(
                state,
                club,
                id: 1,
                position: Position.ST,
                birthYear: _today.Year - 22
            );
            // p21: 21세 — 자격 있음
            var p21 = AddPlayer(
                state,
                club,
                id: 2,
                position: Position.CM,
                birthYear: _today.Year - 21
            );

            AddMatch(
                league,
                club.id,
                club.id,
                new[] { (p22.id, goals: 0, assists: 0, rating: 8.0f) },
                new[] { (p21.id, goals: 0, assists: 0, rating: 7.8f) }
            );

            var awards = SeasonAwardSystem.ComputeSeasonAwards(state, _balance);

            var yp = awards.FirstOrDefault(a => a.type == AwardType.YoungPlayer);
            Assert.IsNotNull(yp, "T3: YoungPlayer 수상됨");
            Assert.AreEqual(p21.id, yp.playerIds[0], "T3: 22세 평점 8.0 아닌 21세 평점 7.8 수상");
        }

        // ── T4. BestEleven 포지션 균형 ────────────────────────────────────

        [Test]
        public void T4_BestEleven_PositionBalance()
        {
            var (state, league, club) = BuildBasicState();

            // 1 GK, 4 DF, 4 MF, 2 AT
            var positions = new[]
            {
                Position.GK,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.WB,
                Position.DM,
                Position.CM,
                Position.AM,
                Position.LM,
                Position.ST,
                Position.CF,
            };
            var players = new List<Player>();
            for (int i = 0; i < positions.Length; i++)
            {
                var p = AddPlayer(state, club, id: i + 1, position: positions[i]);
                players.Add(p);
            }

            // 모든 선수 매치 스탯 추가
            var stats = players
                .Select(p => (p.id, goals: 0, assists: 0, rating: 7.0f))
                .ToArray();
            AddMatchAll(league, club.id, stats);

            var awards = SeasonAwardSystem.ComputeSeasonAwards(state, _balance);

            var be = awards.FirstOrDefault(a => a.type == AwardType.BestEleven);
            Assert.IsNotNull(be, "T4: BestEleven 수상됨");
            Assert.AreEqual(11, be.playerIds.Count, "T4: BestEleven 11명");

            // 포지션 분포 검증
            int gkCount = be.playerIds.Count(pid =>
                state.GetPlayer(pid)?.info?.primaryPosition == Position.GK
            );
            int dfCount = be.playerIds.Count(pid =>
            {
                var pos = state.GetPlayer(pid)?.info?.primaryPosition;
                return pos == Position.CB
                    || pos == Position.LB
                    || pos == Position.RB
                    || pos == Position.WB;
            });
            int mfCount = be.playerIds.Count(pid =>
            {
                var pos = state.GetPlayer(pid)?.info?.primaryPosition;
                return pos == Position.DM
                    || pos == Position.CM
                    || pos == Position.AM
                    || pos == Position.LM
                    || pos == Position.RM
                    || pos == Position.LW
                    || pos == Position.RW;
            });
            int atCount = be.playerIds.Count(pid =>
            {
                var pos = state.GetPlayer(pid)?.info?.primaryPosition;
                return pos == Position.ST || pos == Position.CF;
            });

            Assert.AreEqual(1, gkCount, "T4: GK=1");
            Assert.AreEqual(4, dfCount, "T4: DF=4");
            Assert.AreEqual(4, mfCount, "T4: MF=4");
            Assert.AreEqual(2, atCount, "T4: AT=2");
        }

        // ── T6. history 누적 ──────────────────────────────────────────────

        [Test]
        public void T6_SeasonHistory_AppendedAfterAwards()
        {
            var (state, league, club) = BuildBasicState();
            var p = AddPlayer(state, club, id: 1, position: Position.ST);
            AddMatch(
                league,
                club.id,
                club.id,
                new[] { (p.id, goals: 1, assists: 0, rating: 7f) },
                Array.Empty<(int, int, int, float)>()
            );

            SeasonAwardSystem.ComputeSeasonAwards(state, _balance);

            Assert.AreEqual(1, league.history.Count, "T6: league.history.Count == 1");
            Assert.AreEqual(2025, league.history[0].seasonYear, "T6: seasonYear 기록됨");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        private (GameState, League, Club) BuildBasicState()
        {
            var state = new GameState { currentDate = _today };
            var club = MakeClub(1);
            state.AddClub(club);
            var league = new League { id = 1, seasonYear = 2025 };
            league.clubIds.Add(club.id);
            league.standings = new Standings();
            league.standings.entries.Add(new StandingEntry { clubId = club.id, points = 60 });
            state.leagues.Add(league);
            return (state, league, club);
        }

        private Club MakeClub(int id) =>
            new Club
            {
                id = id,
                name = $"Club{id}",
                season = new SeasonState(),
            };

        private Player AddPlayer(
            GameState state,
            Club club,
            int id,
            Position position,
            int birthYear = 1995
        )
        {
            var p = new Player
            {
                id = id,
                info = new PersonalInfo
                {
                    birthDate = new DateTime(birthYear, 1, 1),
                    primaryPosition = position,
                },
                state = new PlayerState { morale = 50, happiness = 70 },
                currentClubId = club.id,
            };
            club.seniorSquadIds.Add(id);
            state.AddPlayer(p);
            return p;
        }

        private void AddMatch(
            League league,
            int homeClubId,
            int awayClubId,
            (int id, int goals, int assists, float rating)[] homeStats,
            (int id, int goals, int assists, float rating)[] awayStats
        )
        {
            var result = new MatchResult();
            foreach (var s in homeStats)
            {
                result.homeStarting11.Add(s.id);
                result.playerStats.Add(
                    new PlayerMatchStat
                    {
                        playerId = s.id,
                        goals = s.goals,
                        assists = s.assists,
                        rating = s.rating,
                        minutesPlayed = 90,
                    }
                );
            }
            foreach (var s in awayStats)
            {
                result.awayStarting11.Add(s.id);
                result.playerStats.Add(
                    new PlayerMatchStat
                    {
                        playerId = s.id,
                        goals = s.goals,
                        assists = s.assists,
                        rating = s.rating,
                        minutesPlayed = 90,
                    }
                );
            }
            league.schedule.Add(
                new Match
                {
                    homeClubId = homeClubId,
                    awayClubId = awayClubId,
                    result = result,
                    date = _today.AddDays(-7),
                }
            );
        }

        private void AddMatchAll(
            League league,
            int clubId,
            (int id, int goals, int assists, float rating)[] stats
        )
        {
            var result = new MatchResult();
            foreach (var s in stats)
            {
                result.homeStarting11.Add(s.id);
                result.playerStats.Add(
                    new PlayerMatchStat
                    {
                        playerId = s.id,
                        goals = s.goals,
                        assists = s.assists,
                        rating = s.rating,
                        minutesPlayed = 90,
                    }
                );
            }
            league.schedule.Add(
                new Match
                {
                    homeClubId = clubId,
                    awayClubId = clubId,
                    result = result,
                    date = _today.AddDays(-7),
                }
            );
        }
    }
}
