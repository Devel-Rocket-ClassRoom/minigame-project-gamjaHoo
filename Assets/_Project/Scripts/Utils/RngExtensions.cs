// RngExtensions.cs
// System.Random 확장 — 정규분포 추출(Box-Muller) 및 가중 추첨.
// PlayerGenerator(#28) 등 알고리즘이 사용. algorithms.md #1 Logic 의 rng.NextNormal /
// rng.WeightedSample 호출 대상.

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
    }
}
