// RngExtensions.cs
// System.Random 확장 — 정규분포 추출(Box-Muller) / 가중 추첨 / 포아송 추출(Knuth).
// PlayerGenerator(#28) 의 NextNormal·WeightedSample (algorithms.md #1) +
// MatchSimulator 의 NextPoisson (algorithms.md #2 4단계) 호출 대상.

using System;
using System.Collections.Generic;

namespace FMLite.Utils
{
    public static class RngExtensions
    {
        // Box-Muller transform. 매 호출 한 번의 NextDouble 두 번을 소비해 하나의 정규분포 표본 반환.
        public static double NextNormal(this Random rng, double mu, double sigma)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // u1 == 0 이면 log(0) = -inf. 안전을 위해 0 회피.
            double u1;
            do { u1 = rng.NextDouble(); } while (u1 <= 0.0);
            double u2 = rng.NextDouble();

            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mu + z * sigma;
        }

        // 가중 추첨 — 누적분포 기반. weight 합 0 / 음수 케이스는 균등 분포 폴백.
        public static T WeightedSample<T>(this Random rng, IList<T> items, Func<T, double> weightFn)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weightFn == null) throw new ArgumentNullException(nameof(weightFn));
            if (items.Count == 0) throw new ArgumentException("items must be non-empty", nameof(items));

            double total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                double w = weightFn(items[i]);
                if (w > 0) total += w;
            }

            // 모든 weight 가 0 이거나 음수 → 균등 폴백.
            if (total <= 0) return items[rng.Next(items.Count)];

            double threshold = rng.NextDouble() * total;
            double cumulative = 0;
            for (int i = 0; i < items.Count; i++)
            {
                double w = weightFn(items[i]);
                if (w <= 0) continue;
                cumulative += w;
                if (cumulative >= threshold) return items[i];
            }
            // float 부동소수 오차 보호: 마지막 항목 폴백.
            return items[items.Count - 1];
        }

        // 포아송 분포 추출 — Knuth 알고리즘 (algorithms.md #2 4단계 골 분포 모델).
        // λ < 30 범위에서 효율적. V0.1 매치 평균 λ ≈ 1.5~3.5 라 충분.
        // 큰 λ (>30) 는 exp(-λ) underflow 위험 — V1.0+ 에서 정규분포 근사 확장 검토.
        public static int NextPoisson(this Random rng, double lambda)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (lambda < 0) throw new ArgumentOutOfRangeException(nameof(lambda), "lambda must be >= 0");
            if (lambda == 0) return 0;

            double L = Math.Exp(-lambda);
            int k = 0;
            double p = 1.0;
            do
            {
                k++;
                p *= rng.NextDouble();
            } while (p > L);
            return k - 1;
        }
    }
}
