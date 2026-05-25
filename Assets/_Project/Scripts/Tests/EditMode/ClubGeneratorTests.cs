// ClubGeneratorTests.cs
// DoD: algorithms.md #5 Test Scenarios T1~T7.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace FMLite.Tests
{
    public class ClubGeneratorTests
    {
        private GameBalanceSO _balance;
        private LeagueConfigSO _leagueConfig;
        private readonly DateTime _testDate = new DateTime(2025, 8, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _leagueConfig = NewLeagueConfig();
            RegisterPositions();
            RegisterTraits();
            RegisterCountriesAndNamePools();
        }

        [TearDown]
        public void TearDown() => GameDatabase.Clear();

        // ── T1. 결정성 ────────────────────────────────────────────────

        [Test]
        public void T1_Determinism_SameSeedSameResult()
        {
            var r1 = Generate(new Random(42));
            var r2 = Generate(new Random(42));

            Assert.AreEqual(r1.Clubs.Count, r2.Clubs.Count, "T1: club count 동일");
            Assert.AreEqual(r1.Players.Count, r2.Players.Count, "T1: player count 동일");

            for (int i = 0; i < r1.Clubs.Count; i++)
            {
                Assert.AreEqual(r1.Clubs[i].id, r2.Clubs[i].id, $"T1: club[{i}].id");
                Assert.AreEqual(r1.Clubs[i].name, r2.Clubs[i].name, $"T1: club[{i}].name");
                Assert.AreEqual(
                    r1.Clubs[i].reputation,
                    r2.Clubs[i].reputation,
                    $"T1: club[{i}].reputation"
                );
                Assert.AreEqual(
                    r1.Clubs[i].finance.money,
                    r2.Clubs[i].finance.money,
                    $"T1: club[{i}].finance.money"
                );
                Assert.AreEqual(
                    r1.Clubs[i].facilities.scoutLevel,
                    r2.Clubs[i].facilities.scoutLevel,
                    $"T1: club[{i}].facilities.scoutLevel"
                );
            }

            for (int i = 0; i < r1.Players.Count; i++)
            {
                Assert.AreEqual(r1.Players[i].id, r2.Players[i].id, $"T1: player[{i}].id");
                Assert.AreEqual(
                    r1.Players[i].currentAbility,
                    r2.Players[i].currentAbility,
                    $"T1: player[{i}].CA"
                );
                Assert.AreEqual(
                    r1.Players[i].info.firstName,
                    r2.Players[i].info.firstName,
                    $"T1: player[{i}].firstName"
                );
                Assert.AreEqual(
                    r1.Players[i].info.primaryPosition,
                    r2.Players[i].info.primaryPosition,
                    $"T1: player[{i}].primaryPosition"
                );
            }
        }

        // ── T2. 명성 4티어 분포 (clubCount=20) ────────────────────────

        [Test]
        public void T2_ReputationTierDistribution_ClubCount20()
        {
            var result = Generate(new Random(42));
            var reps = result.Clubs.Select(c => c.reputation).ToList();

            int top4 = reps.Count(r => r >= 85 && r <= 95);
            int euro = reps.Count(r => r >= 65 && r <= 80);
            int mid = reps.Count(r => r >= 45 && r <= 60);
            int rel = reps.Count(r => r >= 25 && r <= 40);

            Assert.AreEqual(4, top4, "T2: Top4 4구단");
            Assert.AreEqual(6, euro, "T2: Euro 6구단");
            Assert.AreEqual(7, mid, "T2: Mid 7구단");
            Assert.AreEqual(3, rel, "T2: Rel 3구단");
            Assert.AreEqual(20, top4 + euro + mid + rel, "T2: 모든 구단이 4티어 범위 내");
        }

        // ── T2b. 가변 clubCount ──────────────────────────────────────

        [Test]
        public void T2b_VariableClubCount_TierSumMatchesN()
        {
            foreach (int n in new[] { 10, 12, 24 })
            {
                var cfg = NewLeagueConfig(n);
                var result = ClubGenerator.Generate(
                    new Random(42),
                    cfg,
                    _balance,
                    _testDate,
                    1,
                    1,
                    1
                );
                Assert.AreEqual(
                    n,
                    result.Clubs.Count,
                    $"T2b: clubCount={n} 일 때 정확히 {n}개 생성"
                );
                Assert.AreEqual(
                    n * 25,
                    result.Players.Count,
                    $"T2b: clubCount={n} 일 때 선수 {n * 25}명"
                );
            }
        }

        // ── T3. 스쿼드 인원 / FormationConfig 제약 조건 ───────────────

        [Test]
        public void T3_SquadComposition_AllClubsMatchFormationConstraints()
        {
            var result = Generate(new Random(42));
            var byId = result.Players.ToDictionary(p => p.id);

            foreach (var club in result.Clubs)
            {
                Assert.AreEqual(25, club.seniorSquadIds.Count, $"T3: club {club.id} 스쿼드 25명");

                var posCounts = club
                    .seniorSquadIds.Select(id => byId[id].info.primaryPosition)
                    .GroupBy(p => p)
                    .ToDictionary(g => g.Key, g => g.Count());

                int Get(Position p) => posCounts.TryGetValue(p, out int c) ? c : 0;

                // FormationConfig 4-4-2 필수 인원
                Assert.AreEqual(3, Get(Position.GK), $"T3: club {club.id} GK 정확히 3 (서드키퍼)");
                Assert.GreaterOrEqual(
                    Get(Position.CB),
                    4,
                    $"T3: club {club.id} CB ≥ 4 (actual={Get(Position.CB)})"
                );
                Assert.GreaterOrEqual(Get(Position.LB), 2, $"T3: club {club.id} LB ≥ 2");
                Assert.GreaterOrEqual(Get(Position.RB), 2, $"T3: club {club.id} RB ≥ 2");
                Assert.GreaterOrEqual(
                    Get(Position.DM) + Get(Position.CM),
                    4,
                    $"T3: club {club.id} DM+CM ≥ 4"
                );
                Assert.GreaterOrEqual(
                    Get(Position.LM) + Get(Position.LW),
                    2,
                    $"T3: club {club.id} LM+LW ≥ 2"
                );
                Assert.GreaterOrEqual(
                    Get(Position.RM) + Get(Position.RW),
                    2,
                    $"T3: club {club.id} RM+RW ≥ 2"
                );
                Assert.GreaterOrEqual(
                    Get(Position.ST) + Get(Position.CF),
                    4,
                    $"T3: club {club.id} ST+CF ≥ 4"
                );

                // V0.1 분배표 제외 포지션 (AM, WB)
                Assert.AreEqual(
                    0,
                    Get(Position.AM),
                    $"T3: club {club.id} AM 0명 (V0.1 분배표 제외)"
                );
                Assert.AreEqual(
                    0,
                    Get(Position.WB),
                    $"T3: club {club.id} WB 0명 (V0.1 분배표 제외)"
                );
            }
        }

        // ── T3b. 랜덤 2자리로 인한 구단별 분배 다양성 ──────────────────

        [Test]
        public void T3b_SquadComposition_VariesAcrossClubs()
        {
            var result = Generate(new Random(42));
            var byId = result.Players.ToDictionary(p => p.id);

            var distributions = new HashSet<string>();
            foreach (var club in result.Clubs)
            {
                var sig = string.Join(
                    ",",
                    club.seniorSquadIds.Select(id => byId[id].info.primaryPosition)
                        .GroupBy(p => p)
                        .OrderBy(g => g.Key)
                        .Select(g => $"{g.Key}={g.Count()}")
                );
                distributions.Add(sig);
            }

            Assert.That(
                distributions.Count,
                Is.GreaterThan(1),
                $"T3b: 랜덤 2자리로 구단별 분배 다양성 보장 "
                    + $"(actual distinct distributions={distributions.Count}, 1보다 커야 함)"
            );
        }

        // ── T4. 연령 분포 (500명 batch) ───────────────────────────────

        [Test]
        public void T4_AgeDistribution_500PlayersBatch()
        {
            var result = Generate(new Random(42));
            var ages = result.Players.Select(p => _testDate.Year - p.info.birthDate.Year).ToList();

            double avg = ages.Average();
            int min = ages.Min();
            int max = ages.Max();

            Assert.That(avg, Is.InRange(23.0, 27.0), $"T4: 평균 연령 (actual={avg:F2})");
            Assert.That(min, Is.GreaterThanOrEqualTo(16), $"T4: min age >= 16 (actual={min})");
            Assert.That(max, Is.LessThanOrEqualTo(35), $"T4: max age <= 35 (actual={max})");

            int total = ages.Count;
            double youthR = ages.Count(a => a >= 16 && a <= 21) / (double)total;
            double primeR = ages.Count(a => a >= 22 && a <= 28) / (double)total;
            double vetR = ages.Count(a => a >= 29 && a <= 35) / (double)total;

            Assert.That(
                youthR,
                Is.InRange(0.13, 0.27),
                $"T4: youth 비율 20% ±7% (actual={youthR:P1})"
            );
            Assert.That(
                primeR,
                Is.InRange(0.53, 0.67),
                $"T4: prime 비율 60% ±7% (actual={primeR:P1})"
            );
            Assert.That(
                vetR,
                Is.InRange(0.13, 0.27),
                $"T4: veteran 비율 20% ±7% (actual={vetR:P1})"
            );
        }

        // ── T5. 국적 분포 (500명 batch, ENG ≈ 70%) ────────────────────

        [Test]
        public void T5_NationalityDistribution_500PlayersBatch()
        {
            var result = Generate(new Random(42));
            int eng = result.Players.Count(p => p.info.nationalityCode == "ENG");
            double ratio = eng / (double)result.Players.Count;

            Assert.That(ratio, Is.InRange(0.64, 0.76), $"T5: ENG 비율 70% ±6% (actual={ratio:P1})");
        }

        // ── T6. 재정 / 시설 — 명성 약상관 (20구단) ────────────────────

        [Test]
        public void T6_ReputationCorrelations_ClubCount20()
        {
            var result = Generate(new Random(42));
            double[] reps = result.Clubs.Select(c => (double)c.reputation).ToArray();
            double[] money = result.Clubs.Select(c => (double)c.finance.money).ToArray();
            double[] scout = result.Clubs.Select(c => (double)c.facilities.scoutLevel).ToArray();

            double rMoney = PearsonCorrelation(reps, money);
            double rScout = PearsonCorrelation(reps, scout);

            Assert.That(
                rMoney,
                Is.InRange(0.5, 0.99),
                $"T6: corr(rep, money) in [0.5, 0.99] (actual={rMoney:F3})"
            );
            Assert.That(
                rScout,
                Is.InRange(0.4, 0.99),
                $"T6: corr(rep, scoutLevel) in [0.4, 0.99] (actual={rScout:F3})"
            );

            // 다양성: 시설 등급 분포가 1 또는 5 한 곳에 60% 이상 몰리지 않음
            int lv1 = result.Clubs.Count(c => c.facilities.scoutLevel == 1);
            int lv5 = result.Clubs.Count(c => c.facilities.scoutLevel == 5);
            double r1 = lv1 / (double)result.Clubs.Count;
            double r5 = lv5 / (double)result.Clubs.Count;
            Assert.That(r1, Is.LessThan(0.60), $"T6: Lv1 60% 이상 몰리지 않음 (actual={r1:P1})");
            Assert.That(r5, Is.LessThan(0.60), $"T6: Lv5 60% 이상 몰리지 않음 (actual={r5:P1})");
        }

        // ── T7. ID 유일성 / 단조증가 ──────────────────────────────────

        [Test]
        public void T7_IdUniquenessAndMonotonic()
        {
            var result = Generate(new Random(42));

            var clubIds = result.Clubs.Select(c => c.id).ToList();
            var playerIds = result.Players.Select(p => p.id).ToList();

            Assert.AreEqual(clubIds.Count, clubIds.Distinct().Count(), "T7: club id 중복 없음");
            Assert.AreEqual(
                playerIds.Count,
                playerIds.Distinct().Count(),
                "T7: player id 중복 없음"
            );

            Assert.AreEqual(1, clubIds.Min(), "T7: club id 시작값");
            Assert.AreEqual(20, clubIds.Max(), "T7: club id 끝값");
            Assert.AreEqual(1, playerIds.Min(), "T7: player id 시작값");
            Assert.AreEqual(500, playerIds.Max(), "T7: player id 끝값");

            // seniorSquadIds 가 정확히 players 의 id 부분집합
            var playerIdSet = new HashSet<int>(playerIds);
            foreach (var club in result.Clubs)
            {
                foreach (var id in club.seniorSquadIds)
                    Assert.IsTrue(
                        playerIdSet.Contains(id),
                        $"T7: club {club.id} seniorSquadIds 의 id {id} 가 players 에 없음"
                    );
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private ClubGenerationResult Generate(Random rng, LeagueConfigSO cfg = null) =>
            ClubGenerator.Generate(rng, cfg ?? _leagueConfig, _balance, _testDate, 1, 1, 1);

        private LeagueConfigSO NewLeagueConfig(int clubCount = 20)
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfigSO>();
            cfg.id = 1;
            cfg.displayName = "Test EPL";
            cfg.countryCode = "ENG";
            cfg.clubCount = clubCount;
            cfg.relegationCount = 3;
            cfg.playersPerClub = 25;
            cfg.clubNames = Enumerable.Range(1, clubCount).Select(i => $"Club {i:D2}").ToList();
            return cfg;
        }

        // (Position, isGK, emphTech, emphMental, emphPhys) — PlayerGeneratorTests 와 동일.
        private static readonly (Position p, bool gk, bool t, bool m, bool ph)[] PosDefs =
        {
            (Position.GK, true, false, true, true),
            (Position.CB, false, false, true, true),
            (Position.LB, false, true, true, true),
            (Position.RB, false, true, true, true),
            (Position.WB, false, true, true, true),
            (Position.DM, false, true, true, true),
            (Position.CM, false, true, true, true),
            (Position.AM, false, true, true, false),
            (Position.LM, false, true, true, true),
            (Position.RM, false, true, true, true),
            (Position.LW, false, true, false, true),
            (Position.RW, false, true, false, true),
            (Position.ST, false, true, true, true),
            (Position.CF, false, true, true, true),
        };

        private static readonly Dictionary<Position, (Position pos, float w)[]> AffDefs = new()
        {
            [Position.ST] = new[]
            {
                (Position.CF, 8f),
                (Position.LW, 5f),
                (Position.RW, 5f),
                (Position.AM, 3f),
            },
            [Position.CF] = new[]
            {
                (Position.ST, 8f),
                (Position.AM, 5f),
                (Position.LW, 3f),
                (Position.RW, 3f),
            },
            [Position.LW] = new[] { (Position.LM, 6f), (Position.AM, 4f), (Position.ST, 3f) },
            [Position.RW] = new[] { (Position.RM, 6f), (Position.AM, 4f), (Position.ST, 3f) },
            [Position.AM] = new[]
            {
                (Position.CM, 6f),
                (Position.CF, 4f),
                (Position.LW, 3f),
                (Position.RW, 3f),
            },
            [Position.CM] = new[]
            {
                (Position.AM, 5f),
                (Position.DM, 5f),
                (Position.LM, 3f),
                (Position.RM, 3f),
            },
            [Position.DM] = new[] { (Position.CM, 6f), (Position.CB, 4f) },
            [Position.LM] = new[] { (Position.LW, 6f), (Position.CM, 4f), (Position.LB, 3f) },
            [Position.RM] = new[] { (Position.RW, 6f), (Position.CM, 4f), (Position.RB, 3f) },
            [Position.LB] = new[] { (Position.WB, 8f), (Position.LM, 4f), (Position.CB, 3f) },
            [Position.RB] = new[] { (Position.WB, 8f), (Position.RM, 4f), (Position.CB, 3f) },
            [Position.WB] = new[]
            {
                (Position.LB, 8f),
                (Position.RB, 8f),
                (Position.LM, 5f),
                (Position.RM, 5f),
            },
            [Position.CB] = new[] { (Position.DM, 4f), (Position.LB, 3f), (Position.RB, 3f) },
        };

        private static void RegisterPositions()
        {
            for (int i = 0; i < PosDefs.Length; i++)
            {
                var d = PosDefs[i];
                var so = ScriptableObject.CreateInstance<PositionSO>();
                so.id = i + 1;
                so.position = d.p;
                so.isGoalkeeper = d.gk;
                so.emphasizesTechnical = d.t;
                so.emphasizesMental = d.m;
                so.emphasizesPhysical = d.ph;
                so.affinities = new List<PositionAffinity>();
                if (AffDefs.TryGetValue(d.p, out var entries))
                    foreach (var e in entries)
                        so.affinities.Add(new PositionAffinity { position = e.pos, weight = e.w });
                so.fallbackAffinityWeight = 0.05f;
                GameDatabase.Register(so);
            }
        }

        private static void RegisterTraits()
        {
            var defs = new[]
            {
                (1, "늦깎이형", 1.0f, 1),
                (2, "조숙형", 1.0f, 1),
                (3, "부상 취약", 0.7f, 0),
                (4, "멘탈 강자", 1.0f, 0),
                (5, "빅매치형", 0.8f, 0),
                (6, "만능형", 0.8f, 0),
            };
            foreach (var (id, name, weight, group) in defs)
            {
                var so = ScriptableObject.CreateInstance<TraitSO>();
                so.id = id;
                so.displayName = name;
                so.weight = weight;
                so.exclusionGroupId = group;
                GameDatabase.Register(so);
            }
        }

        private static void RegisterCountriesAndNamePools()
        {
            // T5 의 외국 국가 분포 검증을 위해 ENG + 다른 4개국 등록.
            var defs = new[]
            {
                (
                    1,
                    "ENG",
                    new[]
                    {
                        "James",
                        "John",
                        "Robert",
                        "Michael",
                        "William",
                        "David",
                        "Richard",
                        "Thomas",
                        "Daniel",
                        "Matthew",
                    },
                    new[]
                    {
                        "Smith",
                        "Johnson",
                        "Williams",
                        "Brown",
                        "Jones",
                        "Miller",
                        "Davis",
                        "Wilson",
                        "Taylor",
                        "Moore",
                    }
                ),
                (
                    2,
                    "FRA",
                    new[]
                    {
                        "Pierre",
                        "Jean",
                        "Jacques",
                        "Michel",
                        "Philippe",
                        "Nicolas",
                        "Alain",
                        "Bernard",
                        "Daniel",
                        "Christian",
                    },
                    new[]
                    {
                        "Martin",
                        "Bernard",
                        "Dubois",
                        "Thomas",
                        "Robert",
                        "Petit",
                        "Richard",
                        "Durand",
                        "Moreau",
                        "Laurent",
                    }
                ),
                (
                    3,
                    "GER",
                    new[]
                    {
                        "Hans",
                        "Michael",
                        "Stefan",
                        "Klaus",
                        "Wolfgang",
                        "Thomas",
                        "Peter",
                        "Andreas",
                        "Christian",
                        "Manfred",
                    },
                    new[]
                    {
                        "Müller",
                        "Schmidt",
                        "Schneider",
                        "Fischer",
                        "Weber",
                        "Meyer",
                        "Wagner",
                        "Becker",
                        "Schulz",
                        "Hoffmann",
                    }
                ),
                (
                    4,
                    "ESP",
                    new[]
                    {
                        "Antonio",
                        "José",
                        "Manuel",
                        "Francisco",
                        "David",
                        "Juan",
                        "Javier",
                        "Daniel",
                        "Carlos",
                        "Miguel",
                    },
                    new[]
                    {
                        "García",
                        "Rodríguez",
                        "González",
                        "Fernández",
                        "López",
                        "Martínez",
                        "Sánchez",
                        "Pérez",
                        "Gómez",
                        "Martín",
                    }
                ),
                (
                    5,
                    "ITA",
                    new[]
                    {
                        "Marco",
                        "Andrea",
                        "Luca",
                        "Alessandro",
                        "Stefano",
                        "Francesco",
                        "Matteo",
                        "Davide",
                        "Roberto",
                        "Luigi",
                    },
                    new[]
                    {
                        "Rossi",
                        "Russo",
                        "Ferrari",
                        "Esposito",
                        "Bianchi",
                        "Romano",
                        "Colombo",
                        "Ricci",
                        "Marino",
                        "Greco",
                    }
                ),
            };

            foreach (var (id, code, first, last) in defs)
            {
                var country = ScriptableObject.CreateInstance<CountrySO>();
                country.id = id;
                country.code = code;
                GameDatabase.Register(country);

                var pool = ScriptableObject.CreateInstance<NamePoolSO>();
                pool.countryId = id;
                pool.firstNames = new List<string>(first);
                pool.lastNames = new List<string>(last);
                GameDatabase.Register(pool);
            }
        }

        // Pearson correlation — PlayerGeneratorTests 와 동일.
        private static double PearsonCorrelation(double[] x, double[] y)
        {
            int n = x.Length;
            double meanX = x.Average();
            double meanY = y.Average();
            double num = 0,
                den1 = 0,
                den2 = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - meanX;
                double dy = y[i] - meanY;
                num += dx * dy;
                den1 += dx * dx;
                den2 += dy * dy;
            }
            return num / Math.Sqrt(den1 * den2);
        }
    }
}
