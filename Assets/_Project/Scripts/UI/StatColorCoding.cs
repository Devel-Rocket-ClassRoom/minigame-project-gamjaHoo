// StatColorCoding.cs
// Stage C (V1.0) — stat 등급 색상 코딩 (C.2) + 성장 동향 화살표 (C.4).
// 표시 전용 로직 (게임 룰 아님 → GameBalanceSO 외부화 대상 아님). algorithms.md V1.0-12.
//   · 등급: 80+ Elite / 65-79 Good / 50-64 Average / 35-49 Weak / -34 Poor
//   · 화살표: +2↑ / +1↗ / 0→ / -1↘ / -2↓ (직전 3개월 변화량, GrowthSystem.GetStatChange)
// 색 팔레트는 muip-reference §18 / tasks C.2·C.4 확정값.

using UnityEngine;

namespace FMLite.UI
{
    /// <summary>stat 값 등급 (C.2). 경계값 = 임계값 이상.</summary>
    public enum StatGrade
    {
        Poor, // -34
        Weak, // 35-49
        Average, // 50-64
        Good, // 65-79
        Elite, // 80+
    }

    /// <summary>성장 동향 (C.4). 직전 3개월 stat 변화량 기준.</summary>
    public enum GrowthTrend
    {
        StrongDown, // -2 이하
        Down, // -1
        Flat, // 0
        Up, // +1
        StrongUp, // +2 이상
    }

    public static class StatColorCoding
    {
        // ── 등급 색 (C.2) ─────────────────────────────────────────────────
        private static readonly Color EliteColor = Hex(0x2ECC71);
        private static readonly Color GoodColor = Hex(0x82E08A);
        private static readonly Color AverageColor = Hex(0xBBBBBB);
        private static readonly Color WeakColor = Hex(0xF39C12);
        private static readonly Color PoorColor = Hex(0xE74C3C);

        // ── 성장 화살표 색 (C.4) ──────────────────────────────────────────
        private static readonly Color TrendStrongUp = Hex(0x1E8449); // 진녹
        private static readonly Color TrendUp = Hex(0x2ECC71); // 녹
        private static readonly Color TrendFlat = Hex(0x999999); // 회
        private static readonly Color TrendDown = Hex(0xE87040); // 주황
        private static readonly Color TrendStrongDown = Hex(0xE74C3C); // 빨강

        // ── 등급 분류 (C.2) ───────────────────────────────────────────────

        /// <summary>stat 값 → 등급. 경계값은 임계값 이상 (80+ = Elite).</summary>
        public static StatGrade Classify(int value)
        {
            if (value >= 80)
                return StatGrade.Elite;
            if (value >= 65)
                return StatGrade.Good;
            if (value >= 50)
                return StatGrade.Average;
            if (value >= 35)
                return StatGrade.Weak;
            return StatGrade.Poor;
        }

        public static Color GradeColor(int value) => GradeColor(Classify(value));

        public static Color GradeColor(StatGrade grade) =>
            grade switch
            {
                StatGrade.Elite => EliteColor,
                StatGrade.Good => GoodColor,
                StatGrade.Average => AverageColor,
                StatGrade.Weak => WeakColor,
                _ => PoorColor,
            };

        /// <summary>등급명 Localization 키 (툴팁용). 예: "stat_grade_elite".</summary>
        public static string GradeNameKey(StatGrade grade) =>
            grade switch
            {
                StatGrade.Elite => "stat_grade_elite",
                StatGrade.Good => "stat_grade_good",
                StatGrade.Average => "stat_grade_average",
                StatGrade.Weak => "stat_grade_weak",
                _ => "stat_grade_poor",
            };

        // ── 성장 동향 (C.4) ───────────────────────────────────────────────

        /// <summary>직전 3개월 변화량 → 동향. +2 이상 StrongUp / -2 이하 StrongDown.</summary>
        public static GrowthTrend Trend(int change)
        {
            if (change >= 2)
                return GrowthTrend.StrongUp;
            if (change == 1)
                return GrowthTrend.Up;
            if (change == 0)
                return GrowthTrend.Flat;
            if (change == -1)
                return GrowthTrend.Down;
            return GrowthTrend.StrongDown; // <= -2
        }

        public static Color TrendColor(int change) => TrendColor(Trend(change));

        public static Color TrendColor(GrowthTrend trend) =>
            trend switch
            {
                GrowthTrend.StrongUp => TrendStrongUp,
                GrowthTrend.Up => TrendUp,
                GrowthTrend.Flat => TrendFlat,
                GrowthTrend.Down => TrendDown,
                _ => TrendStrongDown,
            };

        /// <summary>
        /// 성장 동향 표시 토큰 — 부호付 증감값 ("+2" / "-1") 또는 변화 없음 대시 ("-").
        /// NotoSansKR SDF 아틀라스에 ↑↓↗↘ 글리프 미포함 (→/★☆/ASCII 만 존재) → 화살표 대신
        /// 색상(TrendColor) + 부호 숫자로 방향·크기 전달 (폰트 안전, design-decisions 미지원 글리프 금지).
        /// 변화 없음(0)은 허전한 "0" 대신 대시 "-" (색은 Flat 회색) 로 표기.
        /// </summary>
        public static string TrendArrow(int change) =>
            change > 0 ? "+" + change
            : change == 0 ? "-"
            : change.ToString();

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
