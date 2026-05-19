// StartingSquadGachaTests.cs
// DoD: algorithms.md #6 Test Scenarios T1~T7.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using FMLite.Domain;
using FMLite.Application;
using Random = System.Random;

namespace FMLite.Tests
{
    public class StartingSquadGachaTests
    {
        private GameBalanceSO   _balance;
        private LeagueConfigSO  _leagueConfig;
        private readonly DateTime _testDate = new DateTime(2025, 8, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            _balance      = ScriptableObject.CreateInstance<GameBalanceSO>();
            _leagueConfig = NewLeagueConfig();
            RegisterPositions();
            RegisterTraits();
            RegisterCountriesAndNamePools();
        }

        [TearDown]
        public void TearDown() => GameDatabase.Clear();

        // ── T1. 라인 분류 정확성 ──────────────────────────────────────

        [Test]
        public void T1_LineOf_AllPositionsExactlyOneLine()
        {
            var assignments = new Dictionary<Position, Line>
            {
                [Position.GK] = Line.GK,
                [Position.CB] = Line.DF, [Position.LB] = Line.DF, [Position.RB] = Line.DF, [Position.WB] = Line.DF,
                [Position.DM] = Line.MF, [Position.CM] = Line.MF, [Position.AM] = Line.MF, [Position.LM] = Line.MF, [Position.RM] = Line.MF,
                [Position.LW] = Line.AT, [Position.RW] = Line.AT, [Position.ST] = Line.AT, [Position.CF] = Line.AT,
            };
            foreach (var (pos, expected) in assignments)
                Assert.AreEqual(expected, StartingSquadGacha.LineOf(pos), $"T1: {pos} → {expected}");
        }

        // ── T2. 명성 대비 비율 — Strong 케이스 ────────────────────────

        [Test]
        public void T2_RatioBasedTier_StrongCase()
        {
            // rep=50, expectedMean = caRepBase + 0.8*50 = 60 + 40 = 100
            var (state, club) = BuildStateWithSingleClub(rep: 50, gkLineCA: 110, otherLineCA: 100);
            var eval = StartingSquadGacha.EvaluateSquad(club, state, _balance);

            // ratio = 110 / 100 = 1.10 → Strong (>= 1.05, < 1.20)
            Assert.AreEqual(TierGrade.Strong, eval.gk,
                            "T2: GK 라인 CA 110, rep=50 → ratio 1.10 → Strong");
        }

        // ── T3. 명성 대비 — 같은 CA 다른 평가 ────────────────────────

        [Test]
        public void T3_SameAbsoluteCA_DifferentTiersByReputation()
        {
            // 빅클럽 (rep=90, expectedMean=60+72=132): AT 라인 평균 120 → ratio 0.91 → Average
            var (state1, club1) = BuildStateWithSingleClub(rep: 90, atLineCA: 120, otherLineCA: 130);
            var eval1 = StartingSquadGacha.EvaluateSquad(club1, state1, _balance);
            Assert.AreEqual(TierGrade.Average, eval1.at,
                            "T3: rep=90 + AT CA 120 → ratio≈0.91 → Average");

            // 중위권 (rep=50, expectedMean=100): AT 라인 평균 120 → ratio 1.20 → Elite
            var (state2, club2) = BuildStateWithSingleClub(rep: 50, atLineCA: 120, otherLineCA: 100);
            var eval2 = StartingSquadGacha.EvaluateSquad(club2, state2, _balance);
            Assert.AreEqual(TierGrade.Elite, eval2.at,
                            "T3: rep=50 + AT CA 120 → ratio 1.20 → Elite");

            // design-decisions.md #15 의 "빅클럽 평범 ≈ 중위권 훌륭" 구현 검증
        }

        // ── T4. ACE 마커 ──────────────────────────────────────────────

        [Test]
        public void T4_AceMarker_PointsToTopAbilityLine()
        {
            // GK 라인은 평범, ST 라인에 1명만 CA +50 → ACE = AT
            var (state, club) = BuildStateWithSingleClub(rep: 50, gkLineCA: 100, otherLineCA: 100);

            // ST 포지션 선수 1명을 찾아 CA = 200 으로 변경
            var stPlayer = club.seniorSquadIds
                .Select(id => state.GetPlayer(id))
                .First(p => p.info.primaryPosition == Position.ST);
            stPlayer.currentAbility = 200;

            var eval = StartingSquadGacha.EvaluateSquad(club, state, _balance);
            Assert.AreEqual(Line.AT, eval.acePosition, "T4: ACE = AT (ST 라인 최고 CA)");
            Assert.AreEqual(200, eval.aceLineCA, "T4: aceLineCA = 200");
        }

        // ── T5. Reroll 결정성 + 새 id ─────────────────────────────────

