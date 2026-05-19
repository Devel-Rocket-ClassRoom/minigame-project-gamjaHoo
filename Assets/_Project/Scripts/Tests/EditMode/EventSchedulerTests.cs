// EventSchedulerTests.cs
// DoD: Task 8.1 매치 분기 식별. T1~T6.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Application;

namespace FMLite.Tests
{
    public class EventSchedulerTests
    {
        private readonly DateTime _today = new DateTime(2025, 8, 16);
        private List<MatchDayEvent> _captured;
        private Action<MatchDayEvent> _handler;

        [SetUp]
        public void Setup()
        {
            EventBus.Clear();
            _captured = new List<MatchDayEvent>();
            _handler = e => _captured.Add(e);
            EventBus.Subscribe(_handler);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_handler);
            EventBus.Clear();
        }

        private GameState MakeState(List<Match> schedule, int userClubId = -1)
        {
            var state = new GameState
            {
                currentDate = _today,
                userClubId  = userClubId,
            };
            state.leagues.Add(new League
            {
                id       = 1,
                clubIds  = new List<int> { 1, 2, 3, 4 },
                schedule = schedule,
                standings = new Standings { entries = new List<StandingEntry>() },
            });
            return state;
        }

        private static Match Md(int id, DateTime date, int home, int away) =>
            new Match { id = id, date = date, type = CompetitionType.League, homeClubId = home, awayClubId = away };

        // ── T1. 오늘 매치 없음 ────────────────────────────────────────

        [Test]
        public void T1_NoMatchesToday_NoEventPublished()
        {
            var state = MakeState(new List<Match>
            {
                Md(1, _today.AddDays(7), 1, 2),
                Md(2, _today.AddDays(-3), 3, 4),
            });

            bool stop = EventScheduler.Run(state);

            Assert.IsFalse(stop, "T1: 매치 없음 → 정지 신호 없음");
            Assert.AreEqual(0, _captured.Count, "T1: 이벤트 발행 없음");
        }

        // ── T2. 매치 있음 (userClub 미참여) ──────────────────────────

        [Test]
        public void T2_MatchesToday_NotUserClub_NoStop()
        {
            var state = MakeState(new List<Match>
            {
                Md(1, _today, 1, 2),
                Md(2, _today, 3, 4),
            }, userClubId: 99);  // 99 는 schedule 에 없음

            bool stop = EventScheduler.Run(state);

            Assert.IsFalse(stop, "T2: userClub 미참여 → 정지 X");
            Assert.AreEqual(1, _captured.Count);
            Assert.IsFalse(_captured[0].isUserMatch);
            Assert.AreEqual(2, _captured[0].matchIds.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, _captured[0].matchIds);
        }

        // ── T3. 매치 있음 (userClub 출전) ─────────────────────────────

        [Test]
        public void T3_MatchesToday_UserClubPlaying_StopRequested()
        {
            var state = MakeState(new List<Match>
            {
                Md(1, _today, 1, 2),
                Md(2, _today, 3, 4),
            }, userClubId: 1);  // userClub 출전

            bool stop = EventScheduler.Run(state);

            Assert.IsTrue(stop, "T3: userClub 출전 → 정지 신호");
            Assert.AreEqual(1, _captured.Count);
            Assert.IsTrue(_captured[0].isUserMatch);
        }

        // ── T4. 다른 날짜 매치는 무시 ─────────────────────────────────

        [Test]
        public void T4_OtherDayMatches_Ignored()
        {
            var state = MakeState(new List<Match>
            {
                Md(1, _today.AddDays(-1), 1, 2),
                Md(2, _today.AddDays(1), 3, 4),
                Md(3, _today, 1, 3),                // 오늘만 1개
            }, userClubId: -1);

            bool stop = EventScheduler.Run(state);

            Assert.IsFalse(stop);
            Assert.AreEqual(1, _captured.Count);
            CollectionAssert.AreEqual(new[] { 3 }, _captured[0].matchIds);
        }

        // ── T5. userClubId == -1 (선택 전) ────────────────────────────

        [Test]
        public void T5_UserClubSentinel_IsUserMatchFalse()
        {
            var state = MakeState(new List<Match>
            {
                Md(1, _today, 1, 2),
            }, userClubId: -1);

            bool stop = EventScheduler.Run(state);

            Assert.IsFalse(stop, "T5: userClubId=-1 → 정지 X");
            Assert.IsFalse(_captured[0].isUserMatch, "T5: isUserMatch=false");
        }

        // ── T6. 빈 schedule — 이벤트 0 ────────────────────────────────

        [Test]
        public void T6_EmptySchedule_NoEvent()
        {
            var state = MakeState(new List<Match>(), userClubId: 1);

            bool stop = EventScheduler.Run(state);

            Assert.IsFalse(stop);
            Assert.AreEqual(0, _captured.Count);
        }
    }
}
