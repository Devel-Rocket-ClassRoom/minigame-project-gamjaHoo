// BoardSystemTests.cs
// DoD: v1.0-tasks.md M.4 — 보드 평가 + 경질

using System;
using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class BoardSystemTests
    {
        private GameBalanceSO _balance;
        private GameState _state;
        private Club _userClub;
        private League _league;

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();

            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.boardConfidenceLossPerDefeat = 2;
            _balance.boardConfidenceBigMatchLossExtra = 3;
            _balance.boardConfidenceWinGain = 1;
            _balance.boardConfidenceRankMultiplier = 2f;
            _balance.boardWarningThreshold = 30;
            _balance.boardSackedThreshold = 10;
            _balance.bigMatchReputationThreshold = 75;

            _state = new GameState { currentDate = new DateTime(2025, 3, 15) };

            _userClub = new Club
            {
                id = 1,
                name = "UserClub",
                reputation = 60,
                season = new SeasonState { boardConfidence = 50, targetLeaguePosition = 3 },
            };
            _state.AddClub(_userClub);
            _state.userClubId = _userClub.id;

            _league = new League { id = 1, seasonYear = 2025 };
            _league.clubIds.Add(_userClub.id);
            _league.standings = new Standings();
            _league.standings.entries.Add(
                new StandingEntry { clubId = _userClub.id, points = 60 }
            );
            _state.leagues.Add(_league);
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. 패배 시 boardConfidence 감소 ─────────────────────────────

        [Test]
        public void T1_Loss_DecreasesConfidence()
        {
            var opp = AddOpponentClub(id: 2, reputation: 50);
            var match = MakeMatch(homeClubId: _userClub.id, awayClubId: opp.id, homeScore: 0, awayScore: 1);

            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.AreEqual(48, _userClub.season.boardConfidence, "T1: 패배 → -2");
        }

        // ── T2. 빅매치 패배 시 추가 감소 ─────────────────────────────────

        [Test]
        public void T2_BigMatchLoss_DecreasesMoreConfidence()
        {
            var opp = AddOpponentClub(id: 2, reputation: 80); // >= bigMatchReputationThreshold
            var match = MakeMatch(homeClubId: _userClub.id, awayClubId: opp.id, homeScore: 0, awayScore: 1);

            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.AreEqual(45, _userClub.season.boardConfidence, "T2: 빅매치 패배 → -5");
        }

        // ── T3. 승리 시 boardConfidence 증가 ─────────────────────────────

        [Test]
        public void T3_Win_IncreasesConfidence()
        {
            var opp = AddOpponentClub(id: 2, reputation: 50);
            var match = MakeMatch(homeClubId: _userClub.id, awayClubId: opp.id, homeScore: 2, awayScore: 0);

            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.AreEqual(51, _userClub.season.boardConfidence, "T3: 승리 → +1");
        }

        // ── T4. 무승부 시 변동 없음 ───────────────────────────────────────

        [Test]
        public void T4_Draw_NoChange()
        {
            var opp = AddOpponentClub(id: 2, reputation: 50);
            var match = MakeMatch(homeClubId: _userClub.id, awayClubId: opp.id, homeScore: 1, awayScore: 1);

            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.AreEqual(50, _userClub.season.boardConfidence, "T4: 무승부 → 변동 없음");
        }

        // ── T5. 유저 구단 아닌 매치는 무시 ───────────────────────────────

        [Test]
        public void T5_NonUserMatch_Ignored()
        {
            var club2 = AddOpponentClub(id: 2, reputation: 50);
            var club3 = AddOpponentClub(id: 3, reputation: 50);
            var match = MakeMatch(homeClubId: club2.id, awayClubId: club3.id, homeScore: 0, awayScore: 3);

            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.AreEqual(50, _userClub.season.boardConfidence, "T5: 비유저 매치 → 변동 없음");
        }

        // ── T6. 월간 평가 — 목표보다 높은 순위 → 신뢰도 상승 ────────────

        [Test]
        public void T6_MonthlyEval_AboveTarget_IncreasesConfidence()
        {
            // userClub 1위 (actual=1), target=3 → delta=2 → +4
            var club2 = AddOpponentClub(id: 2, reputation: 50);
            _league.clubIds.Add(club2.id);
            _league.standings.entries[0].points = 80;
            _league.standings.entries.Add(new StandingEntry { clubId = club2.id, points = 40 });

            BoardSystem.EvaluateMonthly(_state, _balance);

            Assert.AreEqual(54, _userClub.season.boardConfidence, "T6: target=3, actual=1 → delta=2 → +4");
        }

        // ── T7. 월간 평가 — 목표보다 낮은 순위 → 신뢰도 하락 ────────────

        [Test]
        public void T7_MonthlyEval_BelowTarget_DecreasesConfidence()
        {
            // userClub 2위 (actual=2), target=1 → delta=-1 → -2
            var club2 = AddOpponentClub(id: 2, reputation: 50);
            _league.clubIds.Add(club2.id);
            _league.standings.entries[0].points = 40;  // userClub: 40점 (2위)
            _league.standings.entries.Add(new StandingEntry { clubId = club2.id, points = 80 }); // club2: 1위

            _userClub.season.targetLeaguePosition = 1;

            BoardSystem.EvaluateMonthly(_state, _balance);

            Assert.AreEqual(48, _userClub.season.boardConfidence, "T7: target=1, actual=2 → delta=-1 → -2");
        }

        // ── T8. boardConfidence < 30 → BoardWarningEvent ─────────────────

        [Test]
        public void T8_LowConfidence_FiresBoardWarningEvent()
        {
            _userClub.season.boardConfidence = 31;
            BoardWarningEvent received = null;
            EventBus.Subscribe<BoardWarningEvent>(e => received = e);

            var opp = AddOpponentClub(id: 2, reputation: 50);
            var match = MakeMatch(homeClubId: _userClub.id, awayClubId: opp.id, homeScore: 0, awayScore: 1);
            // 31 - 2 = 29 < 30
            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.IsNotNull(received, "T8: BoardWarningEvent 발행됨");
            Assert.AreEqual(29, received.boardConfidence);
        }

        // ── T9. boardConfidence < 10 → ManagerSackedEvent ────────────────

        [Test]
        public void T9_VeryLowConfidence_FiresManagerSackedEvent()
        {
            _userClub.season.boardConfidence = 11;
            ManagerSackedEvent received = null;
            EventBus.Subscribe<ManagerSackedEvent>(e => received = e);

            var opp = AddOpponentClub(id: 2, reputation: 50);
            var match = MakeMatch(homeClubId: _userClub.id, awayClubId: opp.id, homeScore: 0, awayScore: 1);
            // 11 - 2 = 9 < 10
            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.IsNotNull(received, "T9: ManagerSackedEvent 발행됨");
            Assert.AreEqual(9, received.boardConfidence);
        }

        // ── T10. confidence 0 미만으로 떨어지지 않음 ─────────────────────

        [Test]
        public void T10_Confidence_ClampsToZero()
        {
            _userClub.season.boardConfidence = 1;
            var opp = AddOpponentClub(id: 2, reputation: 50);
            var match = MakeMatch(homeClubId: _userClub.id, awayClubId: opp.id, homeScore: 0, awayScore: 1);

            BoardSystem.ProcessMatchResult(_state, _balance, match, _league);

            Assert.GreaterOrEqual(_userClub.season.boardConfidence, 0, "T10: confidence >= 0");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private Club AddOpponentClub(int id, int reputation)
        {
            var club = new Club
            {
                id = id,
                name = $"Club{id}",
                reputation = reputation,
                season = new SeasonState { boardConfidence = 50 },
            };
            _state.AddClub(club);
            return club;
        }

        private static Match MakeMatch(int homeClubId, int awayClubId, int homeScore, int awayScore)
        {
            return new Match
            {
                homeClubId = homeClubId,
                awayClubId = awayClubId,
                date = new DateTime(2025, 3, 15),
                result = new MatchResult { homeScore = homeScore, awayScore = awayScore },
            };
        }
    }
}
