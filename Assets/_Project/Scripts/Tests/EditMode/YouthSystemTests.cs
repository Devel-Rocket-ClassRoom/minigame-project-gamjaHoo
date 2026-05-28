// YouthSystemTests.cs
// DoD: algorithms.md #4 Test Scenarios T1~T10. 이슈 #39 / #40 / #41 (Task 10.1/10.2/10.3).

using System;
using System.Collections.Generic;
using System.Linq;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class YouthSystemTests
    {
        private GameBalanceSO _balance;
        private LeagueConfigSO _leagueConfig;
        private readonly DateTime _today = new DateTime(2025, 6, 15);

        [SetUp]
        public void Setup()
        {
            GameDatabase.Clear();
            EventBus.Clear();
            _balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            _balance.youthBaseAvgPA = 80; // 기준값 — 피처 보너스 없을 때 old youthAvgPA=80 과 동일
            _leagueConfig = NewLeagueConfig();
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

        // ── T1. 결정성 ──────────────────────────────────────────────

        [Test]
        public void T1_Determinism_SameInputSameResult()
        {
            var (s1, c1) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 3, seed: 42);
            var (s2, c2) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 3, seed: 42);

            var i1 = YouthSystem.GenerateIntake(c1, s1, _balance, _leagueConfig);
            var i2 = YouthSystem.GenerateIntake(c2, s2, _balance, _leagueConfig);

            Assert.AreEqual(
                i1.candidatePlayerIds.Count,
                i2.candidatePlayerIds.Count,
                "T1: 풀 사이즈 동일"
            );
            for (int i = 0; i < i1.candidatePlayerIds.Count; i++)
            {
                var p1 = s1.GetPlayer(i1.candidatePlayerIds[i]);
                var p2 = s2.GetPlayer(i2.candidatePlayerIds[i]);
                Assert.AreEqual(p1.potentialAbility, p2.potentialAbility, $"T1: [{i}] PA 동일");
                Assert.AreEqual(p1.currentAbility, p2.currentAbility, $"T1: [{i}] CA 동일");
                Assert.AreEqual(
                    p1.info.primaryPosition,
                    p2.info.primaryPosition,
                    $"T1: [{i}] 포지션 동일"
                );
            }
        }

        // ── T2. userActionHash — 자금 1원 차이 → 다른 풀 ─────────────

        [Test]
        public void T2_UserActionHash_AffectsSeed()
        {
            var (s1, c1) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 3, seed: 42);
            var (s2, c2) = BuildScenario(userMoney: 10_000_001, youthCoachLevel: 3, seed: 42);

            var i1 = YouthSystem.GenerateIntake(c1, s1, _balance, _leagueConfig);
            var i2 = YouthSystem.GenerateIntake(c2, s2, _balance, _leagueConfig);

            // 풀 PA 시퀀스가 서로 다른지 (대부분 케이스에서 거의 100% 다름)
            var pa1 = i1
                .candidatePlayerIds.Select(id => s1.GetPlayer(id).potentialAbility)
                .ToList();
            var pa2 = i2
                .candidatePlayerIds.Select(id => s2.GetPlayer(id).potentialAbility)
                .ToList();
            bool anyDiff = pa1.Where((x, idx) => x != pa2[idx]).Any();
            Assert.IsTrue(anyDiff, "T2: 자금 1원 차이로 풀 변동 (직플 영상 공유 차단)");
        }

        // ── T3. 풀 사이즈 (L.3: YouthRecruitment 기준) ───────────────

        [Test]
        public void T3_PoolSize_MatchesYouthRecruitmentPoolSize()
        {
            var (s1, c1) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 1, seed: 42);
            var i1 = YouthSystem.GenerateIntake(c1, s1, _balance, _leagueConfig);
            var facility1 = GameDatabase.GetFacilityLevel(FacilityType.YouthRecruitment, 1);
            Assert.AreEqual(
                facility1.youthPoolSize,
                i1.candidatePlayerIds.Count,
                "T3: Lv1 풀 사이즈"
            );

            var (s5, c5) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 5, seed: 42);
            var i5 = YouthSystem.GenerateIntake(c5, s5, _balance, _leagueConfig);
            var facility5 = GameDatabase.GetFacilityLevel(FacilityType.YouthRecruitment, 5);
            Assert.AreEqual(
                facility5.youthPoolSize,
                i5.candidatePlayerIds.Count,
                "T3: Lv5 풀 사이즈"
            );
        }

        // ── T13. 풀 사이즈 = YouthRecruitment (not YouthCoach) — L.3 회귀 방지 ──

        [Test]
        public void T13_PoolSize_FromYouthRecruitmentNotCoach()
        {
            // youthCoachLevel=1 (pool=15), youthRecruitmentLevel=5 (pool=30)
            // 풀 사이즈는 YouthRecruitment Lv5 = 30
            var (state, club) = BuildScenario(
                userMoney: 10_000_000,
                youthCoachLevel: 1,
                seed: 42,
                youthRecruitmentLevel: 5
            );
            var intake = YouthSystem.GenerateIntake(club, state, _balance, _leagueConfig);
            var expected = GameDatabase.GetFacilityLevel(FacilityType.YouthRecruitment, 5)!.youthPoolSize;
            Assert.AreEqual(
                expected,
                intake.candidatePlayerIds.Count,
                $"T13: 풀 사이즈 = YouthRecruitment Lv5 ({expected}), YouthCoach Lv1 아님"
            );
        }

        // ── T4. PA 분포 — 스타 픽 효과 ────────────────────────────────

        [Test]
        public void T4_PaDistribution_StarPickAppears()
        {
            // Lv3 (avgPA 130) 으로 30 인스펙션 누적 → 약 600명 — 5% ≈ 30명 스타
            var pas = CollectPAs(youthCoachLevel: 3, intakeCount: 30, baseSeed: 100);

            int starCount = pas.Count(pa => pa >= 160); // avgPA 130 + bonus 50 - some σ → 160+ 가 스타 영역
            double starRate = (double)starCount / pas.Count;

            Assert.IsTrue(
                starRate >= 0.01 && starRate <= 0.15,
                $"T4: PA 160+ 비율 1~15% (이론 5% 근처). 실측 {starRate:P1} ({starCount}/{pas.Count})"
            );
        }

        // ── T5. CA-PA 의존성 약화 ───────────────────────────────────

        [Test]
        public void T5_CaPaCorrelation_BelowThreshold()
        {
            var pairs = CollectCaPaPairs(youthCoachLevel: 3, intakeCount: 30, baseSeed: 200);
            double corr = PearsonCorrelation(
                pairs.Select(p => (double)p.ca),
                pairs.Select(p => (double)p.pa)
            );
            Assert.IsTrue(
                corr < 0.85,
                $"T5: CA-PA 상관계수 < 0.85 (PA 추정 어려움). 실측 {corr:F3}"
            );
        }

        // ── T6. 나이 가중치 분포 ────────────────────────────────────

        [Test]
        public void T6_AgeWeightDistribution()
        {
            var ages = CollectAges(youthCoachLevel: 3, intakeCount: 30, baseSeed: 300);
            int n = ages.Count;
            double r16 = (double)ages.Count(a => a == 16) / n;
            double r17 = (double)ages.Count(a => a == 17) / n;
            double r18 = (double)ages.Count(a => a == 18) / n;

            Assert.That(
                r16,
                Is.EqualTo(0.40).Within(0.10),
                $"T6: age 16 ≈ 40% ±10% (실측 {r16:P1})"
            );
            Assert.That(
                r17,
                Is.EqualTo(0.40).Within(0.10),
                $"T6: age 17 ≈ 40% ±10% (실측 {r17:P1})"
            );
            Assert.That(
                r18,
                Is.EqualTo(0.20).Within(0.10),
                $"T6: age 18 ≈ 20% ±10% (실측 {r18:P1})"
            );
        }

        // ── T7. 국적 분포 ───────────────────────────────────────────

        [Test]
        public void T7_NationalityDistribution()
        {
            var nats = CollectNationalities(youthCoachLevel: 3, intakeCount: 30, baseSeed: 400);
            int n = nats.Count;
            double engRatio = (double)nats.Count(c => c == "ENG") / n;
            Assert.That(
                engRatio,
                Is.EqualTo(0.78).Within(0.10),
                $"T7: 자국 (ENG) 비율 78% ±10% (실측 {engRatio:P1})"
            );
        }

        // ── T8. 시설 등급별 평균 PA 차이 ────────────────────────────

        [Test]
        public void T8_FacilityLevel_AveragePaDifference()
        {
            var pas1 = CollectPAs(youthCoachLevel: 1, intakeCount: 30, baseSeed: 500);
            var pas5 = CollectPAs(youthCoachLevel: 5, intakeCount: 30, baseSeed: 600);
            double avg1 = pas1.Average();
            double avg5 = pas5.Average();
            Assert.IsTrue(avg5 > avg1, $"T8: Lv5 평균 PA ({avg5:F1}) > Lv1 평균 PA ({avg1:F1})");
            // 차이 약 50 ±15 (avgPA 80→130 차이 + 스타 픽 영향)
            double diff = avg5 - avg1;
            Assert.That(diff, Is.GreaterThan(30), $"T8: 차이 > 30 (실측 {diff:F1})");
        }

        // ── T12. maxSign 제한 — Lv1=33% / Lv5=100% (L.2 DoD) ──────────

        [Test]
        public void T12_SignPlayers_Lv5_CanSignFullPool()
        {
            var (state, club) = BuildScenario(
                userMoney: 10_000_000,
                youthCoachLevel: 5,
                seed: 42
            );
            club.facilities.youthRecruitmentLevel = 5;
            var intake = YouthSystem.GenerateIntake(club, state, _balance, _leagueConfig);
            var allIds = intake.candidatePlayerIds.ToList();

            Assert.DoesNotThrow(
                () => YouthSystem.SignPlayers(intake, allIds, club, state),
                "T12: Lv5 → 풀 전체 영입 가능 (예외 없어야 함)"
            );
        }

        [Test]
        public void T12b_SignPlayers_Lv1_ExceedLimitThrows()
        {
            var (state, club) = BuildScenario(
                userMoney: 10_000_000,
                youthCoachLevel: 1,
                seed: 42
            );
            club.facilities.youthRecruitmentLevel = 1;
            var intake = YouthSystem.GenerateIntake(club, state, _balance, _leagueConfig);
            int poolSize = intake.candidatePlayerIds.Count; // Lv1 = 15
            // maxSign = Round(15 * 0.33) = Round(4.95) = 5
            var tooMany = intake.candidatePlayerIds.Take(poolSize).ToList(); // 전체 선택 시도

            Assert.Throws<ArgumentException>(
                () => YouthSystem.SignPlayers(intake, tooMany, club, state),
                "T12b: Lv1 → 전체 영입 시도 시 예외"
            );
        }

        // ── T11. CA 캡 — 모든 유스 CA ≤ youthMaxCa (L.1 DoD) ────────

        [Test]
        public void T11_CaCap_AllYouthCaBoundedByYouthMaxCa()
        {
            var players = CollectPlayers(youthCoachLevel: 5, intakeCount: 10, baseSeed: 999);
            foreach (var p in players)
                Assert.LessOrEqual(
                    p.currentAbility,
                    _balance.youthMaxCa,
                    $"T11: CA={p.currentAbility} ≤ youthMaxCa={_balance.youthMaxCa}"
                );
        }

        [Test]
        public void T11b_CaCap_AllYouthCaAboveYouthMinCa()
        {
            var players = CollectPlayers(youthCoachLevel: 1, intakeCount: 10, baseSeed: 888);
            foreach (var p in players)
                Assert.GreaterOrEqual(
                    p.currentAbility,
                    _balance.youthMinCa,
                    $"T11b: CA={p.currentAbility} ≥ youthMinCa={_balance.youthMinCa}"
                );
        }

        // ── T9. Reroll ──────────────────────────────────────────────

        [Test]
        public void T9_Reroll_DecrementsTokenAndChangesPool()
        {
            var (state, club) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 3, seed: 42);
            state.rerollTokens = 3;
            var intake = YouthSystem.GenerateIntake(club, state, _balance, _leagueConfig);
            var beforeIds = intake.candidatePlayerIds.ToList();
            int beforeTokens = state.rerollTokens;

            YouthSystem.UseRerollToken(intake, club, state, _balance, _leagueConfig);

            Assert.AreEqual(beforeTokens - 1, state.rerollTokens, "T9: 토큰 1 차감");
            Assert.AreEqual(1, intake.rerollsUsed, "T9: rerollsUsed += 1");
            Assert.AreEqual(beforeIds.Count, intake.candidatePlayerIds.Count, "T9: 풀 사이즈 유지");
            bool allDifferent = !intake.candidatePlayerIds.SequenceEqual(beforeIds);
            Assert.IsTrue(allDifferent, "T9: 새 ID (이전 풀과 다름)");
            // 이전 후보들 GameState 에서 제거됨
            foreach (var id in beforeIds)
                Assert.IsNull(state.GetPlayer(id), $"T9: 이전 후보 id={id} GameState 제거");
        }

        [Test]
        public void T9b_Reroll_NoTokens_Throws()
        {
            var (state, club) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 3, seed: 42);
            state.rerollTokens = 0;
            var intake = YouthSystem.GenerateIntake(club, state, _balance, _leagueConfig);
            Assert.Throws<InvalidOperationException>(() =>
                YouthSystem.UseRerollToken(intake, club, state, _balance, _leagueConfig)
            );
        }

        // ── T10. SignPlayers ────────────────────────────────────────

        [Test]
        public void T10_SignPlayers_SignsAndRemovesRejected()
        {
            var (state, club) = BuildScenario(userMoney: 10_000_000, youthCoachLevel: 3, seed: 42);
            var intake = YouthSystem.GenerateIntake(club, state, _balance, _leagueConfig);
            var allIds = intake.candidatePlayerIds.ToList();
            int total = allIds.Count;
            Assume.That(total >= 4, "T10 사전: 풀 크기 ≥ 4");

            // 상위 2명 영입
            var signIds = allIds.Take(2).ToList();
            YouthSystem.SignPlayers(intake, signIds, club, state);

            // 영입 검증
            Assert.AreEqual(2, intake.signedPlayerIds.Count, "T10: 2명 영입됨");
            foreach (var id in signIds)
            {
                var p = state.GetPlayer(id);
                Assert.IsNotNull(p, $"T10: 영입자 id={id} GameState 유지");
                Assert.AreEqual(club.id, p.currentClubId, $"T10: 영입자 currentClubId 갱신");
                Assert.IsTrue(club.youthSquadIds.Contains(id), "T10: club.youthSquadIds 추가");
            }

            // 미영입 검증
            var rejectedIds = allIds.Skip(2).ToList();
            Assert.AreEqual(
                rejectedIds.Count,
                intake.rejectedPlayerIds.Count,
                "T10: 미영입 ID 수 일치"
            );
            foreach (var id in rejectedIds)
            {
                Assert.IsNull(state.GetPlayer(id), $"T10: 미영입 id={id} GameState 제거");
                Assert.IsTrue(intake.rejectedPlayerIds.Contains(id), "T10: rejectedPlayerIds 보관");
            }

            // candidatePlayerIds 비워짐
            Assert.IsEmpty(intake.candidatePlayerIds, "T10: candidatePlayerIds.Clear()");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private (GameState state, Club club) BuildScenario(
            int userMoney,
            int youthCoachLevel,
            int seed,
            int youthRecruitmentLevel = -1
        )
        {
            if (youthRecruitmentLevel < 0)
                youthRecruitmentLevel = youthCoachLevel;

            var state = new GameState
            {
                currentDate = _today,
                randomSeed = seed,
                rerollTokens = 3,
                userClubId = 1,
                nextPlayerId = 1,
                nextIntakeId = 1,
            };

            var club = new Club
            {
                id = 1,
                name = "UserClub",
                reputation = 60,
                leagueId = 1,
                facilities = new Facilities
                {
                    scoutLevel = 3,
                    trainingLevel = 3,
                    youthCoachLevel = youthCoachLevel,
                    youthRecruitmentLevel = youthRecruitmentLevel,
                },
                finance = new Finance { money = userMoney },
                seniorSquadIds = new List<int>(),
                youthSquadIds = new List<int>(),
                intakeHistory = new List<YouthIntake>(),
            };
            state.AddClub(club);

            var league = new League
            {
                id = 1,
                configSOId = _leagueConfig.id,
                seasonYear = 2025,
                clubIds = new List<int> { 1 },
                schedule = new List<Match>(),
                standings = new Standings { entries = new List<StandingEntry>() },
            };
            state.leagues.Add(league);

            return (state, club);
        }

        // 누적 인스펙션 — 통계 테스트용. seed 마다 새 시나리오 → 한 시나리오에서 GenerateIntake 호출.
        // nextIntakeId / nextPlayerId 가 자동 증가하면서 매 호출마다 다른 시드 → 풀 다양화.
        private IEnumerable<Player> CollectPlayers(int youthCoachLevel, int intakeCount, int baseSeed)
        {
            var (state, club) = BuildScenario(
                userMoney: 10_000_000,
                youthCoachLevel: youthCoachLevel,
                seed: baseSeed
            );
            var players = new List<Player>();
            for (int i = 0; i < intakeCount; i++)
            {
                var intake = YouthSystem.GenerateIntake(club, state, _balance, _leagueConfig);
                foreach (var id in intake.candidatePlayerIds)
                    players.Add(state.GetPlayer(id));
            }
            return players;
        }

        private List<int> CollectPAs(int youthCoachLevel, int intakeCount, int baseSeed) =>
            CollectPlayers(youthCoachLevel, intakeCount, baseSeed)
                .Select(p => p.potentialAbility)
                .ToList();

        private List<(int ca, int pa)> CollectCaPaPairs(
            int youthCoachLevel,
            int intakeCount,
            int baseSeed
        ) =>
            CollectPlayers(youthCoachLevel, intakeCount, baseSeed)
                .Select(p => (p.currentAbility, p.potentialAbility))
                .ToList();

        private List<int> CollectAges(int youthCoachLevel, int intakeCount, int baseSeed) =>
            CollectPlayers(youthCoachLevel, intakeCount, baseSeed)
                .Select(p => (_today.Year - p.info.birthDate.Year))
                .ToList();

        private List<string> CollectNationalities(int youthCoachLevel, int intakeCount, int baseSeed) =>
            CollectPlayers(youthCoachLevel, intakeCount, baseSeed)
                .Select(p => p.info.nationalityCode)
                .ToList();

        private static double PearsonCorrelation(IEnumerable<double> xs, IEnumerable<double> ys)
        {
            var xa = xs.ToArray();
            var ya = ys.ToArray();
            int n = xa.Length;
            double meanX = xa.Average();
            double meanY = ya.Average();
            double sumXY = 0,
                sumX2 = 0,
                sumY2 = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = xa[i] - meanX;
                double dy = ya[i] - meanY;
                sumXY += dx * dy;
                sumX2 += dx * dx;
                sumY2 += dy * dy;
            }
            double denom = Math.Sqrt(sumX2 * sumY2);
            return denom > 0 ? sumXY / denom : 0;
        }

        // ── GameDatabase fixture ─────────────────────────────────────

        private LeagueConfigSO NewLeagueConfig()
        {
            var cfg = ScriptableObject.CreateInstance<LeagueConfigSO>();
            cfg.id = 1;
            cfg.displayName = "Test EPL";
            cfg.countryCode = "ENG";
            cfg.clubCount = 1;
            cfg.relegationCount = 0;
            cfg.playersPerClub = 25;
            cfg.clubNames = new List<string> { "UserClub" };
            GameDatabase.Register(cfg);
            return cfg;
        }

        private void RegisterFacilityLevels()
        {
            // YouthCoach Lv1~5 — PA 보너스 (L.3: balance.youthBaseAvgPA(80) + bonus = 구 avgPA)
            var coachLevels = new (int lv, int paBonus, float traitChance)[]
            {
                (1, 0, 0.05f),
                (2, 20, 0.08f),
                (3, 50, 0.11f),
                (4, 65, 0.14f),
                (5, 80, 0.18f),
            };
            foreach (var (lv, paBonus, traitChance) in coachLevels)
            {
                var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
                so.facilityType = FacilityType.YouthCoach;
                so.level = lv;
                so.youthAvgPABonus = paBonus;
                so.traitGrantChance = traitChance;
                GameDatabase.Register(so);
            }

            // YouthRecruitment Lv1~5 — poolSize 15/18/21/25/30, signRatio 0.33~1.0
            var recruitLevels = new (int lv, int pool, float ratio)[]
            {
                (1, 15, 0.33f),
                (2, 18, 0.50f),
                (3, 21, 0.67f),
                (4, 25, 0.80f),
                (5, 30, 1.00f),
            };
            foreach (var (lv, pool, ratio) in recruitLevels)
            {
                var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
                so.facilityType = FacilityType.YouthRecruitment;
                so.level = lv;
                so.youthPoolSize = pool;
                so.signRatio = ratio;
                GameDatabase.Register(so);
            }
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
            // ENG + 외국 3개 (FRA / GER / ESP) — 외국 추첨 폴백 가능
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
                        "Jean",
                        "Pierre",
                        "Michel",
                        "Alain",
                        "Philippe",
                        "Bernard",
                        "Claude",
                        "Daniel",
                        "Jacques",
                        "Henri",
                    },
                    new[]
                    {
                        "Martin",
                        "Bernard",
                        "Dubois",
                        "Thomas",
                        "Robert",
                        "Richard",
                        "Petit",
                        "Durand",
                        "Leroy",
                        "Moreau",
                    }
                ),
                (
                    3,
                    "GER",
                    new[]
                    {
                        "Hans",
                        "Peter",
                        "Klaus",
                        "Wolfgang",
                        "Helmut",
                        "Werner",
                        "Heinz",
                        "Karl",
                        "Manfred",
                        "Gerhard",
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
                        "Manuel",
                        "José",
                        "Francisco",
                        "Juan",
                        "David",
                        "José Antonio",
                        "Javier",
                        "José Luis",
                        "Carlos",
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
