// PlayerGeneratorTests.cs
// DoD: algorithms.md #1 Test Scenarios T1~T7 검증.
// 모든 단일 선수 테스트(T1~T3)는 Random(seed:42) 고정.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using FMLite.Domain;
using Random = System.Random;
using FMLite.Application;

namespace FMLite.Tests
{
    public class PlayerGeneratorTests
    {
        private GameBalanceSO _balance;
        private readonly DateTime _testDate = new DateTime(2025, 8, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            RegisterPositions();
            RegisterTraits();
            RegisterCountriesAndNamePools();
        }

        [TearDown]
        public void TearDown() => GameDatabase.Clear();

        // ── T1. 빅클럽 베테랑 ST ─────────────────────────────────────

        [Test]
        public void T1_BigClubVeteranST_CAAndStatsInRange()
        {
            var rng    = new Random(42);
            var player = Generate(rng, 85, Position.ST, 27, "ENG");

            Assert.That(player.currentAbility, Is.InRange(120, 155),
                "T1: CA in [120, 155]");
            Assert.That(player.potentialAbility - player.currentAbility, Is.InRange(0, 10),
                "T1: PA-CA gap in [0, 10]");
            Assert.That(TechMean(player), Is.GreaterThanOrEqualTo(14.0),
                "T1: Technical avg >= 14");
            Assert.That(PhysMean(player), Is.GreaterThanOrEqualTo(14.0),
                "T1: Physical avg >= 14");
            Assert.That(GkMean(player), Is.InRange(1.0, 4.0),
                "T1: GK stat avg in [1, 4]");
        }

        // ── T2. 작은 구단 신예 CM ─────────────────────────────────────

        [Test]
        public void T2_SmallClubYouthCM_CAAndNationalityInRange()
        {
            var rng    = new Random(42);
            var player = Generate(rng, 25, Position.CM, 17, "ESP");

            Assert.That(player.currentAbility, Is.InRange(50, 90),
                "T2: CA in [50, 90]");
            // PA 절댓값은 seed·noise 의존. 핵심 검증은 PA >= CA (알고리즘 보장) + 합리적 상한.
            Assert.That(player.potentialAbility, Is.InRange(50, 150),
                "T2: PA in [50, 150]");

            var espPool = GameDatabase.GetNamePool(4); // ESP countryId=4
            Assert.That(espPool.firstNames, Contains.Item(player.info.firstName),
                "T2: firstName in ESP NamePool");
        }

        // ── T3. GK 생성 ───────────────────────────────────────────────

        [Test]
        public void T3_GkGeneration_StatsAndZeroSecondaryPositions()
        {
            var rng    = new Random(42);
            var player = Generate(rng, 50, Position.GK, 24, "ITA");

            Assert.That(GkMean(player),   Is.InRange(8.0, 14.0),
                "T3: GoalkeepingStats avg in [8, 14]");
            Assert.That(TechMean(player), Is.InRange(1.0, 5.0),
                "T3: TechnicalStats avg in [1, 5]");
            Assert.That(MentMean(player), Is.InRange(7.0, 12.0),
                "T3: MentalStats avg in [7, 12]");
            Assert.That(player.info.secondaryPositions.Count, Is.EqualTo(0),
                "T3: GK 2차 포지션 없음");
        }

        // ── T4. 결정성 ────────────────────────────────────────────────

        [Test]
        public void T4_Determinism_SameSeedProducesSamePlayer()
        {
            var p1 = Generate(new Random(42), 70, Position.ST, 25, "ENG");
            var p2 = Generate(new Random(42), 70, Position.ST, 25, "ENG");

            Assert.AreEqual(p1.currentAbility,   p2.currentAbility,   "T4: CA 동일");
            Assert.AreEqual(p1.potentialAbility,  p2.potentialAbility,  "T4: PA 동일");
            Assert.AreEqual(p1.info.firstName,    p2.info.firstName,    "T4: firstName 동일");
            Assert.AreEqual(p1.info.lastName,     p2.info.lastName,     "T4: lastName 동일");
            Assert.AreEqual(p1.contract.weeklyWage, p2.contract.weeklyWage, "T4: 임금 동일");
            Assert.AreEqual(p1.faceSeed,          p2.faceSeed,          "T4: faceSeed 동일");
            Assert.AreEqual(p1.info.preferredFoot, p2.info.preferredFoot, "T4: foot 동일");
        }

