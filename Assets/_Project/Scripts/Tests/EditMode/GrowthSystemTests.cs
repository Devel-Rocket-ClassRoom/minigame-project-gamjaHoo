// GrowthSystemTests.cs
// V0.5 D.4 Sub-B — algorithms.md V0.5-10 Test Scenarios.

using System;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class GrowthSystemTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _today = new DateTime(2026, 6, 1); // 매월 1일

        [SetUp]
        public void Setup()
        {
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
        }

        // ── T1. 결정성 — 같은 시드 두 state → 같은 stat ──────────────

        [Test]
        public void T1_Determinism_SameSeedSameResult()
        {
            var state1 = BuildSingleClubState(
                clubTraining: 5,
                clubGym: 5,
                birthYear: 2008,
                ca: 80,
                pa: 180,
                randomSeed: 42
            );
            var state2 = BuildSingleClubState(
                clubTraining: 5,
                clubGym: 5,
                birthYear: 2008,
                ca: 80,
                pa: 180,
                randomSeed: 42
            );

            SimulateYear(state1);
            SimulateYear(state2);

            var p1 = state1.GetPlayer(1).stats;
            var p2 = state2.GetPlayer(1).stats;
            Assert.AreEqual(
                p1.technical.passing,
                p2.technical.passing,
                "T1: technical.passing 동일"
            );
            Assert.AreEqual(p1.physical.pace, p2.physical.pace, "T1: physical.pace 동일");
            Assert.AreEqual(p1.mental.vision, p2.mental.vision, "T1: mental.vision 동일");
        }

        // ── T2. Training 시설 영향 — Lv1 vs Lv10 → Lv10 평균 성장 ↑ ──

        [Test]
        public void T2_TrainingLevel_AffectsGrowth()
        {
            // 같은 18세 wonderkid (PA 180, CA 80), Lv1 vs Lv10 Training, 1년 시뮬.
            var stateLow = BuildSingleClubState(
                clubTraining: 1,
                clubGym: 0,
                birthYear: 2008,
                ca: 80,
                pa: 180,
                randomSeed: 42
            );
            var stateHigh = BuildSingleClubState(
                clubTraining: 10,
                clubGym: 0,
                birthYear: 2008,
                ca: 80,
                pa: 180,
                randomSeed: 42
            );

            SimulateYear(stateLow);
            SimulateYear(stateHigh);

            int sumLow = SumAllStats(stateLow.GetPlayer(1));
            int sumHigh = SumAllStats(stateHigh.GetPlayer(1));

            Assert.Greater(sumHigh, sumLow, "T2: Training Lv10 > Lv1 (평균 성장 ↑)");
        }

        // ── T3. Absolute 차등 — Determination 변화 < 일반 stat 변화 ──

        [Test]
        public void T3_AbsoluteFactor_LimitsAbsoluteStatGrowth()
        {
            // 100명 평균 — Absolute (Determination) 변화량 << Relative (Passing)
            int absChangeSum = 0;
            int relChangeSum = 0;
            const int playerCount = 50;

            var state = new GameState { currentDate = _today, randomSeed = 7 };
            var club = NewClub(1, trainingLevel: 5, gymLevel: 0);
            for (int i = 1; i <= playerCount; i++)
            {
                var p = NewPlayer(i, birthYear: 2008, ca: 80, pa: 180); // PA gap 100
                p.stats.mental.determination = 50;
                p.stats.technical.passing = 50;
                state.AddPlayer(p);
                club.seniorSquadIds.Add(i);
            }
            state.AddClub(club);

            SimulateYear(state);

            for (int i = 1; i <= playerCount; i++)
            {
                var p = state.GetPlayer(i);
                absChangeSum += p.stats.mental.determination - 50;
                relChangeSum += p.stats.technical.passing - 50;
            }

            Assert.Greater(
                relChangeSum,
                absChangeSum,
                "T3: Absolute (Determination) 변화 < Relative (Passing)"
            );
        }

        // ── T5. PA 캡 — CA = PA 선수 → 거의 성장 X ──────────────────

        [Test]
        public void T5_PaCap_StopsGrowth_WhenCaEqualsPa()
        {
            // CA == PA = 180, 25세 (prime), Training Lv10 → paFactor=0 → 성장 X
            var state = BuildSingleClubState(
                clubTraining: 10,
                clubGym: 10,
                birthYear: 2001,
                ca: 180,
                pa: 180,
                randomSeed: 42
            );

            int sumBefore = SumAllStats(state.GetPlayer(1));
            SimulateYear(state);
            int sumAfter = SumAllStats(state.GetPlayer(1));

            Assert.AreEqual(
                sumBefore,
                sumAfter,
                "T5: CA == PA → 1년 stat 합 변화 X (decline X, ageFactor=0.6)"
            );
        }

        // ── T6. 나이 곡선 — ComputeAgeFactor 단위 검증 ──────────────

        [Test]
        public void T6_AgeFactor_Curve()
        {
            // 16세 = peak (+1.5), 22세 = peak 끝 (+0.9), 25세 = prime (+0.7), 30세 = 정체 (0), 33세 = decline (-0.6)
            Assert.AreEqual(
                1.5f,
                GrowthSystem.ComputeAgeFactor(16, _balance),
                0.01f,
                "T6a: 16세 = +1.5"
            );
            Assert.AreEqual(
                0.9f,
                GrowthSystem.ComputeAgeFactor(22, _balance),
                0.01f,
                "T6b: 22세 = +0.9"
            );
            Assert.AreEqual(
                0.6f,
                GrowthSystem.ComputeAgeFactor(26, _balance),
                0.01f,
                "T6c: 26세 = +0.6 (prime 끝)"
            );
            Assert.AreEqual(
                0f,
                GrowthSystem.ComputeAgeFactor(30, _balance),
                0.01f,
                "T6d: 30세 = 0 (정체)"
            );
            Assert.AreEqual(
                -0.6f,
                GrowthSystem.ComputeAgeFactor(33, _balance),
                0.01f,
                "T6e: 33세 = -0.6 (decline)"
            );
        }

        // ── T7. Decline — 33세+ stat 합 감소 ─────────────────────────

        [Test]
        public void T7_Decline_OldPlayerLosesStats()
        {
            // 35세 (ageFactor = -1.0), 5년 시뮬 — stat 합 감소.
            var state = new GameState { currentDate = _today, randomSeed = 42 };
            var club = NewClub(1, trainingLevel: 10, gymLevel: 10);
            var p = NewPlayer(1, birthYear: 1991, ca: 180, pa: 180); // 35세
            state.AddPlayer(p);
            club.seniorSquadIds.Add(1);
            state.AddClub(club);

            int sumBefore = SumAllStats(p);
            for (int year = 0; year < 3; year++)
                SimulateYear(state);
            int sumAfter = SumAllStats(p);

            Assert.Less(sumAfter, sumBefore, "T7: 노장 (35세+) 3년 시뮬 → stat 합 감소");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private GameState BuildSingleClubState(
            int clubTraining,
            int clubGym,
            int birthYear,
            int ca,
            int pa,
            int randomSeed
        )
        {
            var state = new GameState { currentDate = _today, randomSeed = randomSeed };
            var club = NewClub(1, trainingLevel: clubTraining, gymLevel: clubGym);
            var p = NewPlayer(1, birthYear, ca, pa);
            state.AddPlayer(p);
            club.seniorSquadIds.Add(1);
            state.AddClub(club);
            return state;
        }

        private static Player NewPlayer(int id, int birthYear, int ca, int pa)
        {
            var stats = new Stats();
            stats.technical.ApplyToAll(_ => 50);
            stats.mental.ApplyToAll(_ => 50);
            stats.physical.ApplyToAll(_ => 50);
            stats.gk.ApplyToAll(_ => 50);
            return new Player
            {
                id = id,
                info = new PersonalInfo { birthDate = new DateTime(birthYear, 1, 1) },
                stats = stats,
                currentAbility = ca,
                potentialAbility = pa,
                state = new PlayerState { injury = new InjuryInfo { injuryTypeId = -1 } },
            };
        }

        private static Club NewClub(int id, int trainingLevel, int gymLevel)
        {
            return new Club
            {
                id = id,
                facilities = new Facilities { trainingLevel = trainingLevel, gymLevel = gymLevel },
            };
        }

        private void SimulateYear(GameState state)
        {
            for (int m = 1; m <= 12; m++)
            {
                state.currentDate = new DateTime(2026, m, 1);
                GrowthSystem.Tick(state, _balance);
            }
        }

        private static int SumAllStats(Player p)
        {
            int sum = 0;
            p.stats.technical.ApplyToAll(v =>
            {
                sum += v;
                return v;
            });
            p.stats.mental.ApplyToAll(v =>
            {
                sum += v;
                return v;
            });
            p.stats.physical.ApplyToAll(v =>
            {
                sum += v;
                return v;
            });
            p.stats.gk.ApplyToAll(v =>
            {
                sum += v;
                return v;
            });
            return sum;
        }
    }
}
