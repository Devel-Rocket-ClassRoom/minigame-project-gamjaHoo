// IntegrationTests.cs
// V0.1 Stage 15 통합 테스트 — 165 단위 테스트가 검증 못 한 시스템 간 연결 검증.
// Task 15.1: 한 시즌 완주 / Task 15.2: 세이브 일관성 / Task 15.3: 시드 결정성.
//
// 4 팀 리그로 빠른 시즌 검증 (12 매치 × 1 시즌 = 80일 만에 시즌 종료).
// 365 일 진행 시 시즌 종료 (5/15) + 신규 시즌 시작 (6/1) + 새 시즌 매치 개막 (8/15) 도 검증.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using FMLite.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class IntegrationTests
    {
        private GameBalanceSO _balance;
        private LeagueConfigSO _leagueConfig;

        // 프리시즌 시작 (7/1). 첫 매치는 GameBalanceSO.newSeasonOpening (8/15) 부터.
        private readonly DateTime _seasonStart = new DateTime(2025, 7, 1);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _leagueConfig = NewLeagueConfig(clubCount: 4);
            GameDatabase.Register(_balance);
            GameDatabase.Register(_leagueConfig);
            RegisterPositions();
            RegisterTraits();
            RegisterCountriesAndNamePools();
            RegisterFacilityLevels();
        }

        [TearDown]
        public void TearDown()
        {
            GameDatabase.Clear();
            EventBus.Clear();
        }

        // ════════════════════════════════════════════════════════════════
        // Task 15.1 — 한 시즌 완주
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void T1_FullSeasonCompletion_AllMatchesProcessed()
        {
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            state.userClubId = 1;
            GameTime.Reset(state.currentDate);

            var seasonEndedReceived = 0;
            var seasonStartedReceived = 0;
            Action<SeasonEndedEvent> hEnd = _ => seasonEndedReceived++;
            Action<SeasonStartedEvent> hStart = _ => seasonStartedReceived++;
            EventBus.Subscribe(hEnd);
            EventBus.Subscribe(hStart);

            // 첫 시즌 매치 일정 백업 (검증용)
            var firstSeasonSchedule = state.leagues[0].schedule.ToList();
            int firstSeasonMatchCount = firstSeasonSchedule.Count;

            // 365일 진행 — 8/15 출발 → 다음 해 8/14
            for (int day = 0; day < 365; day++)
                GameLoop.AdvanceDay(state, _balance);

            // 1. 첫 시즌 모든 매치 처리됨
            foreach (var m in firstSeasonSchedule)
                Assert.IsNotNull(m.result, $"T1: 첫 시즌 match.id={m.id} result 채워짐");

            // 2. 첫 시즌 매치 결과 데이터 — I.2 이벤트 종류 도입 후 골 누적 발생.
            //    standings 는 NewSeasonProcessor 6/1 호출 시 초기화되어 365일 종료 시점에 0 으로 돌아감.
            int totalGoals = firstSeasonSchedule.Sum(m =>
                (m.result?.homeScore ?? 0) + (m.result?.awayScore ?? 0)
            );
            Assert.Greater(
                totalGoals,
                0,
                "T1: 첫 시즌 매치 골 누적 (MatchPostProcessor 가 result 채움)"
            );

            // 3. 시즌 종료 / 신규 시즌 시작 이벤트 발행
            Assert.GreaterOrEqual(seasonEndedReceived, 1, "T1: SeasonEndedEvent ≥ 1");
            Assert.GreaterOrEqual(seasonStartedReceived, 1, "T1: SeasonStartedEvent ≥ 1");

            // 4. 신규 시즌 일정 생성됨 — seasonYear 갱신
            Assert.AreEqual(2026, state.leagues[0].seasonYear, "T1: seasonYear +1");

            // 5. 새 일정의 첫 매치 8/15 (새 시즌 개막)
            var newSchedule = state.leagues[0].schedule;
            Assert.IsTrue(
                newSchedule.Any(m => m.date.Month == 8 && m.date.Day == 15),
                "T1: 새 시즌 일정 8/15 매치 존재"
            );

            EventBus.Unsubscribe(hEnd);
            EventBus.Unsubscribe(hStart);
        }

        [Test]
        public void T1b_FullSeasonCompletion_DataIntegrity()
        {
            // 한 시즌 후 데이터 정합성 — squad / standings 합산 / 카운터 단조증가
            var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            state.userClubId = 1;
            GameTime.Reset(state.currentDate);

            int initialPlayerCount = state.allPlayers.Count;
            int initialNextPlayerId = state.nextPlayerId;

            for (int day = 0; day < 365; day++)
                GameLoop.AdvanceDay(state, _balance);

            // Player 수 변동 — 유스 인스펙션 추가 + 은퇴 / FA 제거. 초기 ± 가능
            Assert.Greater(state.allPlayers.Count, 0, "T1b: 선수 존재");

            // nextPlayerId 단조증가 (인스펙션 풀 추가)
            Assert.GreaterOrEqual(
                state.nextPlayerId,
                initialNextPlayerId,
                "T1b: nextPlayerId 증가 (유스 인스펙션)"
            );

            // 모든 클럽 squad 정합성 — seniorSquadIds 의 모든 ID 가 GameState 에 존재
            foreach (var club in state.allClubs)
            {
                foreach (var id in club.seniorSquadIds)
                    Assert.IsNotNull(
                        state.GetPlayer(id),
                        $"T1b: club {club.id} senior id={id} 존재"
                    );
                foreach (var id in club.youthSquadIds)
                    Assert.IsNotNull(
                        state.GetPlayer(id),
                        $"T1b: club {club.id} youth id={id} 존재"
                    );
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Task 15.2 — 세이브 일관성
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void T2_SaveLoad_Consistency()
        {
            const string Slot = "_integration_test_save";
            try
            {
                var state = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
                state.userClubId = 1;
                GameTime.Reset(state.currentDate);

                // 60일 진행 (매치 일부 처리됨)
                for (int day = 0; day < 60; day++)
                    GameLoop.AdvanceDay(state, _balance);

                int beforePlayerCount = state.allPlayers.Count;
                int beforeNextPlayerId = state.nextPlayerId;
                int beforeNextIntakeId = state.nextIntakeId;
                int beforeNextOfferId = state.nextOfferId;
                int beforeMatchesPlayed = state.leagues[0].standings.entries.Sum(e => e.played);
                int beforeTokens = state.rerollTokens;
                var beforeDate = state.currentDate;

                // 저장 → 로드
                SaveSystem.Save(state, Slot);
                var loaded = SaveSystem.Load(Slot);
                Assert.IsNotNull(loaded, "T2: Load 성공");

                // 카운터 라운드트립
                Assert.AreEqual(beforePlayerCount, loaded.allPlayers.Count, "T2: allPlayers.Count");
                Assert.AreEqual(beforeNextPlayerId, loaded.nextPlayerId, "T2: nextPlayerId");
                Assert.AreEqual(beforeNextIntakeId, loaded.nextIntakeId, "T2: nextIntakeId");
                Assert.AreEqual(beforeNextOfferId, loaded.nextOfferId, "T2: nextOfferId");
                Assert.AreEqual(beforeTokens, loaded.rerollTokens, "T2: rerollTokens");
                Assert.AreEqual(beforeDate, loaded.currentDate, "T2: currentDate");

                // 매치 진행 데이터
                int loadedMatchesPlayed = loaded.leagues[0].standings.entries.Sum(e => e.played);
                Assert.AreEqual(beforeMatchesPlayed, loadedMatchesPlayed, "T2: matchesPlayed 합");

                // 인덱스 자동 복원 — GetPlayer 동작
                if (state.allPlayers.Count > 0)
                {
                    var samplePlayerId = state.allPlayers[0].id;
                    Assert.IsNotNull(
                        loaded.GetPlayer(samplePlayerId),
                        $"T2: 로드 후 GetPlayer(id={samplePlayerId}) 동작 (BuildIndexes)"
                    );
                }
            }
            finally
            {
                SaveSystem.DeleteSlot(Slot);
            }
        }

        [Test]
        public void T2b_LoadThenContinue_SameAsOriginal()
        {
            const string Slot = "_integration_test_continue";
            try
            {
                var state1 = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
                state1.userClubId = 1;
                GameTime.Reset(state1.currentDate);
                for (int day = 0; day < 30; day++)
                    GameLoop.AdvanceDay(state1, _balance);

                // 저장 → 다른 변수로 로드
                SaveSystem.Save(state1, Slot);
                var state2 = SaveSystem.Load(Slot);

                // GameTime 동기화 (저장 시점 기준)
                GameTime.Reset(state2.currentDate);

                // 양쪽 동일하게 추가 30일 진행
                for (int day = 0; day < 30; day++)
                {
                    GameTime.Reset(state1.currentDate);
                    GameLoop.AdvanceDay(state1, _balance);
                    GameTime.Reset(state2.currentDate);
                    GameLoop.AdvanceDay(state2, _balance);
                }

                // 데이터 정합성 — 매치 결과 동일
                int played1 = state1.leagues[0].standings.entries.Sum(e => e.played);
                int played2 = state2.leagues[0].standings.entries.Sum(e => e.played);
                Assert.AreEqual(played1, played2, "T2b: 같은 매치 수");
                Assert.AreEqual(state1.currentDate, state2.currentDate, "T2b: 같은 currentDate");
            }
            finally
            {
                SaveSystem.DeleteSlot(Slot);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Task 15.3 — 시드 결정성
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void T3_SeedDeterminism_SameSeedSameProgression()
        {
            var s1 = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            var s2 = GameInitializer.NewGame(42, _seasonStart, _leagueConfig, _balance);
            s1.userClubId = s2.userClubId = 1;

            // 두 시뮬레이션 각자 GameTime reset 후 동일 일수 진행
            for (int day = 0; day < 100; day++)
            {
                GameTime.Reset(s1.currentDate);
                GameLoop.AdvanceDay(s1, _balance);
                GameTime.Reset(s2.currentDate);
                GameLoop.AdvanceDay(s2, _balance);
            }

            // 1. 선수 수 / nextId 동일
            Assert.AreEqual(s1.allPlayers.Count, s2.allPlayers.Count, "T3: 선수 수");
            Assert.AreEqual(s1.nextPlayerId, s2.nextPlayerId, "T3: nextPlayerId");
            Assert.AreEqual(s1.nextIntakeId, s2.nextIntakeId, "T3: nextIntakeId");

            // 2. 각 선수의 currentClubId / CA / PA 동일
            for (int i = 0; i < s1.allPlayers.Count; i++)
            {
                Assert.AreEqual(s1.allPlayers[i].id, s2.allPlayers[i].id, $"T3: player[{i}].id");
                Assert.AreEqual(
                    s1.allPlayers[i].currentClubId,
                    s2.allPlayers[i].currentClubId,
                    $"T3: player[{i}].currentClubId"
                );
                Assert.AreEqual(
                    s1.allPlayers[i].currentAbility,
                    s2.allPlayers[i].currentAbility,
                    $"T3: player[{i}].currentAbility"
                );
            }

            // 3. League standings 완전 동일
            for (int i = 0; i < s1.leagues[0].standings.entries.Count; i++)
            {
                var e1 = s1.leagues[0].standings.entries[i];
                var e2 = s2.leagues[0].standings.entries[i];
                Assert.AreEqual(e1.played, e2.played, $"T3: entries[{i}].played");
                Assert.AreEqual(e1.won, e2.won, $"T3: entries[{i}].won");
                Assert.AreEqual(e1.points, e2.points, $"T3: entries[{i}].points");
                Assert.AreEqual(e1.goalsFor, e2.goalsFor, $"T3: entries[{i}].goalsFor");
            }
        }

        [Test]
        public void T3b_DifferentSeeds_DifferentResults()
        {
            var s1 = GameInitializer.NewGame(seed: 42, _seasonStart, _leagueConfig, _balance);
            var s2 = GameInitializer.NewGame(seed: 99, _seasonStart, _leagueConfig, _balance);
            s1.userClubId = s2.userClubId = 1;

            for (int day = 0; day < 100; day++)
            {
                GameTime.Reset(s1.currentDate);
                GameLoop.AdvanceDay(s1, _balance);
                GameTime.Reset(s2.currentDate);
                GameLoop.AdvanceDay(s2, _balance);
            }

            // 다른 시드 → 다른 매치 결과 (대부분 케이스)
            int played1 = s1.leagues[0].standings.entries.Sum(e => e.played);
            int played2 = s2.leagues[0].standings.entries.Sum(e => e.played);
            // 같은 시점이라 매치 수는 같음
            Assert.AreEqual(played1, played2, "T3b: 같은 일자 — 같은 매치 수");

            // 그러나 점수 분포가 완전 동일하면 의심 — 어딘가가 같은 시드
            bool anyDifferent = false;
            for (int i = 0; i < s1.leagues[0].standings.entries.Count; i++)
            {
                if (
                    s1.leagues[0].standings.entries[i].points
                    != s2.leagues[0].standings.entries[i].points
                )
                {
                    anyDifferent = true;
                    break;
                }
            }
            Assert.IsTrue(anyDifferent, "T3b: 다른 시드 → standings 다름 (적어도 한 항목)");
        }

        // ════════════════════════════════════════════════════════════════
        // R.3 재정 측정 하네스 (#74 / G.5 패턴) — 한 시즌 완주 후 구단별 순현금흐름.
        // Test Runner 콘솔의 [재정측정] 로그로 tier별 본전±/폭주 여부 확인 → GameBalance 튜닝.
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void T_FinanceMeasurement_FullSeasonNetCashFlow()
        {
            // 20구단 — tier 스펙트럼 확보 (Setup 의 4구단 대신 별도 config).
            var cfg = NewLeagueConfig(clubCount: 20);
            GameDatabase.Register(cfg);

            var state = GameInitializer.NewGame(7, _seasonStart, cfg, _balance);
            state.userClubId = -1; // 유저 매치 on-demand 스킵 회피 → 전 구단 백그라운드 시뮬
            GameTime.Reset(state.currentDate);

            // 시작 잔고 + 연 임금 스냅샷
            var startMoney = new Dictionary<int, long>();
            var annualWage = new Dictionary<int, long>();
            foreach (var c in state.allClubs)
            {
                startMoney[c.id] = c.finance?.money ?? 0;
                long weekly = 0;
                foreach (int pid in c.seniorSquadIds)
                {
                    var p = state.GetPlayer(pid);
                    if (p?.contract != null)
                        weekly += p.contract.weeklyWage;
                }
                annualWage[c.id] = weekly * 52;
            }

            // 7/1 → ~5/27 (시즌말 5/15 상금 포함, 6/1 회계연도 리셋 전)
            for (int day = 0; day < 330; day++)
                GameLoop.AdvanceDay(state, _balance);

            // tier 분류 (rep) + 집계
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                "[재정측정] 한 시즌 순현금흐름 (rep / 시작잔고 / 연임금 / 순흐름 / 종료잔고)"
            );
            var byTier = new Dictionary<string, List<long>>
            {
                ["Top4(85+)"] = new(),
                ["Euro(65-84)"] = new(),
                ["Mid(45-64)"] = new(),
                ["Rel(<45)"] = new(),
            };
            foreach (var c in state.allClubs)
            {
                long end = c.finance?.money ?? 0;
                long net = end - startMoney[c.id];
                long wage = annualWage[c.id];
                string tier =
                    c.reputation >= 85 ? "Top4(85+)"
                    : c.reputation >= 65 ? "Euro(65-84)"
                    : c.reputation >= 45 ? "Mid(45-64)"
                    : "Rel(<45)";
                byTier[tier].Add(net);
                sb.AppendLine(
                    $"  rep{c.reputation,3} | start {startMoney[c.id] / 1_000_000.0,6:F1}M "
                        + $"| wage {wage / 1_000_000.0,5:F1}M/y | net {net / 1_000_000.0,7:F1}M "
                        + $"| end {end / 1_000_000.0,7:F1}M  ({c.name})"
                );
            }
            sb.AppendLine("  ── tier 평균 순흐름 ──");
            foreach (var kv in byTier)
            {
                if (kv.Value.Count == 0)
                    continue;
                double avg = 0;
                foreach (var v in kv.Value)
                    avg += v;
                avg /= kv.Value.Count;
                sb.AppendLine($"  {kv.Key,-12}: {avg / 1_000_000.0,7:F1}M  (n={kv.Value.Count})");
            }
            sb.AppendLine("  목표: 중위 본전±, 빅클럽 비폭주, 강등권 적자 과도하지 않게");
            Debug.Log(sb.ToString());

            // 느슨한 sanity — 시즌 진행 + 재정 동작 확인 (수치 합격은 로그로 사람이 판단).
            Assert.AreEqual(20, state.allClubs.Count, "측정: 20구단");
            bool anyWageOutflow = false;
            foreach (var c in state.allClubs)
            {
                if (annualWage[c.id] > 0)
                    anyWageOutflow = true;
            }
            Assert.IsTrue(anyWageOutflow, "측정: 주급 데이터 존재(유출 발생 전제)");
        }

        // ════════════════════════════════════════════════════════════════
        // Helpers — GameDatabase fixture (GameInitializerTests 패턴 재사용)
        // ════════════════════════════════════════════════════════════════

        private static LeagueConfigSO NewLeagueConfig(int clubCount)
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfigSO>();
            cfg.id = 1;
            cfg.displayName = "Test EPL";
            cfg.countryCode = "ENG";
            cfg.clubCount = clubCount;
            cfg.relegationCount = 0;
            cfg.playersPerClub = 25;
            cfg.clubNames = Enumerable.Range(1, clubCount).Select(i => $"Club {i:D2}").ToList();
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

        private static void RegisterFacilityLevels()
        {
            var recruitDefs = new (int lv, int pool, float signRatio)[]
            {
                (1, 15, 0.33f),
                (2, 18, 0.45f),
                (3, 21, 0.60f),
                (4, 25, 0.80f),
                (5, 30, 1.00f),
            };
            foreach (var (lv, pool, ratio) in recruitDefs)
            {
                var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
                so.facilityType = FacilityType.YouthRecruitment;
                so.level = lv;
                so.youthPoolSize = pool;
                so.signRatio = ratio;
                GameDatabase.Register(so);
            }

            var coachDefs = new (int lv, int paBonus, float traitChance)[]
            {
                (1, 0, 0.05f),
                (2, 20, 0.08f),
                (3, 30, 0.11f),
                (4, 45, 0.14f),
                (5, 60, 0.18f),
            };
            foreach (var (lv, paBonus, traitChance) in coachDefs)
            {
                var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
                so.facilityType = FacilityType.YouthCoach;
                so.level = lv;
                so.youthAvgPABonus = paBonus;
                so.traitGrantChance = traitChance;
                GameDatabase.Register(so);
            }
        }
    }
}
