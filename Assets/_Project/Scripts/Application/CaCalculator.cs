// CaCalculator.cs
// Stage D (V1.0, #459) — CurrentAbility 재계산 (앵커 + 포지션 관련 평균).
//   caAnchor = 생성시 CA − RelevantMean(생성 stats, pos)  (PlayerGenerator)
//   currentAbility = round(RelevantMean(현재 stats, pos) + caAnchor)  → 성장 시 CA 자연 상승, 생성 시점 CA 보존
//   CA 변화량 = RelevantMean(now) − RelevantMean(N개월 전 snapshot)  (앵커 상쇄)
// RelevantMean: 포지션이 쓰는 카테고리만 평균 (필드=기술/정신/신체, GK=골키퍼/정신/신체) → 무관 카테고리 희석 제거.
// algorithms.md V1.0-13.

using System;
using FMLite.Domain;

namespace FMLite.Application
{
    public static class CaCalculator
    {
        /// <summary>포지션 관련 카테고리 stat 평균. 필드=기술+정신+신체(36), GK=골키퍼+정신+신체(35).</summary>
        public static double RelevantMean(Stats s, Position pos)
        {
            if (s == null)
                return 0;
            int sum = 0;
            int n = 0;
            Func<int, int> acc = v =>
            {
                sum += v;
                n++;
                return v;
            };
            s.mental.ApplyToAll(acc);
            s.physical.ApplyToAll(acc);
            if (pos == Position.GK)
                s.gk.ApplyToAll(acc);
            else
                s.technical.ApplyToAll(acc);
            return n > 0 ? (double)sum / n : 0;
        }

        private static Position PosOf(Player p) => p?.info?.primaryPosition ?? Position.CM;

        /// <summary>현재 stats + 앵커로 CA 재산출. 앵커 미설정(0) 시 lazy-init (기존 currentAbility 보존).</summary>
        public static int Recompute(Player p, GameBalanceSO b)
        {
            if (p?.stats == null)
                return p?.currentAbility ?? 0;

            var pos = PosOf(p);
            if (p.caAnchor == 0.0 && p.currentAbility > 0)
                p.caAnchor = p.currentAbility - RelevantMean(p.stats, pos);

            int ca = (int)Math.Round(RelevantMean(p.stats, pos) + p.caAnchor);
            return b != null ? Math.Clamp(ca, b.minCA, b.maxCA) : ca;
        }

        /// <summary>직전 N개월 CA 변화량 (현재 − N개월 전 snapshot, RelevantMean 기준). 데이터 부족 시 0.</summary>
        public static int GetCaChange(Player p, int monthsBack = 3)
        {
            if (p?.growthHistory == null || p.growthHistory.Count < monthsBack)
                return 0;
            var pos = PosOf(p);
            var old = p.growthHistory[p.growthHistory.Count - monthsBack];
            return (int)Math.Round(RelevantMean(p.stats, pos))
                - (int)Math.Round(RelevantMean(old.stats, pos));
        }
    }
}