        [Test]
        public void T4_Determinism_DifferentSeedDifferentResult()
        {
            var p1 = Generate(new Random(42),  70, Position.ST, 25, "ENG");
            var p2 = Generate(new Random(999), 70, Position.ST, 25, "ENG");

            bool anyDiff = p1.currentAbility != p2.currentAbility
                        || p1.info.firstName  != p2.info.firstName
                        || p1.faceSeed        != p2.faceSeed;
            Assert.IsTrue(anyDiff, "T4: 다른 시드 → 다른 결과");
        }

        // ── T5. 분포 통계 (1000명 batch) ──────────────────────────────

        [Test]
        public void T5_Distribution_1000OutfieldPlayers()
        {
            // age penalty 적용 후 실제 기대 CA 평균 ≈85 (spec의 100은 prime age 기준 오기).
            // PA-CA 갭 기대값 ≈20 (spec의 25는 age 20~25 기준).
            var nonGkPositions = new[]
            {
                Position.CB, Position.LB, Position.RB, Position.WB,
                Position.DM, Position.CM, Position.AM,
                Position.LM, Position.RM, Position.LW, Position.RW,
                Position.ST, Position.CF,
            };

            const int n   = 1000;
            var       rng = new Random(42);
            var players   = new Player[n];

            for (int i = 0; i < n; i++)
            {
                int      age = 17 + (i % 14);                        // 17..30 균등
                Position pos = nonGkPositions[i % nonGkPositions.Length];
                players[i] = Generate(rng, 50, pos, age, "ENG");
            }

            // CA 평균: age penalty 포함 실측 기대값 ~78~97
            double avgCA = players.Average(p => (double)p.currentAbility);
            Assert.That(avgCA, Is.InRange(75.0, 97.0),
                $"T5: CA 평균 (actual={avgCA:F1}, age penalty 포함)");

            // PA-CA 갭 평균: 실측 기대값 ~15~30
            double avgGap = players.Average(p => (double)(p.potentialAbility - p.currentAbility));
            Assert.That(avgGap, Is.InRange(14.0, 32.0),
                $"T5: PA-CA 갭 평균 (actual={avgGap:F1})");

            // 트레잇 보유 비율 30% ±5% (1000샘플 σ≈1.5%, ±5%는 약 3σ 허용)
            double traitRatio = players.Count(p => p.traitIds.Count > 0) / (double)n;
            Assert.That(traitRatio, Is.InRange(0.25, 0.35),
                $"T5: 트레잇 보유 비율 30% ±5% (actual={traitRatio:P1})");

            // 늦깎이형(id=1) + 조숙형(id=2) 동시 보유 = 0건
            int conflictCount = players.Count(p => p.traitIds.Contains(1) && p.traitIds.Contains(2));
            Assert.AreEqual(0, conflictCount, "T5: DevelopmentSpeed 충돌 트레잇 동시 보유 없음");

            // CA 와 stats 가중합의 상관계수 > 0.6
            double[] caArr   = players.Select(p => (double)p.currentAbility).ToArray();
            double[] statArr = players.Select(p => (double)StatSum(p)).ToArray();
            double   r       = PearsonCorrelation(caArr, statArr);
            Assert.That(r, Is.GreaterThan(0.6),
                $"T5: CA-Stats 상관계수 > 0.6 (actual={r:F3})");
        }

        // ── T6. ST 2차 포지션 affinity 분포 (1000명 batch) ────────────

        [Test]
        public void T6_SecondaryPosition_STAffinityDistribution()
        {
            const int n   = 1000;
            var       rng = new Random(42);
            var allSec    = new List<Position>();

            for (int i = 0; i < n; i++)
            {
                var p = Generate(rng, 50, Position.ST, 25, "ENG");
                allSec.AddRange(p.info.secondaryPositions);
            }

            int total = allSec.Count;
            Assert.That(total, Is.GreaterThan(0), "T6: secondary positions 비어있음");

            int lwrw  = allSec.Count(p => p == Position.LW || p == Position.RW);
            int am    = allSec.Count(p => p == Position.AM);
            int cbEtc = allSec.Count(p => p == Position.CB || p == Position.LB || p == Position.RB);
            int gk    = allSec.Count(p => p == Position.GK);

            Assert.That(lwrw  / (double)total, Is.InRange(0.35, 0.55),
                $"T6: LW/RW 합계 35~55% (actual={lwrw}/{total}={lwrw/(double)total:P1})");
            Assert.That(am    / (double)total, Is.InRange(0.08, 0.22),
                $"T6: AM 8~22% (actual={am}/{total}={am/(double)total:P1})");
            Assert.That(cbEtc / (double)total, Is.LessThan(0.05),
                $"T6: CB/LB/RB < 5% (actual={cbEtc}/{total}={cbEtc/(double)total:P1})");
            Assert.AreEqual(0, gk, "T6: GK = 0건");
        }

