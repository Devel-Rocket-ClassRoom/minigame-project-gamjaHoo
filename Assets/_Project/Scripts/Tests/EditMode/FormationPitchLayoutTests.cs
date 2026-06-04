// FormationPitchLayoutTests.cs
// Stage H.2 — FormationPitchLayout 좌표 산출 검증 (런타임 계산 A안).

using FMLite.Domain;
using FMLite.UI;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests.EditMode
{
    public class FormationPitchLayoutTests
    {
        private static FormationSO MakeFormation(string name, params Position[] slots)
        {
            var so = ScriptableObject.CreateInstance<FormationSO>();
            so.displayName = name;
            so.slotPositions = slots;
            return so;
        }

        // 6 시드 포메이션 (SeedV10Data.GenerateFormations 와 동일 슬롯 순서).
        private static FormationSO F442() =>
            MakeFormation(
                "4-4-2",
                Position.GK,
                Position.CB,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.CM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.ST,
                Position.ST
            );

        private static FormationSO F532() =>
            MakeFormation(
                "5-3-2",
                Position.GK,
                Position.CB,
                Position.CB,
                Position.CB,
                Position.WB,
                Position.WB,
                Position.DM,
                Position.CM,
                Position.CM,
                Position.ST,
                Position.ST
            );

        private static FormationSO[] AllSix() =>
            new[]
            {
                F442(),
                MakeFormation(
                    "4-3-3",
                    Position.GK,
                    Position.CB,
                    Position.CB,
                    Position.LB,
                    Position.RB,
                    Position.DM,
                    Position.CM,
                    Position.CM,
                    Position.LW,
                    Position.RW,
                    Position.CF
                ),
                MakeFormation(
                    "3-5-2",
                    Position.GK,
                    Position.CB,
                    Position.CB,
                    Position.CB,
                    Position.LM,
                    Position.RM,
                    Position.CM,
                    Position.CM,
                    Position.DM,
                    Position.ST,
                    Position.ST
                ),
                MakeFormation(
                    "4-2-3-1",
                    Position.GK,
                    Position.CB,
                    Position.CB,
                    Position.LB,
                    Position.RB,
                    Position.DM,
                    Position.DM,
                    Position.LM,
                    Position.AM,
                    Position.RM,
                    Position.ST
                ),
                MakeFormation(
                    "4-4-1-1",
                    Position.GK,
                    Position.CB,
                    Position.CB,
                    Position.LB,
                    Position.RB,
                    Position.CM,
                    Position.CM,
                    Position.LM,
                    Position.RM,
                    Position.AM,
                    Position.ST
                ),
                F532(),
            };

        [Test]
        public void T1_442_GkLowest_StHighest_AllInRange()
        {
            var c = FormationPitchLayout.Compute(F442());
            Assert.AreEqual(11, c.Length);
            foreach (var p in c)
            {
                Assert.That(p.x, Is.InRange(0f, 1f));
                Assert.That(p.y, Is.InRange(0f, 1f));
            }
            // GK(슬롯0) 가 최저 y, ST(슬롯9,10) 가 최고 y.
            for (int i = 1; i < 11; i++)
                Assert.Less(c[0].y, c[i].y, $"GK should be deepest, slot {i} y={c[i].y}");
            Assert.Greater(c[9].y, c[5].y); // ST > CM
            Assert.Greater(c[10].y, c[5].y);
            Assert.AreEqual(0.5f, c[0].x, 0.001f); // GK 중앙
        }

        [Test]
        public void T2_442_DefenderRow_LateralOrder_LB_CB_CB_RB()
        {
            var c = FormationPitchLayout.Compute(F442());
            // 슬롯 1,2=CB / 3=LB / 4=RB → x: LB < CB,CB < RB
            Assert.Less(c[3].x, c[1].x, "LB left of CB");
            Assert.Less(c[3].x, c[2].x);
            Assert.Greater(c[4].x, c[1].x, "RB right of CB");
            Assert.Greater(c[4].x, c[2].x);
        }

        [Test]
        public void T3_532_WingBacks_AtExtremes_OfDefRow()
        {
            var c = FormationPitchLayout.Compute(F532());
            // 수비 행 = 슬롯 1,2,3(CB) + 4,5(WB). WB 둘이 최좌/최우.
            float[] xs = { c[1].x, c[2].x, c[3].x, c[4].x, c[5].x };
            float min = Mathf.Min(xs),
                max = Mathf.Max(xs);
            // WB(4,5) 중 하나가 min, 다른 하나가 max.
            bool wbMin = Mathf.Approximately(c[4].x, min) || Mathf.Approximately(c[5].x, min);
            bool wbMax = Mathf.Approximately(c[4].x, max) || Mathf.Approximately(c[5].x, max);
            Assert.IsTrue(wbMin, "a WB should be leftmost");
            Assert.IsTrue(wbMax, "a WB should be rightmost");
        }

        [Test]
        public void T4_AllFormations_11Coords_GkDeepest_InRange()
        {
            foreach (var f in AllSix())
            {
                var c = FormationPitchLayout.Compute(f);
                Assert.AreEqual(11, c.Length, f.displayName);
                for (int i = 0; i < 11; i++)
                {
                    Assert.That(c[i].x, Is.InRange(0f, 1f), $"{f.displayName} slot {i} x");
                    Assert.That(c[i].y, Is.InRange(0f, 1f), $"{f.displayName} slot {i} y");
                }
                for (int i = 1; i < 11; i++)
                    Assert.LessOrEqual(c[0].y, c[i].y, $"{f.displayName}: GK not deepest vs {i}");
            }
        }

        [Test]
        public void T5_BadDisplayName_FallsBackToBandRows()
        {
            // displayName 파싱 불가 → 라인 밴드 폴백으로도 11 좌표 + GK 최저 y.
            var f = MakeFormation(
                "weird",
                Position.GK,
                Position.CB,
                Position.CB,
                Position.LB,
                Position.RB,
                Position.CM,
                Position.CM,
                Position.LM,
                Position.RM,
                Position.ST,
                Position.ST
            );
            var c = FormationPitchLayout.Compute(f);
            Assert.AreEqual(11, c.Length);
            for (int i = 1; i < 11; i++)
                Assert.LessOrEqual(c[0].y, c[i].y);
        }
    }
}
