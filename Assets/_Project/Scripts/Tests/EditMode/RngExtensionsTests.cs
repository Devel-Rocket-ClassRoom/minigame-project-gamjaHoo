// RngExtensionsTests.cs
// DoD 검증: 이슈 #70 — NextNormal 분포 / 결정성, WeightedSample 분포 / 폴백 / 예외.
//          이슈 #111 — NextPoisson 평균 / 분산 / 결정성 / λ=0 / λ<0 예외.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using FMLite.Utils;

namespace FMLite.Tests
{
    public class RngExtensionsTests
    {
        [Test]
        public void NextNormal_LargeSample_MatchesMuAndSigma()
        {
            var rng = new Random(42);
            const int n = 10_000;
            const double mu = 0.0;
            const double sigma = 1.0;

            double sum = 0;
            var samples = new double[n];
            for (int i = 0; i < n; i++)
            {
                samples[i] = rng.NextNormal(mu, sigma);
                sum += samples[i];
            }
            double mean = sum / n;

            double varSum = 0;
            for (int i = 0; i < n; i++)
            {
                double d = samples[i] - mean;
                varSum += d * d;
            }
            double stddev = Math.Sqrt(varSum / n);

            Assert.That(mean, Is.EqualTo(mu).Within(0.05),
                "10000 샘플 평균이 μ=0 ±0.05 범위 안에 있어야 함");
            Assert.That(stddev, Is.EqualTo(sigma).Within(0.05),
                "10000 샘플 표준편차가 σ=1 ±0.05 범위 안에 있어야 함");
        }

        [Test]
        public void NextNormal_SameSeed_DeterministicSequence()
        {
            var rng1 = new Random(42);
            var rng2 = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(rng1.NextNormal(0, 1), rng2.NextNormal(0, 1),
                    "같은 시드 → 같은 시퀀스");
            }
        }

        [Test]
        public void WeightedSample_DistributionMatchesWeights()
        {
            var rng = new Random(42);
            var items = new[] { "A", "B", "C" };
            // weight 1 : 2 : 3 → 기대 분포 약 16.7% : 33.3% : 50%
            var weights = new Dictionary<string, double> { ["A"] = 1, ["B"] = 2, ["C"] = 3 };

            const int n = 6_000;
            var counts = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["C"] = 0 };
            for (int i = 0; i < n; i++)
            {
                var pick = rng.WeightedSample(items, x => weights[x]);
                counts[pick]++;
            }

            // 6000회 → A 약 1000, B 약 2000, C 약 3000. ±5% 허용.
            Assert.That(counts["A"], Is.EqualTo(1000).Within(300), "A 비율 ~16.7%");
            Assert.That(counts["B"], Is.EqualTo(2000).Within(300), "B 비율 ~33.3%");
            Assert.That(counts["C"], Is.EqualTo(3000).Within(300), "C 비율 ~50%");
        }

        [Test]
        public void WeightedSample_AllZeroWeights_FallsBackToUniform()
        {
            var rng = new Random(42);
            var items = new[] { "X", "Y", "Z" };

            const int n = 3_000;
            var counts = new Dictionary<string, int> { ["X"] = 0, ["Y"] = 0, ["Z"] = 0 };
            for (int i = 0; i < n; i++)
            {
                var pick = rng.WeightedSample(items, _ => 0.0);
                counts[pick]++;
            }

            // 균등 분포 → 각 약 1000회 ±200 (15% 허용)
            Assert.That(counts["X"], Is.EqualTo(1000).Within(200));
            Assert.That(counts["Y"], Is.EqualTo(1000).Within(200));
            Assert.That(counts["Z"], Is.EqualTo(1000).Within(200));
        }

        [Test]
        public void WeightedSample_EmptyList_Throws()
        {
            var rng = new Random(42);
            var items = Array.Empty<string>();

            Assert.Throws<ArgumentException>(() =>
                rng.WeightedSample(items, _ => 1.0));
        }

        // ─── NextPoisson (issue #111, algorithms.md #2 4단계) ───

        [Test]
        public void NextPoisson_LargeSample_MatchesLambdaMeanAndVariance()
        {
            // 포아송 분포 특성: 평균 = 분산 = λ.
            var rng = new Random(42);
            const int n = 10_000;
            const double lambda = 2.5;

            long sum = 0;
            var samples = new int[n];
            for (int i = 0; i < n; i++)
            {
                samples[i] = rng.NextPoisson(lambda);
                sum += samples[i];
            }
            double mean = (double)sum / n;

            double varSum = 0;
            for (int i = 0; i < n; i++)
            {
                double d = samples[i] - mean;
                varSum += d * d;
            }
            double variance = varSum / n;

            Assert.That(mean, Is.EqualTo(lambda).Within(0.05),
                "10000 샘플 평균이 λ=2.5 ±0.05 범위 안에 있어야 함");
            Assert.That(variance, Is.EqualTo(lambda).Within(0.15),
                "10000 샘플 분산이 λ=2.5 ±0.15 범위 안에 있어야 함 (포아송 평균 = 분산)");
        }

        [Test]
        public void NextPoisson_SameSeed_DeterministicSequence()
        {
            var rng1 = new Random(42);
            var rng2 = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(rng1.NextPoisson(2.5), rng2.NextPoisson(2.5),
                    "같은 시드 → 같은 시퀀스");
            }
        }

        [Test]
        public void NextPoisson_LambdaZero_AlwaysReturnsZero()
        {
            var rng = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(0, rng.NextPoisson(0.0),
                    "λ=0 이면 항상 0 반환 (degenerate 분포)");
            }
        }

        [Test]
        public void NextPoisson_NegativeLambda_Throws()
        {
            var rng = new Random(42);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                rng.NextPoisson(-0.1));
        }

        [Test]
        public void NextPoisson_SmallLambda_ProducesExpectedDistribution()
        {
            // λ=0.8 → P(0)≈45% / P(1)≈36% / P(2)≈14% / P(3)≈4% / P(≥4)≈1%
            // (Match Simulation 약팀 λ 시나리오 검증)
            var rng = new Random(42);
            const int n = 10_000;
            const double lambda = 0.8;

            var counts = new int[10];
            for (int i = 0; i < n; i++)
            {
                int k = rng.NextPoisson(lambda);
                if (k < counts.Length) counts[k]++;
                else counts[counts.Length - 1]++;
            }

            // 약팀이 3-4 골 넣는 이변도 가능해야 함 (>0건).
            int rareCases = counts[3] + counts[4] + counts[5];
            Assert.Greater(rareCases, 0,
                "약팀 λ=0.8 라도 3골 이상 이변 자연스럽게 발생해야 함");

            // 0골이 가장 흔하지만 1골도 비슷한 비율.
            Assert.That(counts[0], Is.EqualTo(4500).Within(500),
                "P(0) ≈ 45% ±5%");
            Assert.That(counts[1], Is.EqualTo(3600).Within(500),
                "P(1) ≈ 36% ±5%");
        }
    }
}