        [Test]
        public void T5_Reroll_Determinism_AndFreshIds()
        {
            var (s1, c1) = BuildStateWithSingleClub(rep: 50, gkLineCA: 100, otherLineCA: 100);
            var (s2, c2) = BuildStateWithSingleClub(rep: 50, gkLineCA: 100, otherLineCA: 100);

            var beforeIds1 = c1.seniorSquadIds.ToList();
            var beforeIds2 = c2.seniorSquadIds.ToList();

            var e1 = StartingSquadGacha.RerollSquad(c1, s1, _leagueConfig, _balance, _testDate, new Random(42));
            var e2 = StartingSquadGacha.RerollSquad(c2, s2, _leagueConfig, _balance, _testDate, new Random(42));

            // 결정성 — 같은 seed → 같은 평가 결과
            Assert.AreEqual(e1.gk, e2.gk, "T5: 결정성 gk");
            Assert.AreEqual(e1.df, e2.df, "T5: 결정성 df");
            Assert.AreEqual(e1.mf, e2.mf, "T5: 결정성 mf");
            Assert.AreEqual(e1.at, e2.at, "T5: 결정성 at");
            Assert.AreEqual(e1.acePosition, e2.acePosition, "T5: 결정성 acePosition");

            // 새 id — 기존 id 와 겹치지 않음
            foreach (int oldId in beforeIds1)
                Assert.IsFalse(c1.seniorSquadIds.Contains(oldId),
                               $"T5: 새 스쿼드에 기존 id {oldId} 가 남아있으면 안 됨");
        }

        // ── T6. Reroll 토큰 부족 ──────────────────────────────────────

        [Test]
        public void T6_Reroll_NoTokens_Throws()
        {
            var (state, club) = BuildStateWithSingleClub(rep: 50, gkLineCA: 100, otherLineCA: 100);
            state.rerollTokens = 0;

            Assert.Throws<InvalidOperationException>(() =>
                StartingSquadGacha.RerollSquad(club, state, _leagueConfig, _balance, _testDate, new Random(42)));
        }

        // ── T7. 라인 비어있음 → Poor ──────────────────────────────────

        [Test]
        public void T7_EmptyLine_ReturnsPoor()
        {
            var state = new GameState();
            var club  = new Club { id = 1, reputation = 50 };
            state.AddClub(club);
            // 의도적으로 GK 0명 (다른 라인은 1명씩만)
            int nextId = 1;
            var lb = new Player { id = nextId++, currentAbility = 100, info = new PersonalInfo { primaryPosition = Position.LB } };
            var cm = new Player { id = nextId++, currentAbility = 100, info = new PersonalInfo { primaryPosition = Position.CM } };
            var st = new Player { id = nextId++, currentAbility = 100, info = new PersonalInfo { primaryPosition = Position.ST } };
            state.AddPlayer(lb); state.AddPlayer(cm); state.AddPlayer(st);
            club.seniorSquadIds.AddRange(new[] { lb.id, cm.id, st.id });

            var eval = StartingSquadGacha.EvaluateSquad(club, state, _balance);
            Assert.AreEqual(TierGrade.Poor, eval.gk, "T7: GK 0명 → Poor");
        }

        // ── Helpers ───────────────────────────────────────────────────

        // 단일 구단 + 25명 합성 스쿼드 (분배표 정확히 따르지 않고 라인 CA 수동 제어).
        // (state, club) 반환. ClubGen 의 BuildSquadComposition 대신 테스트용 고정 분배.
        private (GameState state, Club club) BuildStateWithSingleClub(
            int rep, int gkLineCA = 100, int otherLineCA = 100,
            int? atLineCA = null)
        {
            var state = new GameState { userClubId = 1, rerollTokens = 3 };
            var club  = new Club { id = 1, reputation = rep, name = "TestFC" };
            state.AddClub(club);

            // 분배: GK 3 / CB 4 / LB 2 / RB 2 / DM 2 / CM 2 / LM 1 / LW 1 / RM 1 / RW 1 / ST 2 / CF 2 (= 23)
            // 남은 2자리: CB +1, ST +1 (총 25)
            var composition = new (Position pos, int count)[]
            {
                (Position.GK, 3), (Position.CB, 5), (Position.LB, 2), (Position.RB, 2),
                (Position.DM, 2), (Position.CM, 2), (Position.LM, 1), (Position.LW, 1),
                (Position.RM, 1), (Position.RW, 1), (Position.ST, 3), (Position.CF, 2),
            };

            int nextId = 1;
            foreach (var (pos, count) in composition)
            {
                var line = StartingSquadGacha.LineOf(pos);
                int ca = line switch
                {
                    Line.GK => gkLineCA,
                    Line.AT => atLineCA ?? otherLineCA,
                    _       => otherLineCA,
                };
                for (int i = 0; i < count; i++)
                {
                    var p = new Player
                    {
                        id = nextId,
                        currentAbility = ca,
                        info = new PersonalInfo { primaryPosition = pos },
                    };
                    state.AddPlayer(p);
                    club.seniorSquadIds.Add(nextId);
                    nextId++;
                }
            }
            state.nextPlayerId = nextId;     // ClubGen 시뮬레이션
            return (state, club);
        }

        private LeagueConfigSO NewLeagueConfig()
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfigSO>();
            cfg.id = 1; cfg.displayName = "Test EPL"; cfg.countryCode = "ENG";
            cfg.clubCount = 1; cfg.relegationCount = 0; cfg.playersPerClub = 25;
            cfg.clubNames = new List<string> { "TestFC" };
            return cfg;
        }

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
                (2, "조숙형",   1.0f, 1),
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
            var defs = new[]
            {
                (1, "ENG",
                 new[]{ "James","John","Robert","Michael","William","David","Richard","Thomas","Daniel","Matthew" },
                 new[]{ "Smith","Johnson","Williams","Brown","Jones","Miller","Davis","Wilson","Taylor","Moore" }),
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
    }
}
