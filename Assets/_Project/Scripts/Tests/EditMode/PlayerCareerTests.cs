// PlayerCareerTests.cs
// DoD: v1.0-tasks.md M.1 — SeasonEndProcessor.Run → Player.career.Add

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class PlayerCareerTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2025, 6, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.retirementMinAge = 99; // 은퇴 없음
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. 결과 있는 경기 1개 → career entry 1개 ─────────────────────

        [Test]
        public void T1_MatchWithResult_AddsCareerEntry()
        {
            var state = BuildState();
            var player = MakePlayer(1, state);

            var league = new League { id = 1, seasonYear = 2025 };
            var match = MakeMatch(homeClubId: 10, awayClubId: 20);
            match.result = new MatchResult();
            match.result.homeStarting11.Add(player.id);
            match.result.playerStats.Add(
                new PlayerMatchStat { playerId = player.id, rating = 7f, minutesPlayed = 90 }
            );
            league.schedule.Add(match);
            state.leagues.Add(league);

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(1, player.career.Count, "T1: career entry 1개");
        }

        // ── T2. 스탯 정확히 집계 ────────────────────────────────────────────

        [Test]
        public void T2_Stats_AggregatedCorrectly()
        {
            var state = BuildState();
            var player = MakePlayer(1, state);

            var league = new League { id = 1, seasonYear = 2025 };
            var match = MakeMatch(homeClubId: 10, awayClubId: 20);
            match.result = new MatchResult();
            match.result.homeStarting11.Add(player.id);
            match.result.playerStats.Add(
                new PlayerMatchStat
                {
                    playerId = player.id,
                    goals = 2,
                    assists = 1,
                    rating = 7.5f,
                    yellowCards = 1,
                    redCards = 0,
                    minutesPlayed = 90,
                }
            );
            league.schedule.Add(match);
            state.leagues.Add(league);

            SeasonEndProcessor.Run(state, _balance);

            var stat = player.career[0];
            Assert.AreEqual(2025, stat.seasonYear, "T2: seasonYear");
            Assert.AreEqual(10, stat.clubId, "T2: clubId (homeStarting11)");
            Assert.AreEqual(1, stat.appearances, "T2: appearances");
            Assert.AreEqual(2, stat.goals, "T2: goals");
            Assert.AreEqual(1, stat.assists, "T2: assists");
            Assert.AreEqual(7.5f, stat.averageRating, 0.001f, "T2: averageRating");
            Assert.AreEqual(1, stat.yellowCards, "T2: yellowCards");
            Assert.AreEqual(0, stat.redCards, "T2: redCards");
            Assert.AreEqual(90, stat.minutesPlayed, "T2: minutesPlayed");
        }

        // ── T3. 여러 경기 → appearances + 스탯 누적 ────────────────────────

        [Test]
        public void T3_MultipleMatches_StatsAccumulated()
        {
            var state = BuildState();
            var player = MakePlayer(1, state);

            var league = new League { id = 1, seasonYear = 2025 };
            for (int i = 0; i < 3; i++)
            {
                var match = MakeMatch(homeClubId: 10, awayClubId: 20);
                match.result = new MatchResult();
                match.result.homeStarting11.Add(player.id);
                match.result.playerStats.Add(
                    new PlayerMatchStat
                    {
                        playerId = player.id,
                        goals = 1,
                        rating = 6.0f + i,
                        minutesPlayed = 90,
                    }
                );
                league.schedule.Add(match);
            }
            state.leagues.Add(league);

            SeasonEndProcessor.Run(state, _balance);

            var stat = player.career[0];
            Assert.AreEqual(3, stat.appearances, "T3: appearances=3");
            Assert.AreEqual(3, stat.goals, "T3: goals=3");
            Assert.AreEqual(7.0f, stat.averageRating, 0.001f, "T3: averageRating=(6+7+8)/3");
            Assert.AreEqual(270, stat.minutesPlayed, "T3: minutesPlayed=270");
        }

        // ── T4. 결과 없는 경기 무시 ─────────────────────────────────────────

        [Test]
        public void T4_MatchWithoutResult_Skipped()
        {
            var state = BuildState();
            var player = MakePlayer(1, state);

            var league = new League { id = 1, seasonYear = 2025 };
            var matchWithResult = MakeMatch(homeClubId: 10, awayClubId: 20);
            matchWithResult.result = new MatchResult();
            matchWithResult.result.homeStarting11.Add(player.id);
            matchWithResult.result.playerStats.Add(
                new PlayerMatchStat { playerId = player.id, rating = 7f, minutesPlayed = 90 }
            );
            league.schedule.Add(matchWithResult);
            league.schedule.Add(MakeMatch(homeClubId: 10, awayClubId: 20)); // 결과 없음
            state.leagues.Add(league);

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(1, player.career[0].appearances, "T4: 결과 없는 경기 무시");
        }

        // ── T5. 리그 없음 → career 비어있음 ────────────────────────────────

        [Test]
        public void T5_NoLeagues_NoCareerEntry()
        {
            var state = BuildState();
            var player = MakePlayer(1, state);

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(0, player.career.Count, "T5: 리그 없음 → career 비어있음");
        }

        // ── T6. 두 리그 → 2개의 SeasonStat ────────────────────────────────

        [Test]
        public void T6_TwoLeagues_TwoCareerEntries()
        {
            var state = BuildState();
            var player = MakePlayer(1, state);

            for (int leagueId = 1; leagueId <= 2; leagueId++)
            {
                var league = new League { id = leagueId, seasonYear = 2025 };
                var match = MakeMatch(homeClubId: 10, awayClubId: 20);
                match.result = new MatchResult();
                match.result.homeStarting11.Add(player.id);
                match.result.playerStats.Add(
                    new PlayerMatchStat { playerId = player.id, rating = 7f, minutesPlayed = 90 }
                );
                league.schedule.Add(match);
                state.leagues.Add(league);
            }

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(2, player.career.Count, "T6: 리그 2개 → SeasonStat 2개");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        private GameState BuildState() => new GameState { currentDate = _today };

        private Player MakePlayer(int id, GameState state)
        {
            var player = new Player
            {
                id = id,
                info = new PersonalInfo { birthDate = new DateTime(2000, 1, 1) },
                contract = new Contract { endDate = _today.AddYears(2) },
                currentClubId = 10,
            };
            state.AddPlayer(player);
            return player;
        }

        private static Match MakeMatch(int homeClubId, int awayClubId) =>
            new Match { homeClubId = homeClubId, awayClubId = awayClubId };
    }
}
