// FormationRandomizationTests.cs
// J.7 DoD: 구단 명성 기반 포메이션 추첨 검증.

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
    public class FormationRandomizationTests
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

        // ── T1. 고명성 → Flamboyant 선호 ────────────────────────────────

        [Test]
        public void T1_PickFormation_HighRep_PrefersFlamboyant()
        {
            var solid = MakeFormation(1, 0);
            var flamboyant = MakeFormation(2, 2);
            var formations = new FormationSO[] { solid, flamboyant };
            var rng = new Random(123);

            int flamboyantCount = 0;
            for (int i = 0; i < 200; i++)
                if (ClubGenerator.PickFormation(rng, formations, 0.9f).id == 2)
                    flamboyantCount++;

            Assert.Greater(
                flamboyantCount,
                120,
                $"T1: repNorm=0.9 → Flamboyant 60%+ (actual={flamboyantCount}/200)"
            );
        }

        // ── T2. 저명성 → Solid 선호 ──────────────────────────────────────

        [Test]
        public void T2_PickFormation_LowRep_PrefersSolid()
        {
            var solid = MakeFormation(1, 0);
            var flamboyant = MakeFormation(2, 2);
            var formations = new FormationSO[] { solid, flamboyant };
            var rng = new Random(123);

            int solidCount = 0;
            for (int i = 0; i < 200; i++)
                if (ClubGenerator.PickFormation(rng, formations, 0.1f).id == 1)
                    solidCount++;

            Assert.Greater(
                solidCount,
                120,
                $"T2: repNorm=0.1 → Solid 60%+ (actual={solidCount}/200)"
            );
        }

        // ── T3. 20구단 생성 시 다양한 포메이션 배정 ─────────────────────────

        [Test]
        public void T3_Generate_WithFormations_DiverseFormationsUsed()
        {
            // Solid + Balanced + Flamboyant 3종 등록
            GameDatabase.Register(MakeFormation(10, 0));
            GameDatabase.Register(MakeFormation(11, 1));
            GameDatabase.Register(MakeFormation(12, 2));

            var result = Generate(new Random(42));
            var formationIds = result.Clubs.Select(c => c.tactic.formationId).Distinct().ToList();

            Assert.GreaterOrEqual(
                formationIds.Count,
                2,
                $"T3: 3종 포메이션 등록, 20구단 생성 → 최소 2가지 포메이션 사용 (actual distinct={formationIds.Count})"
            );
        }

        // ── T4. 단일 포메이션 등록 시 모든 구단이 해당 포메이션 사용 ──────────

        [Test]
        public void T4_Generate_SingleFormation_AllClubsUseThatFormation()
        {
            var f532 = MakeFormation532();
            GameDatabase.Register(f532);

            var result = Generate(new Random(42));

            foreach (var club in result.Clubs)
            {
                Assert.AreEqual(
                    6,
                    club.tactic.formationId,
                    $"T4: club {club.id} — 단일 5-3-2 포메이션 등록 시 formationId=6"
                );
                Assert.AreEqual(
                    11,
                    club.tactic.slots.Count,
                    $"T4: club {club.id} — 슬롯 11개"
                );
                Assert.AreEqual(
                    Mentality.Balanced,
                    club.tactic.mentality,
                    $"T4: club {club.id} — mentality=Balanced"
                );
                foreach (var slot in club.tactic.slots)
                    Assert.AreEqual(
                        -1,
                        slot.assignedPlayerId,
                        $"T4: slot {slot.slotIndex} assignedPlayerId=-1 (자동 배정 대기)"
                    );
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private ClubGenerationResult Generate(Random rng) =>
            ClubGenerator.Generate(rng, _leagueConfig, _balance, _testDate, 1, 1, 1);

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

        private static FormationSO MakeFormation(int id, int style)
        {
            var f = ScriptableObject.CreateInstance<FormationSO>();
            f.id = id;
            f.displayName = $"Test_{id}";
            f.formationStyle = style;
            f.slotPositions = new[]
            {
                Position.GK,
                Position.CB,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.CM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.ST,
                Position.ST,
            };
            f.squadConfig = new FormationConfig();
            return f;
        }

        private static FormationSO MakeFormation532()
        {
            var f = ScriptableObject.CreateInstance<FormationSO>();
            f.id = 6;
            f.displayName = "5-3-2";
            f.formationStyle = 0;
            f.slotPositions = new[]
            {
                Position.GK,
                Position.CB,
                Position.CB,
                Position.CB,
                Position.WB,
                Position.WB,
                Position.DM,
                Position.CM,
                Position.CM,
                Position.ST,
                Position.ST,
            };
            f.squadConfig = new FormationConfig
            {
                name = "5-3-2",
                gk = 3,
                cbMin = 5,
                lbMin = 2,
                rbMin = 2,
                dmCmGroupMin = 4,
                lmLwGroupMin = 1,
                rmRwGroupMin = 1,
                stCfGroupMin = 5,
                randomSlots = 2,
            };
            return f;
        }

        // ── Position / Trait / Country 등록 (ClubGeneratorTests 와 동일) ──

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
                so.fallbackAffinityWeight = 0.05f;
                GameDatabase.Register(so);
            }
        }

        private static void RegisterTraits()
        {
            var defs = new[] { (1, "T1", 1.0f, 1), (2, "T2", 1.0f, 1), (3, "T3", 0.7f, 0) };
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
            var countries = new[] { (1, "ENG"), (2, "FRA"), (3, "DEU") };
            foreach (var (id, code) in countries)
            {
                var c = ScriptableObject.CreateInstance<CountrySO>();
                c.id = id;
                c.code = code;
                GameDatabase.Register(c);

                var np = ScriptableObject.CreateInstance<NamePoolSO>();
                np.countryId = id;
                np.firstNames = new List<string> { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };
                np.lastNames = new List<string> { "One", "Two", "Three", "Four", "Five" };
                GameDatabase.Register(np);
            }
        }
    }
}
