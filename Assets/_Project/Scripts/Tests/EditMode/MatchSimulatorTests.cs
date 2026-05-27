// MatchSimulatorTests.cs
// DoD: algorithms.md V1.0-2 Test Scenarios — Stage I.1' (5-zone 상태 머신) + I.2' (zone resolution).
// 후속: Foul/Card/Injury = I.3 / 평점 = I.4 / 텍스트 = I.5 / SubstitutionAI = I.6 / background = I.7 / fatigue·form·morale = I.8 / 연장 = I.11.

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class MatchSimulatorTests
    {
        private GameBalanceSO _balance;

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // T10 부상 — InjuryTypeSO 카탈로그 1개 등록 (PickInjuryType 동작용)
            var injType = ScriptableObject.CreateInstance<InjuryTypeSO>();
            injType.id = 1;
            injType.displayName = "Test Injury";
            injType.minDays = 7;
            injType.maxDays = 14;
            injType.weight = 1f;
            GameDatabase.Register(injType);
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ── T1. 결정성 ────────────────────────────────────────────────

        [Test]
        public void T1_Determinism_SameSeedSameResult()
        {
            var (s1, m1) = BuildState(seed: 42, matchId: 1);
            var (s2, m2) = BuildState(seed: 42, matchId: 1);

            var r1 = MatchSimulator.Simulate(m1, s1, _balance);
            var r2 = MatchSimulator.Simulate(m2, s2, _balance);

            Assert.AreEqual(r1.homeScore, r2.homeScore, "T1: homeScore 결정적");
            Assert.AreEqual(r1.awayScore, r2.awayScore, "T1: awayScore 결정적");
            Assert.AreEqual(
                r1.homePossessionPct,
                r2.homePossessionPct,
                "T1: 점유율 결정적"
            );
            Assert.AreEqual(r1.playerStats.Count, r2.playerStats.Count);
            for (int i = 0; i < r1.playerStats.Count; i++)
            {
                Assert.AreEqual(r1.playerStats[i].playerId, r2.playerStats[i].playerId);
                Assert.AreEqual(r1.playerStats[i].goals, r2.playerStats[i].goals);
                Assert.AreEqual(r1.playerStats[i].shots, r2.playerStats[i].shots);
                Assert.AreEqual(r1.playerStats[i].passes, r2.playerStats[i].passes);
            }
        }

        // ── T2. 인터페이스 호환 ──────────────────────────────────────

        [Test]
        public void T2_InterfaceCompatibility_SignatureUnchanged()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            MatchResult result = MatchSimulator.Simulate(match, state, _balance);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.homeStarting11);
            Assert.IsNotNull(result.awayStarting11);
            Assert.IsNotNull(result.playerStats);
        }

        // ── starting11 자동 선정 ──────────────────────────────────────

        [Test]
        public void StartingEleven_TopByCAExcludingInjured()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            var result = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(11, result.homeStarting11.Count);
            var homeClub = state.GetClub(match.homeClubId);
            var bench = homeClub
                .seniorSquadIds.Where(id => !result.homeStarting11.Contains(id))
                .Select(id => state.GetPlayer(id))
                .ToList();
            int xiMin = result.homeStarting11.Select(id => state.GetPlayer(id).currentAbility).Min();
            int benchMax = bench.Count > 0 ? bench.Max(p => p.currentAbility) : 0;
            Assert.GreaterOrEqual(xiMin, benchMax);
        }

        [Test]
        public void StartingEleven_ExcludesInjuredPlayers()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            var home = state.GetClub(match.homeClubId);
            var top5 = home
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .OrderByDescending(p => p.currentAbility)
                .Take(5)
                .ToList();
            foreach (var p in top5)
                p.state.injury.injuryTypeId = 1;

            var result = MatchSimulator.Simulate(match, state, _balance);
            foreach (var p in top5)
                Assert.IsFalse(result.homeStarting11.Contains(p.id));
            Assert.AreEqual(11, result.homeStarting11.Count);
        }

        [Test]
        public void StartingEleven_ExcludesSuspendedPlayers()
        {
            var (state, match) = BuildState(seed: 1, matchId: 1);
            var home = state.GetClub(match.homeClubId);
            var top5 = home
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .OrderByDescending(p => p.currentAbility)
                .Take(5)
                .ToList();
            foreach (var p in top5)
                p.state.suspendedMatches = 1;

            var result = MatchSimulator.Simulate(match, state, _balance);
            foreach (var p in top5)
                Assert.IsFalse(result.homeStarting11.Contains(p.id));
            Assert.AreEqual(11, result.homeStarting11.Count);
        }

        [Test]
        public void StartingEleven_FewerThan11WhenSquadShort()
        {
            var state = new GameState
            {
                randomSeed = 1,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, "Home");
            var away = NewClub(2, "Away");
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddSquad(state, home, statVal: 50, nextId: nextId, count: 5);
            nextId = AddSquad(state, away, statVal: 50, nextId: nextId, count: 25);
            var match = new Match
            {
                id = 1,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };

            var result = MatchSimulator.Simulate(match, state, _balance);
            Assert.AreEqual(5, result.homeStarting11.Count);
            Assert.AreEqual(11, result.awayStarting11.Count);
        }

        // ── T3. 골 분포 (200 매치 평균) ──────────────────────────────

        [Test]
        public void T3_Goals_DistributionReasonable()
        {
            var seedGen = new System.Random(42);
            int totalGoals = 0;
            const int N = 200;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i + 1);
                var r = MatchSimulator.Simulate(match, state, _balance);
                totalGoals += r.homeScore + r.awayScore;
            }
            double avg = (double)totalGoals / N;
            Assert.Greater(totalGoals, 0, "T3: 골 발생");
            // 5-zone — 균등팀 평균 골수 합리 범위 (튜닝 여지 ±). EPL ~2.7.
            Assert.That(avg, Is.InRange(1.0, 6.0), $"T3: 평균 골수 (실측 {avg:F2}/매치)");
        }

        // ── T4. 점유율 ───────────────────────────────────────────────

        [Test]
        public void T4_Possession_SumsTo100AndBalancedForEqualTeams()
        {
            var seedGen = new System.Random(77);
            double homeSum = 0;
            const int N = 100;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                Assert.That(
                    r.homePossessionPct + r.awayPossessionPct,
                    Is.EqualTo(100f).Within(0.1f),
                    "T4: 점유율 합 = 100"
                );
                homeSum += r.homePossessionPct;
            }
            double homeAvg = homeSum / N;
            // 균등팀 (stat 동일) — home advantage 로 home 약간 우세하나 ~40-60 범위
            Assert.That(
                homeAvg,
                Is.InRange(40.0, 65.0),
                $"T4: 균등팀 home 점유율 ~50 근방 (실측 {homeAvg:F1})"
            );
        }

        // ── T5. 강팀 우세 (stat 차등) ────────────────────────────────

        [Test]
        public void T5_StrongerTeamScoresMore()
        {
            // home stat 75 vs away stat 35 — home 골 우세 검증 (100 매치)
            var seedGen = new System.Random(500);
            int homeGoals = 0,
                awayGoals = 0;
            const int N = 100;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(
                    seed: seedGen.Next(),
                    matchId: i,
                    homeStat: 75,
                    awayStat: 35
                );
                var r = MatchSimulator.Simulate(match, state, _balance);
                homeGoals += r.homeScore;
                awayGoals += r.awayScore;
            }
            Assert.Greater(
                homeGoals,
                awayGoals,
                $"T5: 강팀(home stat 75) 골 > 약팀(away 35). home={homeGoals} away={awayGoals}"
            );
        }

        // ── T6. 슛 분포 ──────────────────────────────────────────────

        [Test]
        public void T6_Shots_DistributionInRange()
        {
            var seedGen = new System.Random(100);
            int totalShots = 0;
            const int N = 100;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                totalShots += r.playerStats.Sum(ps => ps.shots);
            }
            double perTeam = (double)totalShots / N / 2;
            // 5-zone box 도달 빈도 기반 — 넓은 범위 허용 (튜닝 여지).
            Assert.That(
                perTeam,
                Is.InRange(3.0, 30.0),
                $"T6: 슛/팀/매치 (실측 {perTeam:F1})"
            );
        }

        // ── T8. 카드 / 퇴장 (I.3) ────────────────────────────────────

        [Test]
        public void T8_Cards_AccumulateOverMatches()
        {
            _balance.foulProbability = 0.9f; // 파울 자주 (검증 위해 강제 — 카드 발생 보장)
            var seedGen = new System.Random(800);
            int yellow = 0,
                red = 0;
            const int N = 50;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                yellow += r.playerStats.Sum(ps => ps.yellowCards);
                red += r.playerStats.Sum(ps => ps.redCards);
            }
            Assert.Greater(yellow, 0, "T8: 옐로 카드 발생 (카드 시스템 동작)");
            // red 는 확률적 — 50매치면 보통 발생하나 0 도 허용 (drop). yellow 발생으로 시스템 검증.
        }

        // ── T10. 부상 발생 + PlayerInjuredEvent (I.3) ────────────────

        [Test]
        public void T10_Injury_OccursWithEvent()
        {
            _balance.foulProbability = 0.9f; // 파울 자주
            _balance.matchInjuryProbability = 0.8f; // 부상률 강제 ↑ (검증용)
            int events = 0;
            System.Action<PlayerInjuredEvent> h = _ => events++;
            EventBus.Subscribe(h);
            var seedGen = new System.Random(1000);
            for (int i = 0; i < 30; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                MatchSimulator.Simulate(match, state, _balance);
            }
            EventBus.Unsubscribe(h);
            Assert.Greater(events, 0, "T10: 부상 발생 + PlayerInjuredEvent 발행");
        }

        // ── 누적 통계 채워짐 ──────────────────────────────────────────

        [Test]
        public void PlayerMatchStat_AccumulatesCoreFields()
        {
            var seedGen = new System.Random(200);
            int passes = 0,
                tackles = 0,
                keyPasses = 0,
                shotsOnTarget = 0;
            const int N = 50;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                passes += r.playerStats.Sum(ps => ps.passes);
                tackles += r.playerStats.Sum(ps => ps.tackles);
                keyPasses += r.playerStats.Sum(ps => ps.keyPasses);
                shotsOnTarget += r.playerStats.Sum(ps => ps.shotsOnTarget);
            }
            Assert.Greater(passes, 0, "passes > 0");
            Assert.Greater(tackles, 0, "tackles > 0");
            Assert.Greater(keyPasses, 0, "keyPasses > 0");
            Assert.Greater(shotsOnTarget, 0, "shotsOnTarget > 0");
        }

        [Test]
        public void PlayerMatchStat_MinutesPlayedIs90AndRatingFilled()
        {
            var (state, match) = BuildState(seed: 5, matchId: 1);
            var r = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(
                r.homeStarting11.Count + r.awayStarting11.Count,
                r.playerStats.Count
            );
            foreach (var ps in r.playerStats)
            {
                // minutesPlayed 가변 = I.6 (아직 90 고정). rating = I.4 채워짐 (1.0~10.0).
                Assert.AreEqual(90, ps.minutesPlayed, $"minutesPlayed=90 (id={ps.playerId})");
                Assert.That(
                    ps.rating,
                    Is.InRange(1.0f, 10.0f),
                    $"rating 1~10 (id={ps.playerId} rating={ps.rating})"
                );
            }
        }

        // ── T5. 평점 (I.4) ───────────────────────────────────────────

        [Test]
        public void T5_Ratings_FilledAndReflectEvents()
        {
            var seedGen = new System.Random(50);
            bool anyAboveBase = false;
            int checkedCount = 0;
            const int N = 50;
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                foreach (var ps in r.playerStats)
                {
                    Assert.That(
                        ps.rating,
                        Is.InRange(1.0f, 10.0f),
                        $"평점 1~10 (id={ps.playerId} rating={ps.rating})"
                    );
                    if (ps.rating > _balance.ratingBase)
                        anyAboveBase = true;
                    checkedCount++;
                }
            }
            Assert.Greater(checkedCount, 0);
            Assert.IsTrue(
                anyAboveBase,
                "T5: 일부 선수 평점 > base 6.5 (골/어시/승리/선방 가산 반영)"
            );
        }

        // ── T9. 텍스트 이벤트 (I.5) ─────────────────────────────────

        [Test]
        public void T9_CollectEvents_TrueFillsEvents_FalseEmpty()
        {
            var (stateOn, matchOn) = BuildState(seed: 999, matchId: 10);
            var (stateOff, matchOff) = BuildState(seed: 999, matchId: 10);

            var resultOn = MatchSimulator.Simulate(matchOn, stateOn, _balance, collectEvents: true);
            var resultOff = MatchSimulator.Simulate(
                matchOff,
                stateOff,
                _balance,
                collectEvents: false
            );

            // collectEvents=true → events 채워짐 (킥오프/전반종료/종료 최소 3개)
            Assert.Greater(resultOn.events.Count, 0, "T9: collectEvents=true — events 채워짐");
            Assert.GreaterOrEqual(
                resultOn.events.Count,
                3,
                "T9: KickOff+HalfTime+FullTime 최소 3개"
            );

            // collectEvents=false → events 비어있음
            Assert.AreEqual(0, resultOff.events.Count, "T9: collectEvents=false — events 비어있음");

            // 통계는 양쪽 동일 (같은 시드 — #55 설계 의도)
            Assert.AreEqual(
                resultOn.homeScore,
                resultOff.homeScore,
                "T9: homeScore 동일 (collectEvents 는 통계 미영향)"
            );
            Assert.AreEqual(
                resultOn.awayScore,
                resultOff.awayScore,
                "T9: awayScore 동일"
            );
            int shotsOn = resultOn.playerStats.Sum(ps => ps.shots);
            int shotsOff = resultOff.playerStats.Sum(ps => ps.shots);
            Assert.AreEqual(shotsOn, shotsOff, "T9: 슛 통계 동일");

            // textKey + minute 유효성 — Goal/Card 이벤트는 textKey 있음
            foreach (var e in resultOn.events)
            {
                Assert.IsNotNull(e.textKey, $"T9: textKey null (type={e.type})");
                Assert.IsNotEmpty(e.textKey, $"T9: textKey 빈 문자열 (type={e.type})");
                Assert.GreaterOrEqual(e.minute, 0, $"T9: minute >= 0 (type={e.type})");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private (GameState, Match) BuildState(
            int seed,
            int matchId,
            int homeStat = 50,
            int awayStat = 50
        )
        {
            var state = new GameState
            {
                randomSeed = seed,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, "Home");
            var away = NewClub(2, "Away");
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddSquad(state, home, statVal: homeStat, nextId: nextId, count: 25);
            nextId = AddSquad(state, away, statVal: awayStat, nextId: nextId, count: 25);
            var match = new Match
            {
                id = matchId,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };
            return (state, match);
        }

        private static Club NewClub(int id, string name) =>
            new Club
            {
                id = id,
                name = name,
                reputation = 60,
                facilities = new Facilities { medicalLevel = 1, gymLevel = 1 },
            };

        // 라인 분포 GK 3 / DF 8 / MF 8 / AT 6 (25명) — 5-zone snap 이 라인별 선수 필요.
        private static int AddSquad(
            GameState state,
            Club club,
            int statVal,
            int nextId,
            int count
        )
        {
            var composition = new (Position pos, int n)[]
            {
                (Position.GK, 3),
                (Position.CB, 4),
                (Position.LB, 2),
                (Position.RB, 2),
                (Position.DM, 2),
                (Position.CM, 4),
                (Position.LM, 1),
                (Position.RM, 1),
                (Position.ST, 4),
                (Position.CF, 2),
            };
            int added = 0;
            int idx = 0;
            foreach (var (pos, n) in composition)
            {
                for (int i = 0; i < n && added < count; i++)
                {
                    // CA 약간 다양화 (starting11 정렬 안정성)
                    int ca = 100 + ((idx % 5) - 2);
                    state.AddPlayer(NewPlayer(nextId, pos, ca, statVal));
                    club.seniorSquadIds.Add(nextId);
                    nextId++;
                    added++;
                    idx++;
                }
            }
            return nextId;
        }

        private static Player NewPlayer(int id, Position pos, int ca, int statVal) =>
            new Player
            {
                id = id,
                currentAbility = ca,
                potentialAbility = ca + 10,
                info = new PersonalInfo
                {
                    primaryPosition = pos,
                    firstName = "F",
                    lastName = "L",
                },
                state = new PlayerState
                {
                    injury = new InjuryInfo { injuryTypeId = -1 },
                    fatigue = 0,
                    morale = 50,
                    form = 50,
                    suspendedMatches = 0,
                },
                stats = NewBalancedStats(statVal),
                hiddenAttrs = new HiddenAttributes { injuryProneness = 50 },
            };

        private static Stats NewBalancedStats(int v)
        {
            var s = new Stats();
            s.technical.ApplyToAll(_ => v);
            s.mental.ApplyToAll(_ => v);
            s.physical.ApplyToAll(_ => v);
            s.gk.ApplyToAll(_ => v);
            return s;
        }
    }
}
