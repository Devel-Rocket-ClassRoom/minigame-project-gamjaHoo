// LeaderboardSystemTests.cs
// V1.0 R.11 (#528): LeaderboardSystem 집계 / 필터 / 정렬 검증.

using System;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class LeaderboardSystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 5, 15);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.leaderboardRatingMinApps = 2;
            _balance.leaderboardDefaultTopN = 10;
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. BuildLeagueStats 누적 ─────────────────────────────────────

        [Test]
        public void T1_BuildLeagueStats_Accumulates()
        {
            var (state, league, club) = BuildBasicState();
            var p1 = AddPlayer(state, club, id: 1, position: Position.ST);

            // 2경기: 골 2+1, 도움 0+1, 평점 7+8
            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (p1.id, 2, 0, 7f) }, EmptyStats());
            AddMatch(league, club.id, club.id, 3, 0,
                new[] { (p1.id, 1, 1, 8f) }, EmptyStats());

            var acc = LeaderboardSystem.BuildLeagueStats(league);

            Assert.IsTrue(acc.ContainsKey(p1.id));
            var s = acc[p1.id];
            Assert.AreEqual(2, s.apps, "apps");
            Assert.AreEqual(3, s.goals, "goals");
            Assert.AreEqual(1, s.assists, "assists");
            Assert.AreEqual(15f, s.ratingSum, 0.001f, "ratingSum");
            Assert.AreEqual(7.5f, s.AvgRating, 0.001f, "avgRating");
        }

        // ── T2. 득점 순위 + competition ranking ───────────────────────────

        [Test]
        public void T2_Goals_RankingWithTies()
        {
            var (state, league, club) = BuildBasicState();
            var pA = AddPlayer(state, club, id: 1, position: Position.ST);
            var pB = AddPlayer(state, club, id: 2, position: Position.CF);
            var pC = AddPlayer(state, club, id: 3, position: Position.CM);

            // A=3, B=3, C=1 → A,B 공동 1위, C 3위
            AddMatch(league, club.id, club.id, 7, 0,
                new[] { (pA.id, 3, 0, 7f), (pB.id, 3, 0, 7f), (pC.id, 1, 0, 6f) },
                EmptyStats());

            var board = LeaderboardSystem.GetLeaderboard(
                state, league, _balance, LeaderboardCategory.Goals);

            Assert.AreEqual(3, board.Count);
            Assert.AreEqual(1, board[0].rank);
            Assert.AreEqual(1, board[1].rank, "동률 3골 둘 다 1위");
            Assert.AreEqual(3, board[2].rank, "다음 순위는 3위로 건너뜀");
            Assert.AreEqual(pC.id, board[2].playerId);
            Assert.AreEqual(3f, board[0].value, 0.001f);
        }

        // ── T3. 평점 순위 — 최소 출전 필터 ───────────────────────────────

        [Test]
        public void T3_Rating_MinAppsFilter()
        {
            var (state, league, club) = BuildBasicState();
            var pHigh1App = AddPlayer(state, club, id: 1, position: Position.AM);
            var pSteady = AddPlayer(state, club, id: 2, position: Position.CM);

            // pHigh: 1경기 평점 9.5 (필터 < 2 → 제외)
            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (pHigh1App.id, 0, 0, 9.5f) }, EmptyStats());
            // pSteady: 2경기 평점 7.0 평균
            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (pSteady.id, 0, 0, 7f) }, EmptyStats());
            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (pSteady.id, 0, 0, 7f) }, EmptyStats());

            var board = LeaderboardSystem.GetLeaderboard(
                state, league, _balance, LeaderboardCategory.Rating);

            Assert.AreEqual(1, board.Count, "1경기 선수 제외");
            Assert.AreEqual(pSteady.id, board[0].playerId);
            Assert.AreEqual(7f, board[0].value, 0.001f);
        }

        // ── T4. 클린시트 — GK 한정 + 무실점 카운트 ───────────────────────

        [Test]
        public void T4_CleanSheets_GkOnly()
        {
            var (state, league, club) = BuildBasicState();
            var gk = AddPlayer(state, club, id: 1, position: Position.GK);
            var cb = AddPlayer(state, club, id: 2, position: Position.CB);

            // 2경기 무실점 (awayScore=0), 1경기 실점 (awayScore=2)
            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (gk.id, 0, 0, 7f), (cb.id, 0, 0, 7f) }, EmptyStats());
            AddMatch(league, club.id, club.id, 2, 0,
                new[] { (gk.id, 0, 0, 7f), (cb.id, 0, 0, 7f) }, EmptyStats());
            AddMatch(league, club.id, club.id, 1, 2,
                new[] { (gk.id, 0, 0, 6f), (cb.id, 0, 0, 6f) }, EmptyStats());

            var board = LeaderboardSystem.GetLeaderboard(
                state, league, _balance, LeaderboardCategory.CleanSheets);

            Assert.AreEqual(1, board.Count, "GK 만 — CB 제외");
            Assert.AreEqual(gk.id, board[0].playerId);
            Assert.AreEqual(2f, board[0].value, 0.001f, "무실점 2경기");

            // BuildLeagueStats 상으로는 CB 도 클린시트 가산되어 있어야 (출처 동일)
            var acc = LeaderboardSystem.BuildLeagueStats(league);
            Assert.AreEqual(2, acc[cb.id].cleanSheets, "CB 도 선발 무실점 가산 (필터만 GK)");
        }

        // ── T5. 출전 순위 + topN 제한 ────────────────────────────────────

        [Test]
        public void T5_Appearances_AndTopNLimit()
        {
            var (state, league, club) = BuildBasicState();
            var p1 = AddPlayer(state, club, id: 1, position: Position.ST);
            var p2 = AddPlayer(state, club, id: 2, position: Position.CM);
            var p3 = AddPlayer(state, club, id: 3, position: Position.CB);

            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (p1.id, 0, 0, 7f), (p2.id, 0, 0, 7f), (p3.id, 0, 0, 7f) }, EmptyStats());
            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (p1.id, 0, 0, 7f), (p2.id, 0, 0, 7f) }, EmptyStats());

            var board = LeaderboardSystem.GetLeaderboard(
                state, league, _balance, LeaderboardCategory.Appearances, topN: 2);

            Assert.AreEqual(2, board.Count, "topN=2 제한");
            Assert.AreEqual(2f, board[0].value, 0.001f, "최다 출전 2경기");
            Assert.AreEqual(1, board[0].rank);
        }

        // ── T6. clubId 채워짐 (자기구단 강조 근거) ───────────────────────

        [Test]
        public void T6_Entry_ClubIdPopulated()
        {
            var (state, league, club) = BuildBasicState();
            var p1 = AddPlayer(state, club, id: 1, position: Position.ST);
            AddMatch(league, club.id, club.id, 1, 0,
                new[] { (p1.id, 1, 0, 7f) }, EmptyStats());

            var board = LeaderboardSystem.GetLeaderboard(
                state, league, _balance, LeaderboardCategory.Goals);

            Assert.AreEqual(club.id, board[0].clubId);
            Assert.AreEqual(1, board[0].apps);
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────

        private (GameState, League, Club) BuildBasicState()
        {
            var state = new GameState { currentDate = _today };
            var club = new Club { id = 1, name = "Club1", season = new SeasonState() };
            state.AddClub(club);
            var league = new League { id = 1, seasonYear = 2025 };
            league.clubIds.Add(club.id);
            league.standings = new Standings();
            state.leagues.Add(league);
            return (state, league, club);
        }

        private Player AddPlayer(GameState state, Club club, int id, Position position)
        {
            var p = new Player
            {
                id = id,
                info = new PersonalInfo
                {
                    firstName = "P",
                    lastName = id.ToString(),
                    birthDate = new DateTime(1995, 1, 1),
                    primaryPosition = position,
                },
                state = new PlayerState { morale = 50, happiness = 70 },
                currentClubId = club.id,
            };
            club.seniorSquadIds.Add(id);
            state.AddPlayer(p);
            return p;
        }

        private static (int id, int goals, int assists, float rating)[] EmptyStats() =>
            Array.Empty<(int, int, int, float)>();

        private void AddMatch(
            League league,
            int homeClubId,
            int awayClubId,
            int homeScore,
            int awayScore,
            (int id, int goals, int assists, float rating)[] homeStats,
            (int id, int goals, int assists, float rating)[] awayStats
        )
        {
            var result = new MatchResult { homeScore = homeScore, awayScore = awayScore };
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
    }
}
