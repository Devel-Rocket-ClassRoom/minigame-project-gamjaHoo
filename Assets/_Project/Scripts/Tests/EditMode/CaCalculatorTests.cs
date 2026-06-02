// CaCalculatorTests.cs
// Stage D (V1.0, #459) — CA 재계산 (앵커 방식) 검증.
//   T1  라운드트립: Recompute(생성 stats + 앵커) == 생성 CA (밸런스 점프 0)
//   T2  성장 반영: stat 상승 → Recompute 증가
//   T3  GetCaChange: 3개월 변화량 (Avg49 델타) / 데이터 부족 시 0
//   T4  lazy-init: caAnchor 미설정(0) + currentAbility 보존

using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class CaCalculatorTests
    {
        private static Stats Uniform(int v)
        {
            var s = new Stats();
            s.technical.ApplyToAll(_ => v);
            s.mental.ApplyToAll(_ => v);
            s.physical.ApplyToAll(_ => v);
            s.gk.ApplyToAll(_ => v);
            return s;
        }

        // ── T1. 라운드트립 (생성 시점 CA 보존) ────────────────────────
        [Test]
        public void T1_Recompute_RoundTrips_To_GenCA()
        {
            var stats = Uniform(50);
            // 생성 CA 가 평균과 다른 임의값이어도 앵커가 보정.
            foreach (int genCa in new[] { 50, 70, 88, 35 })
            {
                var p = new Player
                {
                    stats = stats,
                    currentAbility = genCa,
                    caAnchor = genCa - CaCalculator.RelevantMean(stats, Position.CB),
                };
                Assert.AreEqual(genCa, CaCalculator.Recompute(p, null), $"genCa={genCa} 라운드트립");
            }
        }

        // ── T2. 성장 반영 ─────────────────────────────────────────────
        [Test]
        public void T2_Recompute_RisesWithGrowth()
        {
            var stats = Uniform(50);
            var p = new Player
            {
                stats = stats,
                currentAbility = 70,
                caAnchor = 70 - CaCalculator.RelevantMean(stats, Position.CB),
            };
            Assert.AreEqual(70, CaCalculator.Recompute(p, null));

            // technical 14개 +10 → 평균 상승 → CA 상승
            p.stats.technical.ApplyToAll(_ => 60);
            Assert.Greater(CaCalculator.Recompute(p, null), 70, "성장 후 CA 상승");
        }

        // ── T3. GetCaChange ───────────────────────────────────────────
        [Test]
        public void T3_GetCaChange_DeltaAndInsufficient()
        {
            var p = new Player { stats = Uniform(54), growthHistory = new List<StatSnapshot>() };
            // 데이터 부족 → 0
            Assert.AreEqual(0, CaCalculator.GetCaChange(p, 3));

            // 3개월 전 스냅샷 = 평균 50, 현재 평균 54 → 변화 +4
            for (int i = 0; i < 3; i++)
                p.growthHistory.Add(new StatSnapshot { stats = Uniform(50) });
            Assert.AreEqual(4, CaCalculator.GetCaChange(p, 3), "3개월 CA 변화 +4");
        }

        // ── T5. RelevantMean 포지션 인지 (b) ──────────────────────────
        [Test]
        public void T5_RelevantMean_PositionAware()
        {
            var s = Uniform(50);
            s.gk.ApplyToAll(_ => 90); // GK 스탯만 상향

            // 필드(CB): GK 스탯 제외 → 50 유지
            Assert.AreEqual(50.0, CaCalculator.RelevantMean(s, Position.CB), 0.01, "필드는 GK스탯 무관");
            // GK: GK 스탯 포함 → 50 초과
            Assert.Greater(CaCalculator.RelevantMean(s, Position.GK), 50.0, "GK는 GK스탯 반영");
        }

        // ── T4. lazy-init (앵커 미설정 보존) ──────────────────────────
        [Test]
        public void T4_Recompute_LazyInitsAnchor()
        {
            var p = new Player
            {
                stats = Uniform(50),
                currentAbility = 65,
                caAnchor = 0.0, // 미설정 (기존 세이브)
            };
            Assert.AreEqual(65, CaCalculator.Recompute(p, null), "lazy-init 으로 65 보존");
        }
    }
}
