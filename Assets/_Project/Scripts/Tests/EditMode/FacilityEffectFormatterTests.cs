// FacilityEffectFormatterTests.cs
// V1.0 D.5 — 시설별 효과 문자열 변환 단위 검증.
// Localization 의존이므로 SetUp 에서 LocalizationSystem.Initialize.

using FMLite.Application;
using FMLite.Domain;
using FMLite.UI;
using NUnit.Framework;
using UnityEngine;

namespace FMLite.Tests
{
    public class FacilityEffectFormatterTests
    {
        private LocalizationSO _so;

        [SetUp]
        public void SetUp()
        {
            _so = ScriptableObject.CreateInstance<LocalizationSO>();
            // 단위 테스트용 — placeholder format. 실제 시드는 더 자연스러운 한국어/영어.
            AddEntry("facility_name_scout", "스카우트", "Scouting");
            AddEntry("facility_name_training", "훈련 시설", "Training");
            AddEntry("facility_name_medical", "의료", "Medical");
            AddEntry("facility_name_gym", "체육관", "Gym");
            AddEntry("facility_name_stadium", "스타디움", "Stadium");
            AddEntry("facility_name_youth_coach", "유스 코치", "Youth Coaching");
            AddEntry("facility_name_youth_recruitment", "유스 모집", "Youth Recruitment");
            AddEntry("facility_name_youth_facility", "유스 시설", "Youth Facilities");

            AddEntry("facility_effect_scout_fmt", "명단 {0}명 · ±{1} CA", "List {0} · ±{1} CA");
            AddEntry("facility_effect_training_fmt", "훈련 효율 ×{0}", "Training ×{0}");
            AddEntry("facility_effect_medical_fmt", "부상률 ×{0} · 회복 ×{1}", "Injury ×{0} · Recovery ×{1}");
            AddEntry("facility_effect_gym_fmt", "피지컬 성장 ×{0}", "Physical ×{0}");
            AddEntry("facility_effect_stadium_fmt", "입장료 £{0} · 명성 +{1}", "Ticket £{0} · Rep +{1}");
            AddEntry("facility_effect_youth_coach_fmt", "유스 +{0} PA · 트레잇 {1}%", "Youth +{0} PA · Trait {1}%");
            AddEntry("facility_effect_youth_recruitment_fmt", "풀 {0}명", "Pool {0}");
            AddEntry("facility_effect_youth_facility_fmt", "유스 성장 ×{0}", "Youth Growth ×{0}");

            LocalizationSystem.Initialize(_so, Language.Korean);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_so);
        }

        // ── GetDisplayName — 시설 종류별 이름 ────────────────────────

        [Test]
        public void GetDisplayName_AllTypes_NonEmpty()
        {
            foreach (FacilityType type in System.Enum.GetValues(typeof(FacilityType)))
            {
                string name = FacilityEffectFormatter.GetDisplayName(type);
                Assert.IsFalse(string.IsNullOrEmpty(name), $"{type} 이름 비어있음");
            }
        }

        [Test]
        public void GetDisplayName_Medical_Korean()
        {
            Assert.AreEqual("의료", FacilityEffectFormatter.GetDisplayName(FacilityType.Medical));
        }

        // ── FormatEffect — 시설별 분기 ───────────────────────────────

        [Test]
        public void FormatEffect_NullSO_ReturnsEmpty()
        {
            string s = FacilityEffectFormatter.FormatEffect(FacilityType.Scout, null);
            Assert.AreEqual(string.Empty, s);
        }

        [Test]
        public void FormatEffect_Scout_FormatsListAndAccuracy()
        {
            var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
            so.scoutingListSize = 200;
            so.caAccuracyMargin = 15;
            string s = FacilityEffectFormatter.FormatEffect(FacilityType.Scout, so);
            StringAssert.Contains("200", s);
            StringAssert.Contains("15", s);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void FormatEffect_Training_FormatsEfficiency()
        {
            var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
            so.trainingEfficiency = 1.50f;
            string s = FacilityEffectFormatter.FormatEffect(FacilityType.Training, so);
            StringAssert.Contains("1.50", s);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void FormatEffect_Medical_FormatsBothMultipliers()
        {
            var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
            so.injuryRateMultiplier = 0.85f;
            so.recoverySpeedMultiplier = 1.25f;
            string s = FacilityEffectFormatter.FormatEffect(FacilityType.Medical, so);
            StringAssert.Contains("0.85", s);
            StringAssert.Contains("1.25", s);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void FormatEffect_Gym_FormatsPhysical()
        {
            var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
            so.physicalGrowthBonus = 1.20f;
            string s = FacilityEffectFormatter.FormatEffect(FacilityType.Gym, so);
            StringAssert.Contains("1.20", s);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void FormatEffect_Stadium_FormatsRevenueAndRep()
        {
            var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
            so.ticketRevenueBase = 5000;
            so.reputationBonus = 3;
            string s = FacilityEffectFormatter.FormatEffect(FacilityType.Stadium, so);
            StringAssert.Contains("5000", s);
            StringAssert.Contains("3", s);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void FormatEffect_YouthCoach_FormatsPaAndTrait()
        {
            var so = ScriptableObject.CreateInstance<FacilityLevelSO>();
            so.youthAvgPABonus = 10;
            so.traitGrantChance = 0.30f;
            string s = FacilityEffectFormatter.FormatEffect(FacilityType.YouthCoach, so);
            StringAssert.Contains("10", s);
            StringAssert.Contains("30", s); // 0.30 × 100 = 30
            Object.DestroyImmediate(so);
        }

        private void AddEntry(string key, string ko, string en)
        {
            _so.entries.Add(new LocalizationEntry { key = key, korean = ko, english = en });
        }
    }
}
