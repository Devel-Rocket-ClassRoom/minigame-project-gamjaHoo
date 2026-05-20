// ScheduleGeneratorTests.cs
// DoD: Task 7.2 더블 라운드 로빈 일정 생성. T1~T7.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class ScheduleGeneratorTests
    {
        private readonly DateTime _seasonStart = new DateTime(2025, 8, 16); // EPL 풍 8월 중순
        private const int LeagueId = 1;
        private const int StartMatchId = 1;

        private static List<int> ClubIds(int n) => Enumerable.Range(1, n).ToList();

        // ── T1. 경기 총수 (20팀 → 380경기) ───────────────────────────

        [Test]
        public void T1_TotalMatchCount_20Clubs_Equals380()
        {
            var matches = ScheduleGenerator.Generate(
                ClubIds(20),
                _seasonStart,
                LeagueId,
                StartMatchId
            );
            Assert.AreEqual(380, matches.Count, "T1: 20팀 → 380경기 (38라운드 × 10경기)");
        }

        // ── T2. 각 팀 정확히 38경기 ───────────────────────────────────

        [Test]
        public void T2_EachClubPlays38Matches_20Clubs()
        {
            var matches = ScheduleGenerator.Generate(
                ClubIds(20),
                _seasonStart,
                LeagueId,
                StartMatchId
            );
            foreach (int id in ClubIds(20))
            {
                int played = matches.Count(m => m.homeClubId == id || m.awayClubId == id);
                Assert.AreEqual(38, played, $"T2: 클럽 {id} 가 정확히 38경기");
            }
        }

        // ── T3. 각 페어 정확히 2경기 (홈/원정 각 1) ──────────────────

        [Test]
        public void T3_EachPair_OneHomeOneAway()
        {
            var matches = ScheduleGenerator.Generate(
                ClubIds(20),
                _seasonStart,
                LeagueId,
                StartMatchId
            );
            var ids = ClubIds(20);

            foreach (int a in ids)
            {
                foreach (int b in ids)
                {
                    if (a >= b)
                        continue;
                    int aHome = matches.Count(m => m.homeClubId == a && m.awayClubId == b);
                    int bHome = matches.Count(m => m.homeClubId == b && m.awayClubId == a);
                    Assert.AreEqual(1, aHome, $"T3: {a} 홈 vs {b} 원정 1경기");
                    Assert.AreEqual(1, bHome, $"T3: {b} 홈 vs {a} 원정 1경기");
                }
            }
        }

        // ── T4. 같은 라운드 내 한 팀 1경기만 ──────────────────────────

        [Test]
        public void T4_NoTeamPlaysTwiceInSameRound()
        {
            var matches = ScheduleGenerator.Generate(
                ClubIds(20),
                _seasonStart,
                LeagueId,
                StartMatchId
            );
            // 라운드 = 같은 date 그룹
            var byDate = matches.GroupBy(m => m.date);
            foreach (var round in byDate)
            {
                var teamsInRound = new List<int>();
                foreach (var m in round)
                {
                    teamsInRound.Add(m.homeClubId);
                    teamsInRound.Add(m.awayClubId);
                }
                Assert.AreEqual(
                    teamsInRound.Count,
                    teamsInRound.Distinct().Count(),
                    $"T4: 라운드 {round.Key:yyyy-MM-dd} 내 팀 중복 없음"
                );
                Assert.AreEqual(
                    20,
                    teamsInRound.Count,
                    $"T4: 라운드 {round.Key:yyyy-MM-dd} 에 20개 클럽 모두 출전"
                );
            }
        }

        // ── T5. 결정성 (같은 입력 → 같은 결과) ────────────────────────

        [Test]
        public void T5_Determinism_SameInputSameOutput()
        {
            var m1 = ScheduleGenerator.Generate(ClubIds(20), _seasonStart, LeagueId, StartMatchId);
            var m2 = ScheduleGenerator.Generate(ClubIds(20), _seasonStart, LeagueId, StartMatchId);

            Assert.AreEqual(m1.Count, m2.Count);
            for (int i = 0; i < m1.Count; i++)
            {
                Assert.AreEqual(m1[i].id, m2[i].id);
                Assert.AreEqual(m1[i].date, m2[i].date);
                Assert.AreEqual(m1[i].homeClubId, m2[i].homeClubId);
                Assert.AreEqual(m1[i].awayClubId, m2[i].awayClubId);
            }
        }

        // ── T6. 날짜 단조증가 + 라운드 간격 ───────────────────────────

        [Test]
        public void T6_DatesMonotonicNonDecreasing_AndRoundInterval()
        {
            var matches = ScheduleGenerator.Generate(
                ClubIds(20),
                _seasonStart,
                LeagueId,
                StartMatchId
            );

            for (int i = 1; i < matches.Count; i++)
                Assert.That(
                    matches[i].date,
                    Is.GreaterThanOrEqualTo(matches[i - 1].date),
                    $"T6: matches[{i}].date >= matches[{i - 1}].date"
                );

            // 라운드 = 같은 날짜. 라운드 첫 날짜들이 7일 간격
            var roundDates = matches.Select(m => m.date).Distinct().OrderBy(d => d).ToList();
            Assert.AreEqual(38, roundDates.Count, "T6: 38라운드 (=38개 distinct date)");
            for (int i = 1; i < roundDates.Count; i++)
                Assert.AreEqual(
                    7,
                    (roundDates[i] - roundDates[i - 1]).TotalDays,
                    $"T6: 라운드 {i} 와 {i - 1} 사이 7일 간격"
                );
        }

        // ── T7. 가변 팀 수 (10팀, 12팀, 8팀) ──────────────────────────

        [Test]
        public void T7_VariableClubCount_TotalMatchesEquals_N_x_NminusOne()
        {
            foreach (int n in new[] { 4, 8, 10, 12 })
            {
                var matches = ScheduleGenerator.Generate(
                    ClubIds(n),
                    _seasonStart,
                    LeagueId,
                    StartMatchId
                );
                Assert.AreEqual(
                    n * (n - 1),
                    matches.Count,
                    $"T7: n={n} → {n}*{n - 1}={n * (n - 1)} 경기"
                );

                // 각 팀이 2(n-1)경기
                foreach (int id in ClubIds(n))
                {
                    int played = matches.Count(m => m.homeClubId == id || m.awayClubId == id);
                    Assert.AreEqual(
                        2 * (n - 1),
                        played,
                        $"T7: n={n} 클럽 {id} 가 {2 * (n - 1)}경기"
                    );
                }
            }
        }

        // ── 추가: id 단조증가 / 중복 없음 ─────────────────────────────

        [Test]
        public void T8_MatchIdsUniqueAndMonotonic()
        {
            var matches = ScheduleGenerator.Generate(
                ClubIds(20),
                _seasonStart,
                LeagueId,
                StartMatchId
            );
            var ids = matches.Select(m => m.id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "T8: id 중복 없음");
            Assert.AreEqual(StartMatchId, ids.Min(), "T8: 시작 id");
            Assert.AreEqual(StartMatchId + 380 - 1, ids.Max(), "T8: 끝 id");

            for (int i = 1; i < ids.Count; i++)
                Assert.AreEqual(ids[i - 1] + 1, ids[i], $"T8: id 단조증가 (i={i})");
        }

        // ── 추가: 홀수 팀 수 예외 ─────────────────────────────────────

        [Test]
        public void T9_OddClubCount_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                ScheduleGenerator.Generate(ClubIds(19), _seasonStart, LeagueId, StartMatchId)
            );
        }
    }
}
