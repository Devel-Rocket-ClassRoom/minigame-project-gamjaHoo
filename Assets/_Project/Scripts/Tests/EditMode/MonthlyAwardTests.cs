// MonthlyAwardTests.cs
// DoD: v0.5-tasks.md M.3 — 월 1회 어워드 발행 + 효과 적용 (algorithms.md V0.5-9 T5)

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
    public class MonthlyAwardTests
    {
        private GameBalanceSO _balance;

        // 현재 날짜 = 매월 1일 (DailyProcessor 호출 시점)
        private readonly DateTime _today = new DateTime(2025, 3, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.monthlyPlayerMoraleBonus = 10;
            _balance.monthlyManagerConfidenceBonus = 5;
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T5. 월간 어워드 효과 ──────────────────────────────────────────

        [Test]
        public void T5_MonthlyAwards_EffectsApplied()
        {
            var state = new GameState { currentDate = _today };

            var club = new Club
            {
                id = 1,
                name = "Club1",
                season = new SeasonState { boardConfidence = 50 },
            };
            state.AddClub(club);

            var league = new League { id = 1, seasonYear = 2025 };
            league.clubIds.Add(club.id);
            state.leagues.Add(league);

            var player = new Player
            {
                id = 1,
                info = new PersonalInfo
                {
                    birthDate = new DateTime(1998, 1, 1),
                    primaryPosition = Position.ST,
                },
                state = new PlayerState { morale = 60, happiness = 70 },
                currentClubId = club.id,
            };
            club.seniorSquadIds.Add(player.id);
            state.AddPlayer(player);

            // 직전 월 (2월) 경기 1개
            var monthStart = _today.AddMonths(-1);
            var match = new Match
            {
                homeClubId = club.id,
                awayClubId = club.id,
                date = monthStart.AddDays(7), // 2월 8일
                result = new MatchResult { homeScore = 2, awayScore = 0 },
            };
            match.result.homeStarting11.Add(player.id);
            match.result.playerStats.Add(
                new PlayerMatchStat
                {
                    playerId = player.id,
                    goals = 2,
                    rating = 8.5f,
                    minutesPlayed = 90,
                }
            );
            league.schedule.Add(match);

            var awards = SeasonAwardSystem.ComputeMonthlyAwards(state, _balance);

            // Player of the Month → morale +10
            var potm = awards.FirstOrDefault(a => a.type == AwardType.MonthlyPlayerOfMonth);
            Assert.IsNotNull(potm, "T5: MonthlyPlayerOfMonth 발행됨");
            Assert.AreEqual(70, player.state.morale, "T5: morale +10 적용");

            // Manager of the Month → boardConfidence +5
            var motm = awards.FirstOrDefault(a => a.type == AwardType.MonthlyManagerOfMonth);
            Assert.IsNotNull(motm, "T5: MonthlyManagerOfMonth 발행됨");
            Assert.AreEqual(55, club.season.boardConfidence, "T5: boardConfidence +5 적용");
        }

    }
}