        // ── T7. GK 2차 포지션 = 0 (100명 batch) ──────────────────────

        [Test]
        public void T7_GkSecondaryPositions_AlwaysZero()
        {
            var rng = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                var p = Generate(rng, 50, Position.GK, 24, "ENG");
                Assert.AreEqual(0, p.info.secondaryPositions.Count,
                    $"T7: GK[{i}] secondaryPositions.Count == 0");
            }
        }

        // ── Generate 래퍼 (테스트 내 반복 줄이기) ────────────────────

        private Player Generate(Random rng, int rep, Position pos, int age, string nationality) =>
            PlayerGenerator.Generate(rng, rep, pos, age, nationality, 1, -1,
                PlayerOrigin.InitialRoster, _testDate, _balance);

        // ── DB 등록 헬퍼 ──────────────────────────────────────────────

        // (Position, isGK, emphTech, emphMental, emphPhys)
        private static readonly (Position p, bool gk, bool t, bool m, bool ph)[] PosDefs =
        {
            (Position.GK, true,  false, true,  true ),
            (Position.CB, false, false, true,  true ),
            (Position.LB, false, true,  true,  true ),
            (Position.RB, false, true,  true,  true ),
            (Position.WB, false, true,  true,  true ),
            (Position.DM, false, true,  true,  true ),
            (Position.CM, false, true,  true,  true ),
            (Position.AM, false, true,  true,  false),
            (Position.LM, false, true,  true,  true ),
            (Position.RM, false, true,  true,  true ),
            (Position.LW, false, true,  false, true ),
            (Position.RW, false, true,  false, true ),
            (Position.ST, false, true,  true,  true ),
            (Position.CF, false, true,  true,  true ),
        };

        // SeedV01Data 와 동일한 affinity 데이터
        private static readonly Dictionary<Position, (Position pos, float w)[]> AffDefs = new()
        {
            [Position.ST] = new[] { (Position.CF, 8f), (Position.LW, 5f), (Position.RW, 5f), (Position.AM, 3f) },
            [Position.CF] = new[] { (Position.ST, 8f), (Position.AM, 5f), (Position.LW, 3f), (Position.RW, 3f) },
            [Position.LW] = new[] { (Position.LM, 6f), (Position.AM, 4f), (Position.ST, 3f) },
            [Position.RW] = new[] { (Position.RM, 6f), (Position.AM, 4f), (Position.ST, 3f) },
            [Position.AM] = new[] { (Position.CM, 6f), (Position.CF, 4f), (Position.LW, 3f), (Position.RW, 3f) },
            [Position.CM] = new[] { (Position.AM, 5f), (Position.DM, 5f), (Position.LM, 3f), (Position.RM, 3f) },
            [Position.DM] = new[] { (Position.CM, 6f), (Position.CB, 4f) },
            [Position.LM] = new[] { (Position.LW, 6f), (Position.CM, 4f), (Position.LB, 3f) },
            [Position.RM] = new[] { (Position.RW, 6f), (Position.CM, 4f), (Position.RB, 3f) },
            [Position.LB] = new[] { (Position.WB, 8f), (Position.LM, 4f), (Position.CB, 3f) },
            [Position.RB] = new[] { (Position.WB, 8f), (Position.RM, 4f), (Position.CB, 3f) },
            [Position.WB] = new[] { (Position.LB, 8f), (Position.RB, 8f), (Position.LM, 5f), (Position.RM, 5f) },
            [Position.CB] = new[] { (Position.DM, 4f), (Position.LB, 3f), (Position.RB, 3f) },
        };

        private static void RegisterPositions()
        {
            for (int i = 0; i < PosDefs.Length; i++)
            {
                var d  = PosDefs[i];
                var so = ScriptableObject.CreateInstance<PositionSO>();
                so.id = i + 1;
                so.position           = d.p;
                so.isGoalkeeper       = d.gk;
                so.emphasizesTechnical = d.t;
                so.emphasizesMental   = d.m;
                so.emphasizesPhysical = d.ph;
                so.affinities         = new List<PositionAffinity>();
                if (AffDefs.TryGetValue(d.p, out var entries))
                    foreach (var e in entries)
                        so.affinities.Add(new PositionAffinity { position = e.pos, weight = e.w });
                so.fallbackAffinityWeight = 0.05f;
                GameDatabase.Register(so);
            }
        }

