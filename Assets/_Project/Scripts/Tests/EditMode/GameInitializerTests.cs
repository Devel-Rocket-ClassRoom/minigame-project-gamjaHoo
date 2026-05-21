// GameInitializerTests.cs
// DoD: data-flows.md #1 시퀀스 검증. T1~T7.

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class GameInitializerTests
    {
        private GameBalanceSO _balance;
        private LeagueConfigSO _leagueConfig;
        private readonly DateTime _seasonStart = new DateTime(2025, 8, 16);

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
        public void T1_Determinism_SameSeedSameInitialState()
        {
            var s1 = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            var s2 = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);

            Assert.AreEqual(s1.allClubs.Count, s2.allClubs.Count);
            Assert.AreEqual(s1.allPlayers.Count, s2.allPlayers.Count);
            Assert.AreEqual(s1.leagues[0].schedule.Count, s2.leagues[0].schedule.Count);

            // 핵심 필드 sample 비교
            for (int i = 0; i < s1.allClubs.Count; i++)
            {
                Assert.AreEqual(s1.allClubs[i].id, s2.allClubs[i].id);
                Assert.AreEqual(s1.allClubs[i].name, s2.allClubs[i].name);
                Assert.AreEqual(s1.allClubs[i].reputation, s2.allClubs[i].reputation);
            }
            for (int i = 0; i < s1.allPlayers.Count; i++)
            {
                Assert.AreEqual(s1.allPlayers[i].id, s2.allPlayers[i].id);
                Assert.AreEqual(s1.allPlayers[i].currentAbility, s2.allPlayers[i].currentAbility);
            }
        }

        // ── T2. ClubGen 결과 등록 ─────────────────────────────────────

        [Test]
        public void T2_AllClubsAndPlayersRegisteredInState()
        {
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);

            Assert.AreEqual(20, state.allClubs.Count, "T2: 20 구단");
            Assert.AreEqual(500, state.allPlayers.Count, "T2: 500 선수");

            // 인덱스 즉시 동작 검증
            Assert.IsNotNull(state.GetClub(1));
            Assert.IsNotNull(state.GetPlayer(1));
            Assert.IsNotNull(state.GetPlayer(500));
        }

        // ── T3. ScheduleGenerator 결과 ────────────────────────────────

        [Test]
        public void T3_ScheduleGenerated_380Matches()
        {
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            var league = state.leagues[0];

            Assert.AreEqual(380, league.schedule.Count, "T3: 380 경기");
            foreach (var m in league.schedule)
            {
                Assert.AreEqual(CompetitionType.League, m.type);
                Assert.That(league.clubIds, Does.Contain(m.homeClubId));
                Assert.That(league.clubIds, Does.Contain(m.awayClubId));
            }
        }

        // ── T4. League.clubIds + Standings 초기화 ─────────────────────

        [Test]
        public void T4_LeagueClubIds_And_StandingsInitialized()
        {
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            var league = state.leagues[0];

            Assert.AreEqual(20, league.clubIds.Count, "T4: League.clubIds 20개");
            Assert.AreEqual(20, league.standings.entries.Count, "T4: Standings 20 entries");

            foreach (var e in league.standings.entries)
            {
                Assert.That(league.clubIds, Does.Contain(e.clubId));
                Assert.AreEqual(0, e.played);
                Assert.AreEqual(0, e.won);
                Assert.AreEqual(0, e.drawn);
                Assert.AreEqual(0, e.lost);
                Assert.AreEqual(0, e.points);
            }
        }

        // ── T5. userClubId == -1 (UI 선택 전) ─────────────────────────

        [Test]
        public void T5_UserClubId_Sentinel_BeforeSelection()
        {
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            Assert.AreEqual(
                -1,
                state.userClubId,
                "T5: GameInitializer 는 userClub 선정 안 함. UI 가 선택 후 설정."
            );
        }

        // ── T6. nextPlayerId == 501 ───────────────────────────────────

        [Test]
        public void T6_NextPlayerId_Updated_After500Players()
        {
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);

            Assert.AreEqual(
                501,
                state.nextPlayerId,
                "T6: ClubGen 500명 (id 1~500) 후 nextPlayerId = 501"
            );
        }

        // ── T7. 메타 필드 ─────────────────────────────────────────────

        [Test]
        public void T7_MetaFields_CorrectlySet()
        {
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);

            Assert.AreEqual(42, state.randomSeed);
            Assert.AreEqual(_seasonStart, state.currentDate);
            Assert.AreEqual(_balance.initialRerollTokens, state.rerollTokens);
            Assert.AreEqual(1, state.leagues.Count, "T7: 단일 리그");
            Assert.AreEqual(1, state.leagues[0].id);
            Assert.AreEqual(_seasonStart.Year, state.leagues[0].seasonYear);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private LeagueConfigSO NewLeagueConfig()
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfigSO>();
            cfg.id = 1;
            cfg.displayName = "Test EPL";
            cfg.countryCode = "ENG";
            cfg.clubCount = 20;
            cfg.relegationCount = 3;
            cfg.playersPerClub = 25;
            cfg.clubNames = Enumerable.Range(1, 20).Select(i => $"Club {i:D2}").ToList();
            return cfg;
        }

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
    }
}
