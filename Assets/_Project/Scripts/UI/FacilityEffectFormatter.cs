// FacilityEffectFormatter.cs
// FacilityLevelSO → 효과 문자열 변환. 시설 종류별 분기.
// V1.0 D.5.

using FMLite.Application;
using FMLite.Domain;

namespace FMLite.UI
{
    public static class FacilityEffectFormatter
    {
        // 시설 이름 (Localization key 기반).
        public static string GetDisplayName(FacilityType type)
        {
            string key = type switch
            {
                FacilityType.Scout => "facility_name_scout",
                FacilityType.Training => "facility_name_training",
                FacilityType.YouthCoach => "facility_name_youth_coach",
                FacilityType.YouthRecruitment => "facility_name_youth_recruitment",
                FacilityType.YouthFacility => "facility_name_youth_facility",
                FacilityType.Medical => "facility_name_medical",
                FacilityType.Stadium => "facility_name_stadium",
                FacilityType.Gym => "facility_name_gym",
                _ => "facility_name_scout",
            };
            return Localization.Get(key);
        }

        // 현재 또는 다음 등급의 효과 요약 문자열.
        // so 가 null 이면 빈 문자열 반환.
        public static string FormatEffect(FacilityType type, FacilityLevelSO so)
        {
            if (so == null)
                return string.Empty;

            return type switch
            {
                FacilityType.Scout => Localization.Get(
                    "facility_effect_scout_fmt",
                    so.scoutingListSize,
                    so.caAccuracyMargin
                ),
                FacilityType.Training => Localization.Get(
                    "facility_effect_training_fmt",
                    so.trainingEfficiency.ToString("0.00")
                ),
                FacilityType.YouthCoach => Localization.Get(
                    "facility_effect_youth_coach_fmt",
                    so.youthAvgPABonus,
                    (so.traitGrantChance * 100).ToString("0")
                ),
                FacilityType.YouthRecruitment => Localization.Get(
                    "facility_effect_youth_recruitment_fmt",
                    so.youthPoolSize
                ),
                FacilityType.YouthFacility => Localization.Get(
                    "facility_effect_youth_facility_fmt",
                    so.youthGrowthRate.ToString("0.00")
                ),
                FacilityType.Medical => Localization.Get(
                    "facility_effect_medical_fmt",
                    so.injuryRateMultiplier.ToString("0.00"),
                    so.recoverySpeedMultiplier.ToString("0.00")
                ),
                FacilityType.Stadium => Localization.Get(
                    "facility_effect_stadium_fmt",
                    so.ticketRevenueBase,
                    so.reputationBonus
                ),
                FacilityType.Gym => Localization.Get(
                    "facility_effect_gym_fmt",
                    so.physicalGrowthBonus.ToString("0.00")
                ),
                _ => string.Empty,
            };
        }
    }
}
