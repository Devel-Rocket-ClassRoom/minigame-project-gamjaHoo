// YouthPositionWeightTests.cs
// DoD: algorithms.md V0.5 L.7 — 라운드별 포지션 가중치 다양성.

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
    public class YouthPositionWeightTests
    {
        // ── T1. volatility=0 → 균등 가중치 ─────────────────────────────

        [Test]
        public void T1_ZeroVolatility_UniformWeights()
        {
            var rng = new Random(42);
            double[] weights = YouthSystem.SamplePositionWeights(rng, 0f);

            Assert.AreEqual(14, weights.Length, "T1: 14개 포지션");
            double expected = 1.0 / 14;
            foreach (var w in weights)
                Assert.AreEqual(expected, w, 1e-9, "T1: volatility=0 → 균등");
        }

        // ── T2. 가중치 합 = 1 ────────────────────────────────────────────

        [Test]
        public void T2_WeightsSumToOne()
        {
            var rng = new Random(99);
            foreach (float vol in new[] { 0f, 0.5f, 1f })
            {
                double[] weights = YouthSystem.SamplePositionWeights(rng, vol);
                double sum = weights.Sum();
                Assert.AreEqual(1.0, sum, 1e-9, $"T2: volatility={vol} 합=1");
            }
        }

        // ── T3. 다른 seed → 다른 가중치 (volatility>0) ───────────────────

        [Test]
        public void T3_DifferentSeeds_DifferentWeights()
        {
            double[] w1 = YouthSystem.SamplePositionWeights(new Random(1), 0.8f);
            double[] w2 = YouthSystem.SamplePositionWeights(new Random(2), 0.8f);
            bool anyDiff = w1.Zip(w2, (a, b) => Math.Abs(a - b) > 1e-9).Any(d => d);
            Assert.IsTrue(anyDiff, "T3: 다른 seed → 다른 가중치");
        }

        // ── T4. 같은 seed → 같은 가중치 (결정성) ────────────────────────

        [Test]
        public void T4_SameSeed_SameWeights()
        {
            double[] w1 = YouthSystem.SamplePositionWeights(new Random(42), 0.5f);
            double[] w2 = YouthSystem.SamplePositionWeights(new Random(42), 0.5f);
            for (int i = 0; i < 14; i++)
                Assert.AreEqual(w1[i], w2[i], 1e-15, $"T4: 결정성 index={i}");
        }

        // ── T5. 다른 인스펙션에서 포지션 분포가 다양함 (통계적) ──────────

        [Test]
        public void T5_DifferentIntakes_DifferentPositionDistributions()
        {
            var balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            balance.youthPositionWeightVolatility = 1.0f;

            // 인스펙션 10번 실행, 각각의 GK 비율 수집
            var gkRatios = new List<double>();
            for (int seed = 0; seed < 10; seed++)
            {
                var rng = new Random(seed);
                double[] w = YouthSystem.SamplePositionWeights(rng, 1.0f);
                gkRatios.Add(w[0]); // GK = index 0
            }

            // volatility=1.0이면 GK 비율이 인스펙션마다 다름
            double variance = gkRatios.Select(r => r - gkRatios.Average()).Select(d => d * d).Average();
            Assert.Greater(variance, 0, "T5: volatility=1.0 → GK 비율 분산 > 0");
        }
    }
}
