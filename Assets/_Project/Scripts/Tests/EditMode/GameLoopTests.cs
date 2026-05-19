// GameLoopTests.cs
// DoD: Task 8.3 시간 진행 통합. T1~T6.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Application;

namespace FMLite.Tests
{
    public class GameLoopTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _start = new DateTime(2025, 8, 16);
        private List<DayAdvancedEvent> _capturedDayEvents;
        private Action<DayAdvancedEvent> _handler;

        [SetUp]
        public void Setup()
        {
            EventBus.Clear();
            GameTime.Reset(_start);
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _capturedDayEvents = new List<DayAdvancedEvent>();
            _handler = e => _capturedDayEvents.Add(e);
            EventBus.Subscribe(_handler);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_handler);
            EventBus.Clear();
        }

        private GameState MakeState(List<Match> schedule = null, int userClubId = -1, int initialFatigue = 30)
        {
            var state = new GameState { currentDate = _start, userClubId = userClubId };
            state.leagues.Add(new League
            {
                id        = 1,
                schedule  = schedule ?? new List<Match>(),
                clubIds   = new List<int> { 1, 2 },
                standings = new Standings { entries = new List<StandingEntry>() },
            });
            var p = new Player
            {
                id = 1,
                state = new PlayerState
                {
                    fatigue = initialFatigue,
                    injury  = new InjuryInfo { injuryTypeId = -1 },
                },
            };
            state.AddPlayer(p);
            return state;
        }

        private static Match Md(int id, DateTime date, int home, int away) =>
            new Match { id = id, date = date, type = CompetitionType.League, homeClubId = home, awayClubId = away };

        // ── T1. AdvanceDay 1회 — currentDate +1 + DayAdvancedEvent ────

        [Test]
        public void T1_AdvanceDay_AdvancesDateAndPublishesEvent()
        {
            var state = MakeState();

            var r = GameLoop.AdvanceDay(state, _balance);

            Assert.AreEqual(1, r.daysAdvanced);
            Assert.AreEqual(_start.AddDays(1).Date, state.currentDate.Date, "T1: currentDate +1");
            Assert.AreEqual(1, _capturedDayEvents.Count, "T1: DayAdvancedEvent 1회");
            Assert.AreEqual(state.currentDate, _capturedDayEvents[0].newDate);
        }

        // ── T2. DailyProcessor 호출 — fatigue 회복 ───────────────────

        [Test]
        public void T2_DailyProcessor_Invoked_FatigueRecovered()
        {
            var state = MakeState(initialFatigue: 30);

            GameLoop.AdvanceDay(state, _balance);

            Assert.AreEqual(15, state.allPlayers[0].state.fatigue,
                            "T2: fatigue 30 → 15 (DailyProcessor)");
        }

        // ── T3. EventScheduler 호출 — 매치 정지 신호 ──────────────────

        [Test]
        public void T3_EventScheduler_Invoked_StopOnUserMatch()
        {
            // 다음 날에 userClub 매치
            var state = MakeState(
                schedule: new List<Match> { Md(1, _start.AddDays(1), 1, 2) },
                userClubId: 1);

            var r = GameLoop.AdvanceDay(state, _balance);

            Assert.IsTrue(r.stopRequested, "T3: userClub 매치 → 정지");
        }

        // ── T4. ContinueUntilStop — 다음 정지까지 자동 진행 ───────────

        [Test]
        public void T4_ContinueUntilStop_AdvancesUntilStopEvent()
        {
            // 7일 후에 userClub 매치
            var matchDate = _start.AddDays(7);
            var state = MakeState(
                schedule: new List<Match> { Md(1, matchDate, 1, 2) },
                userClubId: 1);

            var r = GameLoop.ContinueUntilStop(state, _balance, maxDays: 30);

            Assert.IsTrue(r.stopRequested, "T4: 정지 도달");
            Assert.AreEqual(7, r.daysAdvanced, "T4: 7일 후 정지");
            Assert.AreEqual(matchDate.Date, state.currentDate.Date);
        }

        // ── T5. ContinueUntilStop maxDays 안전 가드 ───────────────────

        [Test]
        public void T5_ContinueUntilStop_MaxDaysGuard()
        {
            // 매치 없음, userClub 없음 — 영원히 정지 신호 없음
            var state = MakeState(userClubId: -1);

            var r = GameLoop.ContinueUntilStop(state, _balance, maxDays: 10);

            Assert.IsFalse(r.stopRequested, "T5: 정지 신호 없음");
            Assert.AreEqual(10, r.daysAdvanced, "T5: maxDays 만큼 진행 후 종료");
            Assert.AreEqual(_start.AddDays(10).Date, state.currentDate.Date);
        }

        // ── T6. GameTime ↔ state.currentDate 동기화 ──────────────────

        [Test]
        public void T6_GameTime_State_Synchronized()
        {
            var state = MakeState();

            GameLoop.AdvanceDay(state, _balance);
            Assert.AreEqual(GameTime.CurrentDate, state.currentDate, "T6: 1일 후 동기화");

            GameLoop.AdvanceDay(state, _balance);
            Assert.AreEqual(GameTime.CurrentDate, state.currentDate, "T6: 2일 후 동기화");
        }
    }
}
