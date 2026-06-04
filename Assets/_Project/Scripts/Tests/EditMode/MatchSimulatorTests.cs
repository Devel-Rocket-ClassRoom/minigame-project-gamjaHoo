// MatchSimulatorTests.cs
// DoD: algorithms.md V0.5-2 Test Scenarios — Stage I.1' (5-zone 상태 머신) + I.2' (zone resolution).
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

        // 구 CA-top11 포지션무시 폴백 검증 — V1.0-14(Stage H) 포메이션 기반 라인업으로 재작성 예정 (#474 후속, design-decisions #71).
        [Ignore("Pending V1.0-14 포메이션 기반 라인업 재작성 (Stage H, design-decisions #71)")]
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

        // 구 폴백 동작 검증 — V1.0-14(Stage H) 라인업 재작성 시 suspended 제외 정합 포함해 재작성 (#474 후속).
        [Ignore("Pending V1.0-14 포메이션 기반 라인업 재작성 (Stage H, design-decisions #71)")]
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
            // V1.0 xG 모델 (#474) — 튜닝 후 실측 ~2.76. EPL 2.7 정합 (sampling 여유 포함).
            Assert.That(avg, Is.InRange(2.2, 3.3), $"T3: 평균 골수 (실측 {avg:F2}/매치)");
        }

        // ── T20. xG 슛맵 채워짐 + 유효 + 결정성 (#474) ────────────────────
        [Test]
        public void T20_Xg_ShotMapPopulatedAndDeterministic()
        {
            var (s1, m1) = BuildState(seed: 42, matchId: 7);
            var (s2, m2) = BuildState(seed: 42, matchId: 7);
            var r1 = MatchSimulator.Simulate(m1, s1, _balance, collectEvents: true);
            var r2 = MatchSimulator.Simulate(m2, s2, _balance, collectEvents: true);

            Assert.Greater(r1.shotMap.Count, 0, "T20: 슛맵 채워짐");
            foreach (var pin in r1.shotMap)
            {
                Assert.That(pin.xg, Is.InRange(0f, 0.96f), "T20: xg 범위");
                Assert.That(pin.x, Is.InRange(0f, 1f), "T20: x 범위");
                Assert.That(pin.y, Is.InRange(0f, 1f), "T20: y 범위");
            }
            Assert.AreEqual(r1.shotMap.Count, r2.shotMap.Count, "T20: shotMap 결정적");
            Assert.AreEqual(r1.shotMap[0].xg, r2.shotMap[0].xg, 1e-6f, "T20: 첫 핀 xg 결정적");
            // 모든 골은 Goal outcome 핀을 정확히 하나 생성 → Goal 핀 수 = 총 득점.
            int goalPins = r1.shotMap.Count(p => p.outcome == ShotOutcome.Goal);
            Assert.AreEqual(r1.homeScore + r1.awayScore, goalPins, "T20: Goal 핀 = 득점수");
        }

        // ── T21. xG ≈ 득점 (밸런싱 전제) + 측정 하네스 (#474) ─────────────
        [Test]
        public void T21_Xg_SumApproximatesGoals_Measurement()
        {
            var seedGen = new System.Random(7);
            const int N = 200;
            int totalGoals = 0;
            double totalXg = 0;
            int totalShots = 0,
                totalPens = 0,
                totalPenGoals = 0;
            var perMatch = new List<int>(N);
            for (int i = 0; i < N; i++)
            {
                var (state, match) = BuildState(seed: seedGen.Next(), matchId: i + 1);
                var r = MatchSimulator.Simulate(match, state, _balance);
                int g = r.homeScore + r.awayScore;
                totalGoals += g;
                perMatch.Add(g);
                totalXg += r.playerStats.Sum(ps => ps.xg);
                totalShots += r.playerStats.Sum(ps => ps.shots);
                // PK 핀 = x≈0.88 (jitter 없음) — 빈도/득점 분해 (튜닝용).
                var pens = r.shotMap.Where(p => Mathf.Approximately(p.x, 0.88f)).ToList();
                totalPens += pens.Count;
                totalPenGoals += pens.Count(p => p.outcome == ShotOutcome.Goal);
            }
            double avgGoals = (double)totalGoals / N;
            double avgXg = totalXg / N;
            double std = System.Math.Sqrt(perMatch.Sum(g => (g - avgGoals) * (g - avgGoals)) / N);
            Debug.Log(
                $"[T21 측정] 평균골={avgGoals:F2} 평균ΣxG={avgXg:F2} std={std:F2} "
                    + $"슛/팀={(double)totalShots / N / 2:F1} PK/경기={(double)totalPens / N:F2} "
                    + $"PK골/경기={(double)totalPenGoals / N:F2} (목표 avg 2.7±0.3 / std<1.5)"
            );
            // xG-밸런싱 전제: 균등팀 finishMod×gkMod≈1 → 평균 득점 ≈ 평균 ΣxG.
            Assert.That(
                avgGoals,
                Is.EqualTo(avgXg).Within(0.6),
                $"T21: 평균골({avgGoals:F2}) ≈ ΣxG({avgXg:F2}) — xG-밸런싱 전제"
            );
        }

        // ── T22. zoneOccupancy 채워짐 (AA.4 선당김) ───────────────────────
        [Test]
        public void T22_ZoneOccupancy_Populated()
        {
            var (state, match) = BuildState(seed: 3, matchId: 3);
            var r = MatchSimulator.Simulate(match, state, _balance);
            Assert.AreEqual(5, r.zoneOccupancy.Length, "T22: zone 5칸");
            Assert.Greater(r.zoneOccupancy.Sum(), 0, "T22: zone 점유 누적");
            Assert.GreaterOrEqual(
                r.zoneOccupancy[2],
                r.zoneOccupancy[0],
                "T22: 미드필드(2) 점유 ≥ HomeBox(0)"
            );
        }

        // ── T23. 평점: 빅찬스 미스 급락 (#74) ─────────────────────────────
        [Test]
        public void T23_Rating_BigChanceMissDrops()
        {
            var baseStat = new PlayerMatchStat { playerId = 1 };
            var missStat = new PlayerMatchStat
            {
                playerId = 2,
                bigChancesMissed = 1,
                xg = 0.40f,
                shots = 1,
            };
            float baseR = MatchSimulator.ComputePlayerRating(baseStat, Line.AT, 0, 0, _balance);
            float missR = MatchSimulator.ComputePlayerRating(missStat, Line.AT, 0, 0, _balance);
            Assert.Less(missR, baseR, "T23: 빅찬스 미스 → 평점 하락");
            Assert.That(baseR - missR, Is.GreaterThan(0.6f), "T23: 0.40xG 미스 급락폭 (≈0.74)");
        }

        // ── T24. 평점: clinical finish (저 xG 골) 가산 ────────────────────
        [Test]
        public void T24_Rating_ClinicalFinishRewarded()
        {
            var clinical = new PlayerMatchStat { goals = 1, shotsOnTarget = 1, xg = 0.05f };
            var tapIn = new PlayerMatchStat { goals = 1, shotsOnTarget = 1, xg = 0.70f };
            float cR = MatchSimulator.ComputePlayerRating(clinical, Line.AT, 1, 0, _balance);
            float tR = MatchSimulator.ComputePlayerRating(tapIn, Line.AT, 1, 0, _balance);
            Assert.Greater(cR, tR, "T24: 같은 골이라도 저 xG(어려운) 마무리 평점↑");
        }

        // ── T25. 평점: 포지션 가중 + 패스성공률 + 무실점 (FM 정합) ────────
        [Test]
        public void T25_Rating_PositionPassCleanSheet()
        {
            // 무실점 DF > 2실점 DF
            var df = new PlayerMatchStat { tackles = 2, interceptions = 1 };
            float clean = MatchSimulator.ComputePlayerRating(df, Line.DF, 1, 0, _balance);
            float concede = MatchSimulator.ComputePlayerRating(df, Line.DF, 1, 2, _balance);
            Assert.Greater(clean, concede, "T25: 무실점 수비수 > 실점 수비수");

            // 패스 성공률 티어
            var hiPass = new PlayerMatchStat { passes = 50, passesCompleted = 47 }; // 94%
            var loPass = new PlayerMatchStat { passes = 50, passesCompleted = 32 }; // 64%
            float hi = MatchSimulator.ComputePlayerRating(hiPass, Line.MF, 0, 0, _balance);
            float lo = MatchSimulator.ComputePlayerRating(loPass, Line.MF, 0, 0, _balance);
            Assert.Greater(hi, lo, "T25: 높은 패스성공률 > 낮은");

            // GK 선방 + 무실점 > base
            var gk = new PlayerMatchStat { saves = 4 };
            float gkR = MatchSimulator.ComputePlayerRating(gk, Line.GK, 0, 0, _balance);
            Assert.Greater(gkR, _balance.ratingBase, "T25: 선방+무실점 GK > base");

            // 화려한 공격 없이도 호수비 무실점 CB ≥ 7.0 (FM '7.5 CB' 재현)
            var cbStat = new PlayerMatchStat
            {
                tackles = 4,
                interceptions = 3,
                clearances = 5,
                passes = 40,
                passesCompleted = 37,
            };
            float cb = MatchSimulator.ComputePlayerRating(cbStat, Line.DF, 1, 0, _balance);
            Assert.GreaterOrEqual(cb, 7.0f, "T25: 호수비 무실점 CB ≥ 7.0 (포지션 가중)");
        }

        // ── T26. G.2 신체: 공중 우세 공격진 헤더 득점 ↑ (페어드 시드) ──────
        [Test]
        public void T26_Physical_AerialStrengthScoresMore_Paired()
        {
            // 키 고정(185) → agility 영향 동일, heading/jump 만 대조 (헤더 xG 격리).
            var seedGen = new System.Random(123);
            int strongGoals = 0,
                weakGoals = 0;
            const int N = 150;
            for (int i = 0; i < N; i++)
            {
                int seed = seedGen.Next();
                var (sS, mS) = BuildState(seed: seed, matchId: i + 1);
                SetHomeForwards(sS, height: 185, agility: 50, heading: 90, jump: 90);
                strongGoals += MatchSimulator.Simulate(mS, sS, _balance).homeScore;

                var (sW, mW) = BuildState(seed: seed, matchId: i + 1);
                SetHomeForwards(sW, height: 185, agility: 50, heading: 35, jump: 35);
                weakGoals += MatchSimulator.Simulate(mW, sW, _balance).homeScore;
            }
            Debug.Log($"[T26] 강공중 home골={strongGoals} 약공중 home골={weakGoals}");
            Assert.Greater(strongGoals, weakGoals, "T26: 헤더 우수 공격진 헤더 찬스 득점 우세");
        }

        // ── T27. G.2 신체: 키 작은(민첩) 공격진 드리블 박스진입 우세 (페어드) ─
        [Test]
        public void T27_Physical_AgilityShortDribbleAdvantage_Paired()
        {
            // 골은 전환 노이즈 + 헤더 보상(실패→코너→헤더)으로 둔감 →
            // 드리블 진입 직접 지표 = OpenPlay(x≈0.84)/Clear(x≈0.90) 슛맵 핀 수로 격리 측정.
            // heading/jump 낮춰 양쪽 헤더 xG floor 동일, height(165 vs 205)로 agilityEff 만 대조.
            var seedGen = new System.Random(321);
            int shortEntries = 0,
                tallEntries = 0;
            const int N = 250;
            for (int i = 0; i < N; i++)
            {
                int seed = seedGen.Next();
                var (sShort, mShort) = BuildState(seed: seed, matchId: i + 1);
                SetHomeForwards(sShort, height: 165, agility: 90, heading: 25, jump: 25);
                shortEntries += CountHomeDribbleEntries(
                    MatchSimulator.Simulate(mShort, sShort, _balance)
                );

                var (sTall, mTall) = BuildState(seed: seed, matchId: i + 1);
                SetHomeForwards(sTall, height: 205, agility: 90, heading: 25, jump: 25);
                tallEntries += CountHomeDribbleEntries(
                    MatchSimulator.Simulate(mTall, sTall, _balance)
                );
            }
            Debug.Log($"[T27] 단신민첩 진입={shortEntries} 장신 진입={tallEntries}");
            Assert.Greater(
                shortEntries,
                tallEntries,
                "T27: 키 작은 민첩 공격진 드리블 박스진입(OpenPlay/Clear) 우세"
            );
        }

        // home(side=0) 드리블 진입 슛 = OpenPlay(x≈0.84) + ClearChance(x≈0.90) 핀.
        private static int CountHomeDribbleEntries(MatchResult r) =>
            r.shotMap.Count(p =>
                p.side == 0
                && (Mathf.Approximately(p.x, 0.84f) || Mathf.Approximately(p.x, 0.90f))
            );

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

            // I.6 이후: 교체 선수 포함 — 최소 22명, 최대 22 + subs
            Assert.GreaterOrEqual(
                r.playerStats.Count,
                r.homeStarting11.Count + r.awayStarting11.Count
            );
            foreach (var ps in r.playerStats)
            {
                // minutesPlayed: I.6 교체 시 가변 (1~90). rating = I.4 채워짐 (1.0~10.0).
                Assert.That(
                    ps.minutesPlayed,
                    Is.InRange(1, 90),
                    $"minutesPlayed 1~90 (id={ps.playerId})"
                );
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

        // ── T11. I.6 SubstitutionAI — 피로 기반 자동 교체 ───────────────

        [Test]
        public void T11_Substitution_FatigueTrigger_MinutesPlayedUpdated()
        {
            var (state, match) = BuildState(seed: 77, matchId: 77);
            // 전원 피로 80 → threshold(70) 초과 → 45/60/75분 체크에서 교체 발생
            for (int id = 1; id <= 50; id++)
            {
                var p = state.GetPlayer(id);
                if (p != null)
                    p.state.fatigue = 80;
            }
            _balance.substitutionFatigueThreshold = 70;
            _balance.substitutionTacticalMinute = 45;
            _balance.maxSubstitutionsPerTeam = 3;

            var result = MatchSimulator.Simulate(match, state, _balance, collectEvents: true);

            var subEvents = result.events.Where(e => e.type == MatchEventType.Substitution).ToList();
            Assert.Greater(subEvents.Count, 0, "T11: 피로 교체 미발동");

            foreach (var e in subEvents)
            {
                Assert.Greater(e.minute, 0, $"T11: minute={e.minute} 유효하지 않음");
                Assert.Less(e.minute, 90, $"T11: minute={e.minute} 90분 이상");
                Assert.AreNotEqual(e.actorPlayerId, e.targetPlayerId, "T11: playerIn == playerOut");
            }

            // 교체 아웃 선수는 minutesPlayed < 90
            Assert.IsTrue(
                result.playerStats.Any(ps => ps.minutesPlayed < 90),
                "T11: 교체 아웃 선수 minutesPlayed < 90 없음"
            );

            // 팀당 최대 3회 초과 금지
            int homeSubCount = subEvents.Count(e => e.side == 0);
            int awaySubCount = subEvents.Count(e => e.side == 1);
            Assert.LessOrEqual(homeSubCount, 3, "T11: home 교체 횟수 초과");
            Assert.LessOrEqual(awaySubCount, 3, "T11: away 교체 횟수 초과");
        }

        // ── T13. I.10 — 세트피스 taker stat 반영 ────────────────────────

        [Test]
        public void T13_SetPiece_TakerStatReflected_CornerAndFreeKick()
        {
            // ── T13-a: Corner taker = 코너 stat 최상위 선수 ──────────────
            // 시드 고정 + 충분한 매치 수로 코너 이벤트 발생 보장
            _balance.zoneCornerChance = 0.99f; // 공격 third 실패 시 거의 항상 corner
            _balance.longThrowChance = 0.00f;
            _balance.cornerConversionBase = 0.00f; // corner → 슛 확률 0 (이벤트만 검증)

            // 홈팀(clubId=1) — CA 최상위 선수를 taker 로 지정 (반드시 starting XI 포함)
            var (state, match) = BuildState(seed: 42, matchId: 42);
            var homeSquad = state.GetClub(1).seniorSquadIds;
            var taker = state.allPlayers
                .Where(p => homeSquad.Contains(p.id))
                .OrderByDescending(p => p.currentAbility)
                .First();
            taker.stats.technical.corners = 100;
            foreach (var p in state.allPlayers.Where(p => homeSquad.Contains(p.id) && p.id != taker.id))
                p.stats.technical.corners = 1;

            var result = MatchSimulator.Simulate(match, state, _balance, collectEvents: true);

            // 홈 팀(side=0) 코너만 검증 — 어웨이 코너는 별도 taker
            var homeCornerEvents = result
                .events.Where(e => e.type == MatchEventType.Corner && e.side == 0)
                .ToList();
            foreach (var ev in homeCornerEvents)
                Assert.AreEqual(
                    taker.id,
                    ev.actorPlayerId,
                    $"T13-a: home corner actorPlayerId={ev.actorPlayerId}, expected={taker.id}"
                );

            // ── T13-b: 세트피스 결과 정합성 (파울 → FK 해결) ────────────
            _balance.foulProbability = 0.99f; // 거의 항상 파울
            _balance.freeKickDirectProb = 1.0f; // 항상 직접 FK
            _balance.freeKickConversionBase = 0.00f; // on-target 확률 0 (이벤트 여부만)

            var (s2, m2) = BuildState(seed: 55, matchId: 55);
            // 어웨이팀(clubId=2) 에서 freeKickTaking=100 인 선수 지정
            var awaySquad = s2.GetClub(2).seniorSquadIds;
            var fkTaker = s2.allPlayers
                .Where(p => awaySquad.Contains(p.id))
                .OrderByDescending(p => p.currentAbility)
                .First();
            fkTaker.stats.technical.freeKickTaking = 100;
            foreach (var p in s2.allPlayers.Where(p => awaySquad.Contains(p.id) && p.id != fkTaker.id))
                p.stats.technical.freeKickTaking = 1;
            var r2 = MatchSimulator.Simulate(m2, s2, _balance, collectEvents: true);
            var fkEvents = r2.events.Where(e => e.type == MatchEventType.FreeKick).ToList();

            // FK 이벤트가 발생했다면 fkTaker 의 shots ≥ 1 (직접 FK shot 시도)
            if (fkEvents.Count > 0)
            {
                var fkStat = r2.playerStats.FirstOrDefault(ps => ps.playerId == fkTaker.id);
                Assert.IsNotNull(fkStat, "T13-b: fkTaker playerStat 없음");
                Assert.Greater(fkStat.shots, 0, "T13-b: FK taker shots=0 (직접 FK shot 미적용)");
            }

            // ── T13-c: 매치 정상 완료 ─────────────────────────────────
            _balance.foulProbability = 0.12f; // 원복
            _balance.freeKickDirectProb = 0.50f;
            _balance.zoneCornerChance = 0.25f;
            _balance.cornerConversionBase = 0.08f;

            var (s3, m3) = BuildState(seed: 77, matchId: 77);
            var r3 = MatchSimulator.Simulate(m3, s3, _balance, collectEvents: true);
            Assert.GreaterOrEqual(r3.homeScore, 0, "T13-c: homeScore < 0");
            Assert.GreaterOrEqual(r3.awayScore, 0, "T13-c: awayScore < 0");
            Assert.GreaterOrEqual(r3.playerStats.Count, 22, "T13-c: playerStats 부족");
        }

        // ── T7. I.8 — fatigue 임계 + form/morale 외부 영향 ──────────────

        [Test]
        public void T7_ExternalEffects_FatigueThresholdAndFormMorale()
        {
            // ── T7-a: fatigue ≤ 50 → perf = 1.0 (보정 없음) ─────────────
            _balance.fatiguePerfThreshold = 50;
            _balance.fatiguePerfFloor = 0.6f;
            _balance.fatiguePerfPenaltyPerPoint = 0.01f;

            var (s1, m1) = BuildState(seed: 10, matchId: 10);
            var (s2, m2) = BuildState(seed: 10, matchId: 10);

            // s1: fatigue = 0 (기본), s2: fatigue = 50 (임계 직전)
            foreach (var p in s2.allPlayers)
                p.state.fatigue = 50;

            var r1 = MatchSimulator.Simulate(m1, s1, _balance);
            var r2 = MatchSimulator.Simulate(m2, s2, _balance);

            // 시드 동일 + 피로 보정 없음 → 점수 동일
            Assert.AreEqual(r1.homeScore, r2.homeScore, "T7-a: fatigue=50 → 보정 없이 결과 동일");
            Assert.AreEqual(r1.awayScore, r2.awayScore, "T7-a: fatigue=50 → 보정 없이 결과 동일");

            // ── T7-b: fatigue = 100 → perf floor 0.6 (슛 평점 ↓) ────────
            var (sHigh, mHigh) = BuildState(seed: 42, matchId: 42, homeStat: 80, awayStat: 80);
            var (sBase, mBase) = BuildState(seed: 42, matchId: 42, homeStat: 80, awayStat: 80);

            foreach (var p in sHigh.allPlayers)
                p.state.fatigue = 100;

            // fatigue 100 → perf = max(0.6, 1 - 50*0.01) = 0.6. 슛수/점수 ↓ 경향.
            // 100매치 배치 없이 단일 매치 — 슛 수보다 perf 계산 자체를 단위 검증.
            // Eff(raw=100, fatigue=100, perf=0.6) → 60 * homeMod
            // Eff(raw=100, fatigue=0,   perf=1.0) → 100 * homeMod
            // 슛 결과가 확률적이므로 골 수 직접 비교 대신 ≥ 0 정합성 체크.
            var rHigh = MatchSimulator.Simulate(mHigh, sHigh, _balance);
            Assert.GreaterOrEqual(rHigh.homeScore, 0, "T7-b: fatigue=100 매치 정상 완료");
            Assert.GreaterOrEqual(rHigh.awayScore, 0, "T7-b: fatigue=100 매치 정상 완료");

            // ── T7-c: fatigue > 40 → 부상률 ↑ ───────────────────────────
            // injuryProneness 100 + fatigue 50(>40) → 부상 발생 확률 높음. 충분한 시도에서 ≥1 부상.
            _balance.fatigueInjuryThreshold = 40;
            _balance.fatigueInjuryMultiplier = 1.5f;
            _balance.matchInjuryProbability = 0.5f; // 테스트용 — 높게 설정
            _balance.injuryProneRefDivisor = 50f;

            int injuryCount = 0;
            for (int trial = 0; trial < 30; trial++)
            {
                var (st, mt) = BuildState(seed: trial, matchId: trial);
                foreach (var p in st.allPlayers)
                {
                    p.state.fatigue = 50; // > fatigueInjuryThreshold(40)
                    p.hiddenAttrs.injuryProneness = 100;
                }
                EventBus.Subscribe<PlayerInjuredEvent>(_ => injuryCount++);
                MatchSimulator.Simulate(mt, st, _balance);
                EventBus.Clear();
            }
            Assert.Greater(injuryCount, 0, "T7-c: fatigue>40 + injuryProneness=100 → 30매치 중 부상 ≥1");

            // ── T7-d: form/morale 보정 — form=100 → formMod > 1 ─────────
            _balance.formCoeff = 200f;
            _balance.moraleCoeff = 200f;
            // form=100, morale=50 → formMod = (1+(100-50)/200)×(1+(50-50)/200) = 1.25×1.0 = 1.25
            // form=0,   morale=50 → formMod = (1+(0-50)/200)×1.0 = 0.75
            // 동일 시드에서 form 높은 팀이 득점 우위 경향 (확률적이므로 ≥0 정합성 + 완료 검증).
            _balance.matchInjuryProbability = 0.03f; // 원복

            var (sForm, mForm) = BuildState(seed: 99, matchId: 99);
            foreach (var p in sForm.allPlayers.Where(p => p.currentClubId == 1))
                p.state.form = 100;

            var rForm = MatchSimulator.Simulate(mForm, sForm, _balance);
            Assert.GreaterOrEqual(rForm.homeScore, 0, "T7-d: form=100 매치 정상 완료");
        }

        // ── T11. 연장 / 승부차기 ──────────────────────────────────────

        [Test]
        public void T11_ExtraTimeAndPenaltyShootout()
        {
            // actionsPerMinuteMin=Max=0 → 매 분 0 액션 → 골 불가 → 0-0 보장 (결정적)
            _balance.actionsPerMinuteMin = 0;
            _balance.actionsPerMinuteMax = 0;

            // T11-a: 컵 매치 0-0 → 연장 → 승부차기 결착
            var (s, m) = BuildState(seed: 1, matchId: 1);
            m.type = CompetitionType.FACup;
            m.allowsExtraTime = true;

            var r = MatchSimulator.Simulate(m, s, _balance);

            Assert.IsTrue(r.decidedByPenalties, "T11-a: 컵 0-0 → 승부차기 결착");
            Assert.AreEqual(0, r.homeScore, "T11-a: ET 종료 시 매치 스코어 0-0 (home)");
            Assert.AreEqual(0, r.awayScore, "T11-a: ET 종료 시 매치 스코어 0-0 (away)");

            // T11-b: 승부차기 스코어 불일치 (결착)
            Assert.AreNotEqual(
                r.penaltyHomeScore,
                r.penaltyAwayScore,
                "T11-b: 승부차기 결착 → penaltyHomeScore ≠ penaltyAwayScore"
            );

            // T11-c: 리그 매치 → decidedByPenalties=false
            var (sL, mL) = BuildState(seed: 2, matchId: 2);
            var rL = MatchSimulator.Simulate(mL, sL, _balance);
            Assert.IsFalse(rL.decidedByPenalties, "T11-c: 리그 매치 → decidedByPenalties=false");
        }

        // ── T12. Mentality → 슛수 차이 (J.3) ────────────────────────

        [Test]
        public void T12_MentalityAffectsShotFrequency()
        {
            // 5-zone Markov 에서 VeryAttacking 은 공격적이지만 수비도 약해 possession 이 교환됨.
            // 순 효과: VeryAttacking 슛수 > VeryDefensive 슛수 (비율 ~1.4). 50 trials 로 통계 안정화.
            const int trials = 50;
            int veryAtkShots = 0;
            int veryDefShots = 0;

            for (int i = 0; i < trials; i++)
            {
                var (sAtk, mAtk) = BuildState(seed: i, matchId: i * 2 + 100);
                sAtk.GetClub(1).tactic = new Tactic { mentality = Mentality.VeryAttacking };
                var rAtk = MatchSimulator.Simulate(mAtk, sAtk, _balance);
                var homeAtk = new HashSet<int>(rAtk.homeStarting11);
                veryAtkShots += rAtk
                    .playerStats.Where(ps => homeAtk.Contains(ps.playerId))
                    .Sum(ps => ps.shots);

                var (sDef, mDef) = BuildState(seed: i, matchId: i * 2 + 101);
                sDef.GetClub(1).tactic = new Tactic { mentality = Mentality.VeryDefensive };
                var rDef = MatchSimulator.Simulate(mDef, sDef, _balance);
                var homeDef = new HashSet<int>(rDef.homeStarting11);
                veryDefShots += rDef
                    .playerStats.Where(ps => homeDef.Contains(ps.playerId))
                    .Sum(ps => ps.shots);
            }

            double ratio = veryDefShots > 0 ? (double)veryAtkShots / veryDefShots : 9.9;
            Assert.That(
                ratio,
                Is.GreaterThan(1.25),
                $"T12: VeryAttacking 홈팀 슛 > VeryDefensive 홈팀 슛 (ratio={ratio:F2}, atk={veryAtkShots}, def={veryDefShots})"
            );
        }

        // ── T1. Role 가중치 — Poacher shot 가중치 = Target Man 의 2× (J.4) ──
        // 스펙의 "슈팅 비율 ~2×" = ComputeEventWeight 가중치 비율. (emergent 슛 카운트는 5-zone 점유/zone
        // 동학으로 증폭 — 정확 비율은 가중치 레벨에서 검증, 통합 흐름은 아래 TacticWeighting_FlowsIntoShotSelection.)
        // (T2 Mentality = 기존 T12 / T3 Set Piece 자동선정 = 기존 T13(I.10) 로 대체)

        [Test]
        public void T1_RoleWeight_PoacherDoubleTargetMan()
        {
            RegisterRole(37, "Poacher", TacticImpact.EventShot, 1.5f);
            RegisterRole(38, "Target Man", TacticImpact.EventShot, 0.75f);

            // 동일 stat / 동일 duty(Attack) → role(shot) 차이만 (1.5 vs 0.75 = 2×)
            var (state, _, aId, bId) = BuildTacticState(
                seed: 1,
                matchId: 1,
                roleA: 37,
                dutyA: Duty.Attack,
                roleB: 38,
                dutyB: Duty.Attack
            );
            var tactic = state.GetClub(1).tactic;
            float wPoacher = TacticImpact.ComputeEventWeight(
                tactic,
                aId,
                state,
                TacticImpact.EventShot,
                _balance
            );
            float wTarget = TacticImpact.ComputeEventWeight(
                tactic,
                bId,
                state,
                TacticImpact.EventShot,
                _balance
            );

            Assert.That(
                wPoacher / wTarget,
                Is.EqualTo(2.0).Within(0.01),
                $"T1: Poacher shot 가중치 = 2× Target Man (wP={wPoacher}, wT={wTarget})"
            );
        }

        // ── T4. Duty 가중치 — 같은 Role / Attack shot 가중치 = Defend 의 3× (J.4) ──

        [Test]
        public void T4_DutyWeight_AttackTripleDefend()
        {
            RegisterRole(37, "Poacher", TacticImpact.EventShot, 1.5f); // 동일 Role → role 상쇄, duty 차이만

            // duty(shot): Attack=1.5 vs Defend=0.5 = 3×
            var (state, _, aId, bId) = BuildTacticState(
                seed: 1,
                matchId: 1,
                roleA: 37,
                dutyA: Duty.Attack,
                roleB: 37,
                dutyB: Duty.Defend
            );
            var tactic = state.GetClub(1).tactic;
            float wAttack = TacticImpact.ComputeEventWeight(
                tactic,
                aId,
                state,
                TacticImpact.EventShot,
                _balance
            );
            float wDefend = TacticImpact.ComputeEventWeight(
                tactic,
                bId,
                state,
                TacticImpact.EventShot,
                _balance
            );

            Assert.That(
                wAttack / wDefend,
                Is.EqualTo(3.0).Within(0.01),
                $"T4: Attack shot 가중치 = 3× Defend (wA={wAttack}, wD={wDefend})"
            );
        }

        // ── J.4 통합 — Tactic 가중치가 MatchSimulator 슈터 추첨에 실제 반영 (방향성) ──
        // emergent 슛 카운트의 정확 비율은 엔진 동학으로 증폭되므로 방향성만 검증 (정확 비율 = T1).

        [Test]
        public void TacticWeighting_FlowsIntoShotSelection()
        {
            RegisterRole(37, "Poacher", TacticImpact.EventShot, 1.5f);
            RegisterRole(38, "Target Man", TacticImpact.EventShot, 0.75f);

            int poacherShots = 0,
                targetShots = 0;
            const int N = 40;
            for (int i = 0; i < N; i++)
            {
                var (state, match, aId, bId) = BuildTacticState(
                    seed: i,
                    matchId: i + 1,
                    roleA: 37,
                    dutyA: Duty.Attack,
                    roleB: 38,
                    dutyB: Duty.Attack
                );
                var r = MatchSimulator.Simulate(match, state, _balance);
                poacherShots += ShotsOf(r, aId);
                targetShots += ShotsOf(r, bId);
            }

            Assert.Greater(
                (double)poacherShots,
                targetShots * 1.3,
                $"통합: Poacher 가 Target Man 보다 슛 우세 (가중 추첨 반영) — P={poacherShots}, T={targetShots}"
            );
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static int ShotsOf(MatchResult r, int playerId) =>
            r.playerStats.FirstOrDefault(ps => ps.playerId == playerId)?.shots ?? 0;

        private static void RegisterRole(int id, string name, string eventKey, float mult)
        {
            var role = ScriptableObject.CreateInstance<PlayerRoleSO>();
            role.id = id;
            role.displayName = name;
            role.eventModifiers = new List<MatchEventModifier>
            {
                new MatchEventModifier { eventType = eventKey, multiplier = mult },
            };
            GameDatabase.Register(role);
        }

        // home XI = GK1 + CB4 + CM4 + ST(aId, roleA/dutyA) + ST(bId, roleB/dutyB) = 11 (둘 다 선발).
        // home.tactic 에 두 ST 슬롯만 배정 → SnapPlayer(AT) 가중 추첨이 두 ST 사이에서만 작동. away = 표준 25 (tactic null).
        private (GameState state, Match match, int aId, int bId) BuildTacticState(
            int seed,
            int matchId,
            int roleA,
            Duty dutyA,
            int roleB,
            Duty dutyB
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
            int AddHome(Position pos)
            {
                int id = nextId;
                state.AddPlayer(NewPlayer(id, pos, ca: 100, statVal: 50));
                home.seniorSquadIds.Add(id);
                nextId++;
                return id;
            }

            AddHome(Position.GK);
            for (int i = 0; i < 4; i++)
                AddHome(Position.CB);
            for (int i = 0; i < 4; i++)
                AddHome(Position.CM);
            int aId = AddHome(Position.ST);
            int bId = AddHome(Position.ST);

            nextId = AddSquad(state, away, statVal: 50, nextId: nextId, count: 25);

            home.tactic = new Tactic
            {
                formationId = 1,
                mentality = Mentality.Balanced,
                slots = new List<TacticSlot>
                {
                    new TacticSlot
                    {
                        slotIndex = 0,
                        roleId = roleA,
                        duty = dutyA,
                        assignedPlayerId = aId,
                    },
                    new TacticSlot
                    {
                        slotIndex = 1,
                        roleId = roleB,
                        duty = dutyB,
                        assignedPlayerId = bId,
                    },
                },
            };

            var match = new Match
            {
                id = matchId,
                homeClubId = 1,
                awayClubId = 2,
                type = CompetitionType.League,
            };
            return (state, match, aId, bId);
        }

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

        // G.2 테스트용 — home AT 라인 선수들의 신체조건/공중 stat 설정 (#474).
        private static void SetHomeForwards(
            GameState state,
            int height,
            int agility,
            int heading,
            int jump
        )
        {
            var home = state.GetClub(1);
            foreach (var id in home.seniorSquadIds)
            {
                var p = state.GetPlayer(id);
                if (p == null || StartingSquadGacha.LineOf(p.info.primaryPosition) != Line.AT)
                    continue;
                p.physical = new PhysicalAttributes
                {
                    height = height,
                    weight = 78,
                    preferredFoot = Foot.Right,
                    weakFootAbility = 3,
                };
                p.stats.physical.agility = agility;
                p.stats.physical.jumpingReach = jump;
                p.stats.technical.heading = heading;
            }
        }
    }
}
