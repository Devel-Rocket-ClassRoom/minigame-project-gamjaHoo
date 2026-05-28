// ManagerReputationTests.cs
// DoD: v1.0-tasks.md M.8 — 매니저 평판 변동

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class ManagerReputationTests
    {
        private GameBalanceSO _balance;
        private GameState _state;
        private Club _userClub;

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();

            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.managerRepLeagueWin = 20;
            _balance.managerRepMonthlyAward = 5;
            _balance.managerRepSacked = -30;
            _balance.managerRepBoardBonusDivisor = 20;
            _balance.boardSackedThreshold = 10;
            _balance.boardWarningThreshold = 30;

            _state = new GameState
            {
                currentDate = new DateTime(2025, 6, 1),
                managerReputation = 50,
            };

            _userClub = new Club
            {
                id = 1,
                season = new SeasonState { boardConfidence = 50 },
            };
            _state.AddClub(_userClub);
            _state.userClubId = 1;
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. 리그 우승 +20 ────────────────────────────────────────────

        [Test]
        public void T1_LeagueWin_AddsTwenty()
        {
            var league = new League { id = 1 };
            league.clubIds.Add(1);
            league.standings = new Standings();
            league.standings.entries.Add(new StandingEntry { clubId = 1, points = 90 });
            league.standings.entries.Add(new StandingEntry { clubId = 2, points = 60 });
            _state.leagues.Add(league);

            ManagerReputationSystem.OnLeagueWin(_state, _balance);

            Assert.AreEqual(70, _state.managerReputation, "T1: 우승 → rep +20");
        }

        // ── T2. 우승 아닐 때 rep 변동 없음 ───────────────────────────────

        [Test]
        public void T2_NoLeagueWin_NoChange()
        {
            var league = new League { id = 1 };
            league.clubIds.Add(1);
            league.standings = new Standings();
            league.standings.entries.Add(new StandingEntry { clubId = 2, points = 90 }); // 다른 팀 1위
            league.standings.entries.Add(new StandingEntry { clubId = 1, points = 60 });
            _state.leagues.Add(league);

            ManagerReputationSystem.OnLeagueWin(_state, _balance);

            Assert.AreEqual(50, _state.managerReputation, "T2: 우승 아님 → rep 변동 없음");
        }

        // ── T3. 경질 -30 ────────────────────────────────────────────────

        [Test]
        public void T3_Sacked_SubtractsThirty()
        {
            ManagerReputationSystem.OnManagerSacked(_state, _balance);

            Assert.AreEqual(20, _state.managerReputation, "T3: 경질 → rep -30");
        }

        // ── T4. 월간 어워드 +5 ──────────────────────────────────────────

        [Test]
        public void T4_MonthlyAward_AddsFive()
        {
            ManagerReputationSystem.OnMonthlyManagerAward(_state, _balance);

            Assert.AreEqual(55, _state.managerReputation, "T4: 월간 어워드 → rep +5");
        }

        // ── T5. 하한 클램프 0 ───────────────────────────────────────────

        [Test]
        public void T5_Clamp_Min_Zero()
        {
            _state.managerReputation = 5;
            ManagerReputationSystem.OnManagerSacked(_state, _balance);

            Assert.AreEqual(0, _state.managerReputation, "T5: rep 하한 0");
        }

        // ── T6. 상한 클램프 100 ─────────────────────────────────────────

        [Test]
        public void T6_Clamp_Max_100()
        {
            _state.managerReputation = 95;
            var league = new League { id = 1 };
            league.clubIds.Add(1);
            league.standings = new Standings();
            league.standings.entries.Add(new StandingEntry { clubId = 1, points = 90 });
            _state.leagues.Add(league);

            ManagerReputationSystem.OnLeagueWin(_state, _balance);

            Assert.AreEqual(100, _state.managerReputation, "T6: rep 상한 100");
        }

        // ── T7. boardConfidence 가산 효과 ────────────────────────────────

        [Test]
        public void T7_BoardBonusApplied_InEvaluateMonthly()
        {
            _state.managerReputation = 40; // rep/20 = 2 보너스
            _balance.boardConfidenceRankMultiplier = 2f;

            var league = new League { id = 1 };
            league.clubIds.Add(1);
            league.standings = new Standings();
            league.standings.entries.Add(new StandingEntry { clubId = 1, points = 10 });
            _state.leagues.Add(league);

            _userClub.season.targetLeaguePosition = 1;
            _userClub.season.boardConfidence = 50;

            BoardSystem.EvaluateMonthly(_state, _balance);

            // delta=0 (target=1, actual=1), repBonus=2
            Assert.AreEqual(52, _userClub.season.boardConfidence, "T7: rep 보너스 반영");
        }
    }
}