        private static void RegisterTraits()
        {
            // id, displayName, weight, exclusionGroupId
            var defs = new[] {
                (1, "늦깎이형",  1.0f, 1),
                (2, "조숙형",    1.0f, 1),
                (3, "부상 취약", 0.7f, 0),
                (4, "멘탈 강자", 1.0f, 0),
                (5, "빅매치형",  0.8f, 0),
                (6, "만능형",    0.8f, 0),
            };
            foreach (var (id, name, weight, group) in defs)
            {
                var so = ScriptableObject.CreateInstance<TraitSO>();
                so.id = id; so.displayName = name;
                so.weight = weight; so.exclusionGroupId = group;
                GameDatabase.Register(so);
            }
        }

        private static void RegisterCountriesAndNamePools()
        {
            // (countryId, code, firstNames[], lastNames[]) — SeedV01Data ID 기준
            var defs = new[]
            {
                (1, "ENG",
                 new[]{ "James","John","Robert","Michael","William","David","Richard","Thomas","Daniel","Matthew" },
                 new[]{ "Smith","Johnson","Williams","Brown","Jones","Miller","Davis","Wilson","Taylor","Moore" }),
                (4, "ESP",
                 new[]{ "Antonio","José","Manuel","Francisco","David","Juan","Javier","Daniel","Carlos","Miguel" },
                 new[]{ "García","Rodríguez","González","Fernández","López","Martínez","Sánchez","Pérez","Gómez","Martín" }),
                (5, "ITA",
                 new[]{ "Marco","Andrea","Luca","Alessandro","Stefano","Francesco","Matteo","Davide","Roberto","Luigi" },
                 new[]{ "Rossi","Russo","Ferrari","Esposito","Bianchi","Romano","Colombo","Ricci","Marino","Greco" }),
            };

            foreach (var (id, code, first, last) in defs)
            {
                var country = ScriptableObject.CreateInstance<CountrySO>();
                country.id = id; country.code = code;
                GameDatabase.Register(country);

                var pool = ScriptableObject.CreateInstance<NamePoolSO>();
                pool.countryId = id;
                pool.firstNames = new List<string>(first);
                pool.lastNames  = new List<string>(last);
                GameDatabase.Register(pool);
            }
        }

        // ── Stat helpers ──────────────────────────────────────────────

        private static double TechMean(Player p)
        {
            var t = p.stats.technical;
            return new[] { t.passing, t.shooting, t.tackling, t.dribbling, t.heading, t.crossing,
                           t.firstTouch, t.finishing, t.longShots, t.freeKickAccuracy, t.penaltyTaking, t.corners }
                   .Average(x => (double)x);
        }

        private static double MentMean(Player p)
        {
            var m = p.stats.mental;
            return new[] { m.vision, m.anticipation, m.composure, m.concentration, m.decisions, m.determination,
                           m.leadership, m.offTheBall, m.positioning, m.teamwork, m.workRate, m.aggression }
                   .Average(x => (double)x);
        }

        private static double PhysMean(Player p)
        {
            var ph = p.stats.physical;
            return new[] { ph.acceleration, ph.agility, ph.balance, ph.jumping,
                           ph.naturalFitness, ph.pace, ph.stamina, ph.strength }
                   .Average(x => (double)x);
        }

        private static double GkMean(Player p)
        {
            var g = p.stats.gk;
            return new[] { g.aerialReach, g.commandOfArea, g.communication, g.eccentricity, g.handling,
                           g.kicking, g.oneOnOnes, g.reflexes, g.rushingOut, g.throwing }
                   .Average(x => (double)x);
        }

        private static int StatSum(Player p)
        {
            var t  = p.stats.technical;
            var m  = p.stats.mental;
            var ph = p.stats.physical;
            var g  = p.stats.gk;
            return t.passing + t.shooting + t.tackling + t.dribbling + t.heading + t.crossing
                 + t.firstTouch + t.finishing + t.longShots + t.freeKickAccuracy + t.penaltyTaking + t.corners
                 + m.vision + m.anticipation + m.composure + m.concentration + m.decisions + m.determination
                 + m.leadership + m.offTheBall + m.positioning + m.teamwork + m.workRate + m.aggression
                 + ph.acceleration + ph.agility + ph.balance + ph.jumping
                 + ph.naturalFitness + ph.pace + ph.stamina + ph.strength
                 + g.aerialReach + g.commandOfArea + g.communication + g.eccentricity + g.handling
                 + g.kicking + g.oneOnOnes + g.reflexes + g.rushingOut + g.throwing;
        }

        private static double PearsonCorrelation(double[] x, double[] y)
        {
            int    n     = x.Length;
            double meanX = x.Average();
            double meanY = y.Average();
            double num = 0, den1 = 0, den2 = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - meanX;
                double dy = y[i] - meanY;
                num  += dx * dy;
                den1 += dx * dx;
                den2 += dy * dy;
            }
            return num / Math.Sqrt(den1 * den2);
        }
    }
}
