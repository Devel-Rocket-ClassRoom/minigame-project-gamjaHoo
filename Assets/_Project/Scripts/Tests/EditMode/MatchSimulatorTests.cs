// MatchSimulatorTests.cs
// DoD: algorithms.md #2 Test Scenarios T1~T7.

using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
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
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            // 명시적 재확인 (SO 기본값) — 명세와 일치 보장
            _balance.avgGoalsPerMatch = 2.70f;
            _balance.homeAdvantageGoalBonus = 0.30f;
            _balance.scoringWeightByLine = new[] { 0.0f, 0.4f, 1.5f, 5.0f };
        }

        // ── T1. 결정성 ────────────────────────────────────────────────

        [Test]
        public void T1_Determinism_SameSeedSameResult()
        {
            var (state1, match1) = BuildState(homeCA: 110, awayCA: 110, seed: 42, matchId: 1);
            var (state2, match2) = BuildState(homeCA: 110, awayCA: 110, seed: 42, matchId: 1);

            var r1 = MatchSimulator.Simulate(match1, state1, _balance);
            var r2 = MatchSimulator.Simulate(match2, state2, _balance);

            Assert.AreEqual(r1.homeScore, r2.homeScore, "T1: homeScore 결정적");
            Assert.AreEqual(r1.awayScore, r2.awayScore, "T1: awayScore 결정적");
            CollectionAssert.AreEqual(
                r1.homeStarting11,
                r2.homeStarting11,
                "T1: homeStarting11 결정적"
            );
            CollectionAssert.AreEqual(
                r1.awayStarting11,
                r2.awayStarting11,
                "T1: awayStarting11 결정적"
            );
            Assert.AreEqual(
                r1.playerStats.Count,
                r2.playerStats.Count,
                "T1: playerStats count 결정적"
            );
            for (int i = 0; i < r1.playerStats.Count; i++)
            {
                Assert.AreEqual(
                    r1.playerStats[i].playerId,
                    r2.playerStats[i].playerId,
                    $"T1: playerStats[{i}].playerId"
                );
                Assert.AreEqual(
                    r1.playerStats[i].goals,
                    r2.playerStats[i].goals,
                    $"T1: playerStats[{i}].goals"
                );
            }

            // 다른 matchId → 다른 결과 (대부분 케이스, 시드 충돌 위험 낮음)
            var (state3, match3) = BuildState(homeCA: 110, awayCA: 110, seed: 42, matchId: 999);
            var r3 = MatchSimulator.Simulate(match3, state3, _balance);
            bool anyDiff = r1.homeScore != r3.homeScore || r1.awayScore != r3.awayScore;
            Assert.IsTrue(anyDiff, "T1: 다른 matchId → 일반적으로 다른 결과");
        }

        // ── T2. starting11 선정 (top-11 by CA + 부상자 제외) ──────────

        [Test]
        public void T2_StartingEleven_TopByCAExcludingInjured()
        {
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
            // home 구단 25명 — top-11 CA 가 starting11 에 들어와야 함
            var result = MatchSimulator.Simulate(match, state, _balance);

            Assert.AreEqual(11, result.homeStarting11.Count, "T2: 25명 스쿼드 → starting11 = 11");
            var homeClub = state.GetClub(match.homeClubId);
            var benchPlayers = homeClub
                .seniorSquadIds.Where(id => !result.homeStarting11.Contains(id))
                .Select(id => state.GetPlayer(id))
                .ToList();
            int starting11MinCA = result
                .homeStarting11.Select(id => state.GetPlayer(id).currentAbility)
                .Min();
            int benchMaxCA = benchPlayers.Count > 0 ? benchPlayers.Max(p => p.currentAbility) : 0;
            Assert.GreaterOrEqual(
                starting11MinCA,
                benchMaxCA,
                "T2: starting11 의 최저 CA >= 벤치 최고 CA (top-by-CA 정렬)"
            );
        }

        [Test]
        public void T2b_StartingEleven_ExcludesInjuredPlayers()
        {
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 1, matchId: 1);
            // home 구단의 top-5 by CA 를 모두 부상 처리 → starting11 에 들어가면 안 됨
            var home = state.GetClub(match.homeClubId);
            var top5 = home
                .seniorSquadIds.Select(id => state.GetPlayer(id))
                .OrderByDescending(p => p.currentAbility)
                .Take(5)
                .ToList();
            foreach (var p in top5)
                p.state.injury.injuryTypeId = 1; // 부상 상태

            var result = MatchSimulator.Simulate(match, state, _balance);
            foreach (var p in top5)
                Assert.IsFalse(
                    result.homeStarting11.Contains(p.id),
                    $"T2b: 부상자 (id={p.id}) 는 starting11 에 포함되면 안 됨"
                );
            Assert.AreEqual(11, result.homeStarting11.Count, "T2b: 가용 인원 충분 → 여전히 11명");
        }

        [Test]
        public void T2c_StartingEleven_FewerThan11WhenSquadShort()
        {
            // 스쿼드 5명만 — starting11.Count = 5 가 정상 (Edge case)
            var state = new GameState
            {
                randomSeed = 1,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, name: "Home", squadSize: 5, ca: 100);
            var away = NewClub(2, name: "Away", squadSize: 25, ca: 100);
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddPlayersToClub(state, home, ca: 100, nextId: nextId, count: 5);
            nextId = AddPlayersToClub(state, away, ca: 100, nextId: nextId, count: 25);
            var match = new Match
            {
                id = 1,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };

            var result = MatchSimulator.Simulate(match, state, _balance);
            Assert.AreEqual(5, result.homeStarting11.Count, "T2c: 스쿼드 5명 → starting11 5명");
            Assert.AreEqual(11, result.awayStarting11.Count, "T2c: away 정상 11명");
        }

        // ── T3. 강팀 승률 ─────────────────────────────────────────────

        [Test]
        public void T3_StrongTeamWinRate_OverThreshold()
        {
            // 강팀 CA 합 ~1700 (170×10+) / 약팀 ~900 (90×10+) — 200매치 시뮬 (시드 well-distributed).
            //
            // strengthExponent=1.5 기준 정규근사:
            //   s_h^1.5 = 1700^1.5 ≈ 70100, s_w^1.5 = 900^1.5 ≈ 27000
            //   strengthRatio ≈ 70100 / 97100 ≈ 0.722
            //
            // 강팀 홈:
            //   λ_strong_home = 2.7 × 0.722 + 0.3 = 2.25
            //   λ_weak_away   = 2.7 × 0.278       = 0.75
            //   E[D]=1.50, σ≈√3≈1.73 → P(D≥1) ≈ Φ((1.50-0.5)/1.73) ≈ 72%
            // 표본오차 마진 7%p → 임계치 65%.
            int strongWinsAsHome = SimulateManyMatches(
                strongCA: 170,
                weakCA: 90,
                strongIsHome: true,
                matchCount: 200,
                globalSeed: 100
            );
            Assert.GreaterOrEqual(
                strongWinsAsHome,
                130,
                $"T3a: 강팀 홈 200매치 승률 ≥ 65% (= 130/200, 이론 ~72%). 실측 {strongWinsAsHome}/200"
            );

            // 강팀 원정:
            //   λ_strong_away = 2.7 × 0.722       = 1.95
            //   λ_weak_home   = 2.7 × 0.278 + 0.3 = 1.05
            //   E[D]=0.90 → P(D≥1) ≈ Φ((0.90-0.5)/1.73) ≈ 59%
            // 표본오차 마진 9%p → 임계치 50%.
            int strongWinsAsAway = SimulateManyMatches(
                strongCA: 170,
                weakCA: 90,
                strongIsHome: false,
                matchCount: 200,
                globalSeed: 200
            );
            Assert.GreaterOrEqual(
                strongWinsAsAway,
                100,
                $"T3b: 강팀 원정 200매치 승률 ≥ 50% (= 100/200, 이론 ~59%). 실측 {strongWinsAsAway}/200"
            );
        }

        // ── T4. 동급 팀 — 홈 어드밴티지 ───────────────────────────────

        [Test]
        public void T4_EqualTeams_HomeAdvantageShowsInWinRates()
        {
            // λ_home=1.65 / λ_away=1.35. P(home>away)≈45% / draw≈22% / P(away>home)≈33% 기대 (Skellam)
            var seedGen = new System.Random(10_000);
            int homeWins = 0,
                draws = 0,
                awayWins = 0;
            for (int i = 0; i < 1000; i++)
            {
                int matchSeed = seedGen.Next();
                var (state, match) = BuildState(
                    homeCA: 100,
                    awayCA: 100,
                    seed: matchSeed,
                    matchId: i
                );
                var r = MatchSimulator.Simulate(match, state, _balance);
                if (r.homeScore > r.awayScore)
                    homeWins++;
                else if (r.homeScore < r.awayScore)
                    awayWins++;
                else
                    draws++;
            }
            Assert.Greater(
                homeWins,
                awayWins,
                $"T4: 동급 팀이면 홈 어드밴티지로 홈 승률 > 원정 승률. home={homeWins} draw={draws} away={awayWins}"
            );
        }

        // ── T5. 골 분포 통계 ──────────────────────────────────────────

        [Test]
        public void T5_GoalDistribution_MatchesExpectedRanges()
        {
            // 동급 팀, λ_home=1.65 / λ_away=1.35, total λ = 3.0
            // P(home=0)*P(away=0) = e^(-3.0) ≈ 4.98% → 무득점 약 5%
            var seedGen = new System.Random(20_000);
            int totalGoals = 0;
            int scorelessMatches = 0;
            int highScoringMatches = 0;
            const int N = 1000;
            for (int i = 0; i < N; i++)
            {
                int matchSeed = seedGen.Next();
                var (state, match) = BuildState(
                    homeCA: 100,
                    awayCA: 100,
                    seed: matchSeed,
                    matchId: i
                );
                var r = MatchSimulator.Simulate(match, state, _balance);
                int goals = r.homeScore + r.awayScore;
                totalGoals += goals;
                if (goals == 0)
                    scorelessMatches++;
                if (goals >= 5)
                    highScoringMatches++;
            }
            double avgGoals = (double)totalGoals / N;
            double scorelessRate = (double)scorelessMatches / N;
            double highScoringRate = (double)highScoringMatches / N;

            Assert.That(
                avgGoals,
                Is.EqualTo(3.0).Within(0.2),
                $"T5: 평균 골수 ≈ 3.0 ±0.2 (실측 {avgGoals:F2})"
            );
            Assert.That(
                scorelessRate,
                Is.InRange(0.02, 0.10),
                $"T5: 무득점 비율 2~10% (이론 5%, 실측 {scorelessRate:P1})"
            );
            Assert.That(
                highScoringRate,
                Is.InRange(0.08, 0.25),
                $"T5: 5골 이상 비율 8~25% (실측 {highScoringRate:P1})"
            );
        }

        // ── T6. 득점자 분포 ───────────────────────────────────────────

        [Test]
        public void T6_ScorerDistribution_AT_GreaterThan_MF_GreaterThan_DF_AndNoGK()
        {
            // 라인별 정확한 분배로 양 팀 25명 (GK 3 / DF 8 / MF 8 / AT 6).
            // 시드 well-distributed — 500매치로 충분한 표본.
            var seedGen = new System.Random(30_000);
            var lineCounts = new Dictionary<Line, int>
            {
                [Line.GK] = 0,
                [Line.DF] = 0,
                [Line.MF] = 0,
                [Line.AT] = 0,
            };
            const int N = 500;
            for (int i = 0; i < N; i++)
            {
                int matchSeed = seedGen.Next();
                var (state, match) = BuildStateWithDiversePositions(seed: matchSeed, matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                foreach (var ps in r.playerStats)
                {
                    if (ps.goals == 0)
                        continue;
                    var p = state.GetPlayer(ps.playerId);
                    var line = StartingSquadGacha.LineOf(p.info.primaryPosition);
                    lineCounts[line] += ps.goals;
                }
            }

            int total = lineCounts.Values.Sum();
            Assert.Greater(total, 0, "T6: 적어도 한 골은 나와야 함");
            double atRate = (double)lineCounts[Line.AT] / total;
            double mfRate = (double)lineCounts[Line.MF] / total;
            double dfRate = (double)lineCounts[Line.DF] / total;
            double gkRate = (double)lineCounts[Line.GK] / total;

            Assert.Greater(
                atRate,
                mfRate,
                $"T6: AT 비율 > MF 비율 (at={atRate:P1} mf={mfRate:P1})"
            );
            Assert.Greater(
                mfRate,
                dfRate,
                $"T6: MF 비율 > DF 비율 (mf={mfRate:P1} df={dfRate:P1})"
            );
            Assert.AreEqual(
                0,
                lineCounts[Line.GK],
                $"T6: GK 득점 = 0 (가중치 0). gkRate={gkRate:P1}"
            );
            // 라인별 가중치 합 (인원 × weight × avg CA/100):
            //   AT: 6 × 5.0 × ~1.07 ≈ 32 / MF: 8 × 1.5 × ~1.07 ≈ 13 / DF: 8 × 0.4 × ~1.07 ≈ 3.4
            //   총합 ≈ 48 → AT 비율 ≈ 67%, MF ≈ 27%, DF ≈ 7%
            Assert.That(
                atRate,
                Is.InRange(0.55, 0.80),
                $"T6: AT 비율 55~80% (이론 ~67%, 실측 {atRate:P1})"
            );
        }

        // ── T7. PlayerMatchStat ──────────────────────────────────────

        [Test]
        public void T7_PlayerMatchStat_FilledCorrectlyForV01()
        {
            var (state, match) = BuildState(homeCA: 100, awayCA: 100, seed: 5, matchId: 1);
            var r = MatchSimulator.Simulate(match, state, _balance);

            // starting11 합 == playerStats 합 (22명)
            Assert.AreEqual(
                r.homeStarting11.Count + r.awayStarting11.Count,
                r.playerStats.Count,
                "T7: starting11 합 == playerStats.Count"
            );

            int goalsInStats = r.playerStats.Sum(ps => ps.goals);
            Assert.AreEqual(
                r.homeScore + r.awayScore,
                goalsInStats,
                "T7: playerStats 의 goals 합 == homeScore + awayScore"
            );

            foreach (var ps in r.playerStats)
            {
                Assert.AreEqual(90, ps.minutesPlayed, $"T7: minutesPlayed=90 (id={ps.playerId})");
                Assert.AreEqual(0, ps.assists, $"T7: V0.1 assists=0 (id={ps.playerId})");
                Assert.AreEqual(0f, ps.rating, $"T7: V0.1 rating=0 (id={ps.playerId})");
                Assert.AreEqual(0, ps.yellowCards, $"T7: V0.1 yellow=0 (id={ps.playerId})");
                Assert.AreEqual(0, ps.redCards, $"T7: V0.1 red=0 (id={ps.playerId})");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private (GameState, Match) BuildState(int homeCA, int awayCA, int seed, int matchId)
        {
            var state = new GameState
            {
                randomSeed = seed,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, name: "Home", squadSize: 25, ca: homeCA);
            var away = NewClub(2, name: "Away", squadSize: 25, ca: awayCA);
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddPlayersToClub(state, home, ca: homeCA, nextId: nextId, count: 25);
            nextId = AddPlayersToClub(state, away, ca: awayCA, nextId: nextId, count: 25);
            var match = new Match
            {
                id = matchId,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };
            return (state, match);
        }

        // 양 팀 각자 라인 분포 다양 (GK 3 / DF 8 / MF 8 / AT 6 = 25명). CA 는 80~140 다양.
        private (GameState, Match) BuildStateWithDiversePositions(int seed, int matchId)
        {
            var state = new GameState
            {
                randomSeed = seed,
                currentDate = new System.DateTime(2025, 8, 15),
            };
            var home = NewClub(1, name: "Home");
            var away = NewClub(2, name: "Away");
            state.AddClub(home);
            state.AddClub(away);
            int nextId = 1;
            nextId = AddDiverseSquad(state, home, nextId);
            nextId = AddDiverseSquad(state, away, nextId);
            var match = new Match
            {
                id = matchId,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };
            return (state, match);
        }

        private static int AddDiverseSquad(GameState state, Club club, int nextId)
        {
            // 라인 분포: GK 3 / DF 8 / MF 8 / AT 6 = 25
            var composition = new (Position pos, int count)[]
            {
                (Position.GK, 3),
                (Position.CB, 4),
                (Position.LB, 2),
                (Position.RB, 2),
                (Position.DM, 2),
                (Position.CM, 3),
                (Position.LM, 1),
                (Position.RM, 2),
                (Position.LW, 1),
                (Position.RW, 1),
                (Position.ST, 3),
                (Position.CF, 1),
            };
            // CA 변동 90~130 (시드 결정성 위해 인덱스 기반)
            int idx = 0;
            foreach (var (pos, count) in composition)
            {
                for (int i = 0; i < count; i++)
                {
                    int ca = 90 + (idx * 7 % 40); // 90,97,104,...,124, 순환
                    state.AddPlayer(NewPlayer(nextId, pos, ca));
                    club.seniorSquadIds.Add(nextId);
                    nextId++;
                    idx++;
                }
            }
            return nextId;
        }

        private static Club NewClub(int id, string name, int squadSize = 25, int ca = 100) =>
            new Club
            {
                id = id,
                name = name,
                reputation = 60,
            };

        private static int AddPlayersToClub(
            GameState state,
            Club club,
            int ca,
            int nextId,
            int count
        )
        {
            // ST 4 / CM 4 / CB 3 등 다양 — 단순화 위해 1/3 씩 GK/필드/AT 분배.
            // T1~T5 는 라인 분포 무관 (CA 합만 사용). T2 starting11 검증도 CA 차이 없으면 정렬 안정성으로 단순.
            // → CA 를 약간 다양화하여 정렬 결과 안정성 확인 가능.
            for (int i = 0; i < count; i++)
            {
                // 같은 CA 면 정렬 시 안정성 의존 — 약간씩 다양화 (-2..+2)
                int variedCA = ca + ((i % 5) - 2);
                Position pos = (i % 5) switch
                {
                    0 => Position.GK,
                    1 => Position.CB,
                    2 => Position.CM,
                    3 => Position.ST,
                    _ => Position.LM,
                };
                state.AddPlayer(NewPlayer(nextId, pos, variedCA));
                club.seniorSquadIds.Add(nextId);
                nextId++;
            }
            return nextId;
        }

        private static Player NewPlayer(int id, Position pos, int ca) =>
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
                    injury = new InjuryInfo { injuryTypeId = -1 }, // 부상 없음
                    fatigue = 0,
                    morale = 50,
                    form = 50,
                },
            };

        // T3 helper: 강팀 vs 약팀 N 매치 시뮬레이션 — 강팀 승수 반환.
        // 시드 well-distributed — globalSeed 기반 seedGen 으로 매 매치마다 well-distributed seed 생성.
        // 단순 (seed + i) ^ i 패턴은 i 가 seed 의 lowest set bit 보다 작으면 collision 발생 → 매치 결과 클러스터링.
        private int SimulateManyMatches(
            int strongCA,
            int weakCA,
            bool strongIsHome,
            int matchCount,
            int globalSeed
        )
        {
            var seedGen = new System.Random(globalSeed);
            int strongWins = 0;
            for (int i = 0; i < matchCount; i++)
            {
                int matchSeed = seedGen.Next();
                var (state, match) = strongIsHome
                    ? BuildState(homeCA: strongCA, awayCA: weakCA, seed: matchSeed, matchId: i)
                    : BuildState(homeCA: weakCA, awayCA: strongCA, seed: matchSeed, matchId: i);
                var r = MatchSimulator.Simulate(match, state, _balance);
                bool strongWon = strongIsHome
                    ? (r.homeScore > r.awayScore)
                    : (r.awayScore > r.homeScore);
                if (strongWon)
                    strongWins++;
            }
            return strongWins;
        }
    }
}
