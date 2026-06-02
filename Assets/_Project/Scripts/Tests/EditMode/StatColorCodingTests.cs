// StatColorCodingTests.cs
// Stage C (V1.0, #455) — stat 등급 색상 코딩 (C.2) + 성장 화살표 (C.4) 경계값 검증.
//   T1  Classify 등급 경계값 (80/65/50/35)
//   T2  GradeColor 등급별 색 일치
//   T3  GradeNameKey 매핑
//   T4  Trend 변화량 경계값 (+2 / +1 / 0 / -1 / -2)
//   T5  TrendColor / TrendArrow 매핑

using FMLite.UI;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class StatColorCodingTests
    {
        // ── T1. Classify 등급 경계값 ──────────────────────────────────
        [Test]
        public void T1_Classify_Boundaries()
        {
            Assert.AreEqual(StatGrade.Elite, StatColorCoding.Classify(100), "100 = Elite");
            Assert.AreEqual(StatGrade.Elite, StatColorCoding.Classify(80), "80 = Elite (경계)");
            Assert.AreEqual(StatGrade.Good, StatColorCoding.Classify(79), "79 = Good");
            Assert.AreEqual(StatGrade.Good, StatColorCoding.Classify(65), "65 = Good (경계)");
            Assert.AreEqual(StatGrade.Average, StatColorCoding.Classify(64), "64 = Average");
            Assert.AreEqual(StatGrade.Average, StatColorCoding.Classify(50), "50 = Average (경계)");
            Assert.AreEqual(StatGrade.Weak, StatColorCoding.Classify(49), "49 = Weak");
            Assert.AreEqual(StatGrade.Weak, StatColorCoding.Classify(35), "35 = Weak (경계)");
            Assert.AreEqual(StatGrade.Poor, StatColorCoding.Classify(34), "34 = Poor");
            Assert.AreEqual(StatGrade.Poor, StatColorCoding.Classify(0), "0 = Poor");
        }

        // ── T2. GradeColor 등급별 색 일치 ─────────────────────────────
        [Test]
        public void T2_GradeColor_MatchesSpec()
        {
            Assert.AreEqual(Hex(0x2ECC71), StatColorCoding.GradeColor(80), "Elite #2ECC71");
            Assert.AreEqual(Hex(0x82E08A), StatColorCoding.GradeColor(70), "Good #82E08A");
            Assert.AreEqual(Hex(0xBBBBBB), StatColorCoding.GradeColor(55), "Average #BBBBBB");
            Assert.AreEqual(Hex(0xF39C12), StatColorCoding.GradeColor(40), "Weak #F39C12");
            Assert.AreEqual(Hex(0xE74C3C), StatColorCoding.GradeColor(20), "Poor #E74C3C");
        }

        // ── T3. GradeNameKey 매핑 ─────────────────────────────────────
        [Test]
        public void T3_GradeNameKey_Mapping()
        {
            Assert.AreEqual("stat_grade_elite", StatColorCoding.GradeNameKey(StatGrade.Elite));
            Assert.AreEqual("stat_grade_good", StatColorCoding.GradeNameKey(StatGrade.Good));
            Assert.AreEqual("stat_grade_average", StatColorCoding.GradeNameKey(StatGrade.Average));
            Assert.AreEqual("stat_grade_weak", StatColorCoding.GradeNameKey(StatGrade.Weak));
            Assert.AreEqual("stat_grade_poor", StatColorCoding.GradeNameKey(StatGrade.Poor));
        }

        // ── T4. Trend 변화량 경계값 ───────────────────────────────────
        [Test]
        public void T4_Trend_Boundaries()
        {
            Assert.AreEqual(GrowthTrend.StrongUp, StatColorCoding.Trend(5), "+5 = StrongUp");
            Assert.AreEqual(GrowthTrend.StrongUp, StatColorCoding.Trend(2), "+2 = StrongUp (경계)");
            Assert.AreEqual(GrowthTrend.Up, StatColorCoding.Trend(1), "+1 = Up");
            Assert.AreEqual(GrowthTrend.Flat, StatColorCoding.Trend(0), "0 = Flat");
            Assert.AreEqual(GrowthTrend.Down, StatColorCoding.Trend(-1), "-1 = Down");
            Assert.AreEqual(GrowthTrend.StrongDown, StatColorCoding.Trend(-2), "-2 = StrongDown (경계)");
            Assert.AreEqual(GrowthTrend.StrongDown, StatColorCoding.Trend(-5), "-5 = StrongDown");
        }

        // ── T5. TrendColor / TrendArrow 매핑 ──────────────────────────
        [Test]
        public void T5_TrendColorAndArrow_Mapping()
        {
            Assert.AreEqual(Hex(0x1E8449), StatColorCoding.TrendColor(2), "StrongUp 진녹");
            Assert.AreEqual(Hex(0x2ECC71), StatColorCoding.TrendColor(1), "Up 녹");
            Assert.AreEqual(Hex(0x999999), StatColorCoding.TrendColor(0), "Flat 회");
            Assert.AreEqual(Hex(0xE87040), StatColorCoding.TrendColor(-1), "Down 주황");
            Assert.AreEqual(Hex(0xE74C3C), StatColorCoding.TrendColor(-2), "StrongDown 빨강");

            // 화살표 — 5단계 모두 서로 다른 글리프
            string up2 = StatColorCoding.TrendArrow(2);
            string up1 = StatColorCoding.TrendArrow(1);
            string flat = StatColorCoding.TrendArrow(0);
            string dn1 = StatColorCoding.TrendArrow(-1);
            string dn2 = StatColorCoding.TrendArrow(-2);
            Assert.IsFalse(string.IsNullOrEmpty(up2));
            CollectionAssert.AllItemsAreUnique(new[] { up2, up1, flat, dn1, dn2 }, "5단계 화살표 고유");
        }

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
