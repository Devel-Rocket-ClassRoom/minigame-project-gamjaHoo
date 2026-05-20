// SeasonProcessorsTests.cs
// DoD: data-flows.md #6 V0.1 시퀀스. 이슈 #44 (SeasonEndProcessor) / #45 (NewSeasonProcessor).

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
    public class SeasonProcessorsTests
    {
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        // ── SeasonEndProcessor ──────────────────────────────────────

        [Test]
        public void T1_SeasonEnd_ExpiredContracts_BecomeFA()
        {
            var state = BuildState(seasonEndDate: new DateTime(2026, 5, 15));
            var club = state.GetClub(1);
            // 만료 (endDate <= currentDate) 선수 1명, 미만료 1명
            var expired = NewPlayer(1, age: 25);
            expired.contract = new Contract
            {
                startDate = new DateTime(2023, 1, 1),
                endDate = new DateTime(2026, 1, 1),
            };
            expired.currentClubId = club.id;
            var active = NewPlayer(2, age: 25);
            active.contract = new Contract
            {
                startDate = state.currentDate,
                endDate = state.currentDate.AddYears(2),
            };
            active.currentClubId = club.id;
            state.AddPlayer(expired);
            state.AddPlayer(active);
            club.seniorSquadIds.AddRange(new[] { 1, 2 });

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(-1, expired.currentClubId, "T1: 만료 선수 FA 전환");
            Assert.IsFalse(club.seniorSquadIds.Contains(1), "T1: 만료 선수 squad 제거");
            Assert.AreEqual(club.id, active.currentClubId, "T1: 미만료 선수 그대로");
            Assert.IsTrue(club.seniorSquadIds.Contains(2), "T1: 미만료 squad 유지");
        }

        [Test]
        public void T2_SeasonEnd_Retirement_33PlusEventually()
        {
            // 통계 검증 — 33+ 선수 100명, retirementProbabilityPerYear=0.15 → 약 15명 은퇴 예상 (±5)
            var state = BuildState(seasonEndDate: new DateTime(2026, 5, 15));
            var club = state.GetClub(1);
            for (int i = 1; i <= 100; i++)
            {
                var p = NewPlayer(i, age: 35); // 35세 (>= 33)
                p.currentClubId = club.id;
                p.contract = new Contract
                {
                    startDate = state.currentDate,
                    endDate = state.currentDate.AddYears(2),
                };
                state.AddPlayer(p);
                club.seniorSquadIds.Add(i);
            }
            int beforeCount = state.allPlayers.Count;

            SeasonEndProcessor.Run(state, _balance);

            int afterCount = state.allPlayers.Count;
            int retired = beforeCount - afterCount;
            // P(retire) = 0.15 → 100 시 평균 15 ±5. 5 <= retired <= 25 범위
            Assert.That(
                retired,
                Is.InRange(5, 30),
                $"T2: 33+ 100명 중 은퇴자 5~30 (이론 15, 실측 {retired})"
            );
        }

        [Test]
        public void T3_SeasonEnd_Under33_NoRetirement()
        {
            // 32세 선수는 절대 은퇴 안 함
            var state = BuildState(seasonEndDate: new DateTime(2026, 5, 15));
            var club = state.GetClub(1);
            for (int i = 1; i <= 50; i++)
            {
                var p = NewPlayer(i, age: 32); // 32세 (< 33)
                p.currentClubId = club.id;
                p.contract = new Contract
                {
                    startDate = state.currentDate,
                    endDate = state.currentDate.AddYears(2),
                };
                state.AddPlayer(p);
                club.seniorSquadIds.Add(i);
            }
            int before = state.allPlayers.Count;

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(before, state.allPlayers.Count, "T3: 33 미만 은퇴 X");
        }

        [Test]
        public void T4_SeasonEnd_PublishesEvent()
        {
            var state = BuildState(seasonEndDate: new DateTime(2026, 5, 15));
            int count = 0;
            Action<SeasonEndedEvent> h = _ => count++;
            EventBus.Subscribe(h);

            SeasonEndProcessor.Run(state, _balance);

            Assert.AreEqual(1, count, "T4: SeasonEndedEvent 1회 발행");
            EventBus.Unsubscribe(h);
        }

        // ── NewSeasonProcessor ──────────────────────────────────────

        [Test]
        public void T5_NewSeason_TokensGranted_WithCap()
        {
            var state = BuildState(seasonEndDate: new DateTime(2026, 5, 15));
            state.currentDate = new DateTime(2026, 6, 1);
            state.rerollTokens = 1;

            NewSeasonProcessor.Run(state, _balance);

            // 1 + 3 = 4 (max 5 미만)
            Assert.AreEqual(4, state.rerollTokens, "T5: 토큰 1 + 3 = 4");

            // Cap 테스트 — 3 → 6 (cap 5)
            state.rerollTokens = 3;
            NewSeasonProcessor.Run(state, _balance);
            Assert.AreEqual(5, state.rerollTokens, "T5: 토큰 3+3=6 → cap 5");
        }

        [Test]
        public void T6_NewSeason_FatigueAndFormReset()
        {
            var state = BuildState(seasonEndDate: new DateTime(2026, 5, 15));
            state.currentDate = new DateTime(2026, 6, 1);
            var club = state.GetClub(1);
            for (int i = 1; i <= 5; i++)
            {
                var p = NewPlayer(i, age: 25);
                p.state.fatigue = 80;
                p.state.form = 30;
                state.AddPlayer(p);
                club.seniorSquadIds.Add(i);
            }

            NewSeasonProcessor.Run(state, _balance);

            foreach (var p in state.allPlayers)
            {
                Assert.AreEqual(0, p.state.fatigue, $"T6: id={p.id} fatigue 리셋");
                Assert.AreEqual(50, p.state.form, $"T6: id={p.id} form 리셋");
            }
        }

        [Test]
        public void T7_NewSeason_StandingsReset_AndScheduleGenerated()
        {
            // 4팀 리그 + 기존 시즌 진행 상태
            var state = new GameState
            {
                currentDate = new DateTime(2026, 6, 1),
                randomSeed = 42,
                userClubId = -1,
                nextPlayerId = 1,
            };
            for (int i = 1; i <= 4; i++)
            {
                var c = new Club
                {
                    id = i,
                    name = $"C{i}",
                    reputation = 100 - (i * 5),
                    leagueId = 1,
                    finance = new Finance(),
                };
                state.AddClub(c);
            }
            var league = new League
            {
                id = 1,
                seasonYear = 2025,
                clubIds = new List<int> { 1, 2, 3, 4 },
                schedule = new List<Match>(),
                standings = new Standings
                {
                    entries = new List<StandingEntry>
                    {
                        new StandingEntry
                        {
                            clubId = 1,
                            played = 6,
                            won = 4,
                            points = 12,
                        },
                        new StandingEntry
                        {
                            clubId = 2,
                            played = 6,
                            won = 2,
                            points = 6,
                        },
                        new StandingEntry
                        {
                            clubId = 3,
                            played = 6,
                            won = 3,
                            points = 9,
                        },
                        new StandingEntry
                        {
                            clubId = 4,
                            played = 6,
                            won = 1,
                            points = 3,
                        },
                    },
                },
            };
            state.leagues.Add(league);

            NewSeasonProcessor.Run(state, _balance);

            Assert.AreEqual(2026, league.seasonYear, "T7: seasonYear +1");
            Assert.AreEqual(4, league.standings.entries.Count, "T7: Standings 재구성 (4 클럽)");
            foreach (var e in league.standings.entries)
            {
                Assert.AreEqual(0, e.played, $"T7: entry.played 0");
                Assert.AreEqual(0, e.points, $"T7: entry.points 0");
            }

            // 4팀 더블 라운드 로빈 = 12 경기
            Assert.AreEqual(12, league.schedule.Count, "T7: 새 일정 12 경기");
            DateTime opening = new DateTime(
                2026,
                _balance.newSeasonOpeningMonth,
                _balance.newSeasonOpeningDay
            );
            Assert.AreEqual(
                opening,
                league.schedule[0].date.Date,
                "T7: 첫 매치 newSeasonOpening 날짜"
            );
        }

        [Test]
        public void T8_NewSeason_ClubSeasonTargets_RepRanking()
        {
            var state = new GameState { currentDate = new DateTime(2026, 6, 1), randomSeed = 1 };
            // rep 90 / 70 / 50 / 30 — 명성 순위 1/2/3/4
            var c1 = new Club
            {
                id = 1,
                name = "Top",
                reputation = 90,
                leagueId = 1,
                finance = new Finance(),
            };
            var c2 = new Club
            {
                id = 2,
                name = "High",
                reputation = 70,
                leagueId = 1,
                finance = new Finance(),
            };
            var c3 = new Club
            {
                id = 3,
                name = "Mid",
                reputation = 50,
                leagueId = 1,
                finance = new Finance(),
            };
            var c4 = new Club
            {
                id = 4,
                name = "Low",
                reputation = 30,
                leagueId = 1,
                finance = new Finance(),
            };
            state.AddClub(c1);
            state.AddClub(c2);
            state.AddClub(c3);
            state.AddClub(c4);
            var league = new League
            {
                id = 1,
                seasonYear = 2025,
                clubIds = new List<int> { 1, 2, 3, 4 },
                schedule = new List<Match>(),
                standings = new Standings(),
            };
            state.leagues.Add(league);

            NewSeasonProcessor.Run(state, _balance);

            Assert.AreEqual(1, c1.season.targetLeaguePosition, "T8: rep 90 → 1위 목표");
            Assert.AreEqual(2, c2.season.targetLeaguePosition);
            Assert.AreEqual(3, c3.season.targetLeaguePosition);
            Assert.AreEqual(4, c4.season.targetLeaguePosition);
            Assert.AreEqual(
                _balance.initialBoardConfidence,
                c1.season.boardConfidence,
                "T8: boardConfidence 초기화"
            );
        }

        [Test]
        public void T9_NewSeason_PublishesEvent()
        {
            var state = BuildState(seasonEndDate: new DateTime(2026, 5, 15));
            state.currentDate = new DateTime(2026, 6, 1);
            int count = 0;
            Action<SeasonStartedEvent> h = _ => count++;
            EventBus.Subscribe(h);

            NewSeasonProcessor.Run(state, _balance);

            Assert.AreEqual(1, count, "T9: SeasonStartedEvent 1회 발행");
            EventBus.Unsubscribe(h);
        }

        // ── Helpers ──────────────────────────────────────────────────

        private GameState BuildState(DateTime seasonEndDate)
        {
            var state = new GameState
            {
                currentDate = seasonEndDate,
                randomSeed = 42,
                userClubId = -1,
                rerollTokens = 0,
                nextPlayerId = 1,
            };
            // 4 클럽 (T7 외 테스트용 — 단순 1 클럽)
            for (int i = 1; i <= 4; i++)
            {
                var c = new Club
                {
                    id = i,
                    name = $"C{i}",
                    reputation = 100 - (i * 10),
                    leagueId = 1,
                    finance = new Finance(),
                };
                state.AddClub(c);
            }
            var league = new League
            {
                id = 1,
                seasonYear = 2025,
                clubIds = new List<int> { 1, 2, 3, 4 },
                schedule = new List<Match>(),
                standings = new Standings
                {
                    entries = new List<StandingEntry>
                    {
                        new StandingEntry { clubId = 1 },
                        new StandingEntry { clubId = 2 },
                        new StandingEntry { clubId = 3 },
                        new StandingEntry { clubId = 4 },
                    },
                },
            };
            state.leagues.Add(league);
            return state;
        }

        private Player NewPlayer(int id, int age)
        {
            var refDate = new DateTime(2026, 5, 15);
            return new Player
            {
                id = id,
                currentAbility = 100,
                potentialAbility = 100,
                info = new PersonalInfo
                {
                    primaryPosition = Position.CM,
                    firstName = "F",
                    lastName = "L",
                    birthDate = new DateTime(refDate.Year - age, refDate.Month, refDate.Day),
                },
                state = new PlayerState
                {
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    fatigue = 0,
                    morale = 50,
                    form = 50,
                },
                contract = new Contract
                {
                    weeklyWage = 50_000,
                    startDate = refDate.AddYears(-2),
                    endDate = refDate.AddYears(2),
                },
                currentClubId = 1,
            };
        }
    }
}
